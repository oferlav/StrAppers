using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace StudentTeamBuilderService;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly KickoffConfig _kickoffConfig;
    private readonly ProjectCriteriaConfig _criteriaConfig;
    private readonly Random _random = new();

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration, IOptions<KickoffConfig> kickoffConfig, IOptions<ProjectCriteriaConfig> criteriaConfig)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _kickoffConfig = kickoffConfig.Value;
        _criteriaConfig = criteriaConfig.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = _configuration.GetValue<int>("Worker:IntervalMinutes", 5);
        var baseUrl = _configuration.GetValue<string>("Backend:BaseUrl") ?? "http://localhost:9001";
        var connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        var configFilePath = _configuration["ConfigFilePath"] ?? "Unknown";

        _logger.LogInformation("Student Team Builder Worker started. Interval: {Interval} minutes, Backend: {Backend}, DB: {HasConn}", intervalMinutes, baseUrl, !string.IsNullOrWhiteSpace(connectionString));
        _logger.LogInformation("[CONFIG] Config file path: {ConfigPath}", configFilePath);
        _logger.LogInformation("[CONFIG] KickoffConfig values: MinimumStudents={MinimumStudents}, RequireAdmin={RequireAdmin}, RequireUIUXDesigner={RequireUIUXDesigner}, RequireProductManager={RequireProductManager}, RequireDeveloperRule={RequireDeveloperRule}, MaxPendingTime={MaxPendingTime}",
            _kickoffConfig.MinimumStudents, _kickoffConfig.RequireAdmin, _kickoffConfig.RequireUIUXDesigner, _kickoffConfig.RequireProductManager, _kickoffConfig.RequireDeveloperRule, _kickoffConfig.MaxPendingTime);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[ITERATION] Starting iteration at {Time}", DateTime.UtcNow);
                await ExpireOldPendingAsync(connectionString, stoppingToken);
                await UpdateProjectCriteriaAsync(connectionString, stoppingToken);

                var created = await TryCreateBoardsAsync(connectionString, baseUrl, stoppingToken);
                _logger.LogInformation("[ITERATION] Completed at {Time}. Boards created: {Created}", DateTime.UtcNow, created);

                await LogInstituteDiagnosticsAsync(connectionString, stoppingToken);
                await CallRunDueSprintMergesAsync(baseUrl, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker iteration error: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
        }
    }

    private async Task<int> TryCreateBoardsAsync(string connectionString, string baseUrl, CancellationToken ct)
    {
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Build candidates for ANY priority (1..4). Each student may appear multiple times (once per prioritized project).
        var sql = @"SELECT s.""Id"", s.""IsAdmin"", s.""StartPendingAt"", p.prjId AS ""ProjectId"", r.""RoleId"", ro.""Type"" AS ""RoleType"", ro.""Name"" AS ""RoleName"", p.prio AS ""PriorityRank""
                    FROM ""Students"" s
                    LEFT JOIN (
                        SELECT sr.""StudentId"", sr.""RoleId""
                        FROM ""StudentRoles"" sr
                        WHERE sr.""IsActive"" = TRUE
                    ) r ON r.""StudentId"" = s.""Id""
                    LEFT JOIN ""Roles"" ro ON ro.""Id"" = r.""RoleId""
                    CROSS JOIN LATERAL (
                        VALUES (s.""ProjectPriority1"", 1),
                               (s.""ProjectPriority2"", 2),
                               (s.""ProjectPriority3"", 3),
                               (s.""ProjectPriority4"", 4)
                    ) AS p(prjId, prio)
                    WHERE s.""Status"" = 1 AND p.prjId IS NOT NULL
                      AND s.""InstitutePriority1"" IS NULL
                      AND s.""InstitutePriority2"" IS NULL
                      AND s.""InstitutePriority3"" IS NULL
                      AND s.""InstitutePriority4"" IS NULL";

        var all = (await conn.QueryAsync<StudentCandidate>(new CommandDefinition(sql, cancellationToken: ct))).ToList();
        _logger.LogInformation("[CANDIDATES] Total rows (by project priority expansion): {Count}", all.Count);
        
        // DEBUG: Log detailed candidate information from SQL query
        if (all.Any())
        {
            _logger.LogInformation("[CANDIDATES] Detailed candidate breakdown:");
            foreach (var candidate in all)
            {
                _logger.LogInformation("[CANDIDATES]   StudentId={StudentId}, ProjectId={ProjectId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}, PriorityRank={PriorityRank}",
                    candidate.Id, candidate.ProjectId?.ToString() ?? "NULL", candidate.RoleId?.ToString() ?? "NULL", 
                    candidate.RoleType?.ToString() ?? "NULL", candidate.RoleName ?? "NULL", candidate.IsAdmin, candidate.PriorityRank);
            }
        }
        
        if (!all.Any()) return 0;

        // Group by projectId, prefer groups where candidates have lower PriorityRank and older StartPendingAt
        var byProject = all
            .GroupBy(c => c.ProjectId!.Value)
            .OrderBy(g => g.Min(x => x.PriorityRank))
            .ThenBy(g => g.Min(x => x.StartPendingAt ?? DateTime.UtcNow));

        foreach (var group in byProject)
        {
            var projectId = group.Key;
            _logger.LogInformation("[GROUP] Project {ProjectId}: candidates={Count}, priorities=[{Priorities}]", projectId, group.Count(), string.Join(",", group.Select(x => x.PriorityRank).Distinct().OrderBy(x => x)));
            // Order candidates: priority asc, StartPendingAt asc to prefer best choices and oldest pending
            var ordered = group
                .OrderBy(c => c.PriorityRank)
                .ThenBy(c => c.StartPendingAt ?? DateTime.UtcNow)
                .ToList();

            var chosen = SelectGroup(ordered, _kickoffConfig, _logger);
            if (chosen == null) continue;

            var ids = chosen.Select(c => c.Id).Distinct().ToArray();
            _logger.LogInformation("[SELECTED] Project {ProjectId}: studentIds=[{Ids}], roles=[{Roles}], admins={AdminCount}", projectId, string.Join(',', ids), string.Join(',', chosen.Select(c => $"{c.RoleName}:{c.RoleType}")), chosen.Count(c => c.IsAdmin));
            using var tx = conn.BeginTransaction();
            try
            {
                // Update Status and ProjectId for all selected students
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE \"Students\" SET \"Status\"=2, \"ProjectId\"=@ProjectId, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                    new { Ids = ids, ProjectId = projectId }, transaction: tx, cancellationToken: ct));

                _logger.LogInformation("[UPDATE] Updated {Count} students with ProjectId={ProjectId} and Status=2", ids.Length, projectId);

                await tx.CommitAsync(ct);
            }
            catch
            {
                await tx.RollbackAsync(ct);
                throw;
            }

            var projectTitle = await conn.ExecuteScalarAsync<string>(new CommandDefinition(
                "SELECT \"Title\" FROM \"Projects\" WHERE \"Id\"=@Id", new { Id = projectId }, cancellationToken: ct));

            var client = _httpClientFactory.CreateClient();
            // Increase timeout to 20 minutes - board creation can take 10-15 minutes due to Neon branch creation,
            // database setup, GitHub repos, Railway deployment, etc.
            client.Timeout = TimeSpan.FromMinutes(20);

            // Log InstituteId for each student so we can verify the join will find QuestMode
            var studentInstitutes = await conn.QueryAsync<(int Id, int? InstituteId)>(new CommandDefinition(
                @"SELECT ""Id"", ""InstituteId"" FROM ""Students"" WHERE ""Id"" = ANY(@Ids)",
                new { Ids = ids }, cancellationToken: ct));
            foreach (var si in studentInstitutes)
                _logger.LogInformation("[QUEST-DEBUG] StudentId={StudentId} InstituteId={InstituteId}", si.Id, si.InstituteId?.ToString() ?? "NULL");

            // Detect if the institute for these students has QuestMode enabled
            var isQuestMode = await conn.ExecuteScalarAsync<bool>(new CommandDefinition(
                @"SELECT COALESCE(i.""QuestMode"", false) FROM ""Students"" s
                  JOIN ""Institutes"" i ON i.""Id"" = s.""InstituteId""
                  WHERE s.""Id"" = ANY(@Ids) AND i.""QuestMode"" = true LIMIT 1",
                new { Ids = ids }, cancellationToken: ct));
            _logger.LogInformation("[QUEST] IsQuestMode={IsQuestMode} for students=[{Ids}]", isQuestMode, string.Join(",", ids));

            // Also log the raw institute QuestMode value to confirm column is read correctly
            var instituteQuestFlags = await conn.QueryAsync<(int InstituteId, bool QuestMode)>(new CommandDefinition(
                @"SELECT DISTINCT i.""Id"", i.""QuestMode"" FROM ""Students"" s
                  JOIN ""Institutes"" i ON i.""Id"" = s.""InstituteId""
                  WHERE s.""Id"" = ANY(@Ids)",
                new { Ids = ids }, cancellationToken: ct));
            foreach (var f in instituteQuestFlags)
                _logger.LogInformation("[QUEST-DEBUG] InstituteId={InstituteId} QuestMode={QuestMode}", f.InstituteId, f.QuestMode);

            var body = new CreateBoardRequest
            {
                ProjectId = projectId,
                StudentIds = ids.ToList(),
                Title = $"{projectTitle} Kickoff meeting",
                DateTime = NextDayNoonUtc().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DurationMinutes = 30,
                IsQuestMode = isQuestMode
            };

            try
            {
                var resp = await client.PostAsJsonAsync($"{baseUrl}/api/Boards/use/create", body, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    // Rollback first so students are reset even if reading/logging the body fails (e.g. huge HTML)
                    await RollbackStudentsAsync(conn, ids, ct);
                    var errorText = await ReadErrorBodyTruncatedAsync(resp, 2048, ct);
                    _logger.LogWarning("[CREATE_BOARD] Failed for project {ProjectId}. Status={Status}. Body={Body}", projectId, resp.StatusCode, errorText);
                    continue;
                }
                var okText = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("[CREATE_BOARD] Success project {ProjectId}: students=[{Ids}] Response={Response}", projectId, string.Join(",", ids), okText);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CREATE_BOARD] Exception for project {ProjectId}: {Message}", projectId, ex.Message);
                await RollbackStudentsAsync(conn, ids, ct);
            }
        }

        return 0;
    }

    /// <summary>Resets selected students to Status=1 and ProjectId=NULL. Retries up to 3 times on failure so students are not left stuck at Status=2 when board creation fails.</summary>
    private async Task RollbackStudentsAsync(NpgsqlConnection conn, int[] ids, CancellationToken ct)
    {
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE \"Students\" SET \"Status\"=1, \"ProjectId\"=NULL, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                    new { Ids = ids }, cancellationToken: ct));
                _logger.LogInformation("[ROLLBACK] Reset {Count} students: Status=1, ProjectId=NULL", ids.Length);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[ROLLBACK] Attempt {Attempt}/{MaxAttempts} failed for {Count} students: {Message}", attempt, maxAttempts, ids.Length, ex.Message);
                if (attempt == maxAttempts)
                    _logger.LogError("[ROLLBACK] All {MaxAttempts} attempts failed. Students may remain at Status=2. Fix manually or retry next iteration.", maxAttempts);
                else
                    await Task.Delay(500 * attempt, ct);
            }
        }
    }

    /// <summary>Reads response body up to maxChars to avoid huge HTML error pages blocking or filling logs.</summary>
    private static async Task<string> ReadErrorBodyTruncatedAsync(HttpResponseMessage resp, int maxChars, CancellationToken ct)
    {
        try
        {
            var full = await resp.Content.ReadAsStringAsync(ct);
            if (string.IsNullOrEmpty(full)) return "(empty)";
            if (full.Length <= maxChars) return full;
            return full.Substring(0, maxChars) + "... [truncated]";
        }
        catch (Exception)
        {
            return "(failed to read body)";
        }
    }

    private async Task CallRunDueSprintMergesAsync(string baseUrl, CancellationToken ct)
    {
        try
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);
            var resp = await client.PostAsync($"{baseUrl}/api/Trello/use/run-due-sprint-merges", content: null, ct);
            if (resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("[RUN-DUE-SPRINT-MERGES] Success: {Response}", body);
            }
            else
            {
                var errorText = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("[RUN-DUE-SPRINT-MERGES] Failed. Status={Status}, Body={Body}", resp.StatusCode, errorText);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[RUN-DUE-SPRINT-MERGES] Exception: {Message}", ex.Message);
        }
    }

    private static DateTime NextDayNoonUtc()
    {
        var next = DateTime.UtcNow.Date.AddDays(1).AddHours(12);
        return next;
    }

    private async Task UpdateProjectCriteriaAsync(string connectionString, CancellationToken ct)
    {
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);

        // Get all projects with their creation dates and existing CriteriaIds
        var projects = (await conn.QueryAsync<(int Id, DateTime CreatedAt, string? CriteriaIds)>(new CommandDefinition(
            @"SELECT ""Id"", ""CreatedAt"", ""CriteriaIds"" FROM ""Projects"" ORDER BY ""Id""",
            cancellationToken: ct))).ToList();

        if (!projects.Any())
        {
            _logger.LogInformation("[CRITERIA] No projects found to update");
            return;
        }

        _logger.LogInformation("[CRITERIA] Processing {Count} projects for criteria generation", projects.Count);

        var updatedCount = 0;
        var cutoffDate = DateTime.UtcNow.AddDays(-_criteriaConfig.NewProjectsMaxDays);

        foreach (var project in projects)
        {
            // Parse existing CriteriaIds into a HashSet
            var criteriaIds = new HashSet<int>();
            if (!string.IsNullOrEmpty(project.CriteriaIds))
            {
                var existingIds = project.CriteriaIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var idStr in existingIds)
                {
                    if (int.TryParse(idStr, out int id))
                    {
                        criteriaIds.Add(id);
                    }
                }
            }

            // New Projects (id=8) - append '8' if project is new and '8' is not already present
            if (project.CreatedAt >= cutoffDate && !criteriaIds.Contains(8))
            {
                criteriaIds.Add(8);
                _logger.LogDebug("[CRITERIA] Project {ProjectId}: Adding CriteriaId 8 (New Project) - CreatedAt: {CreatedAt}", project.Id, project.CreatedAt);
            }

            // Convert to comma-separated string (sorted for consistency)
            var criteriaIdsString = criteriaIds.Count > 0
                ? string.Join(",", criteriaIds.OrderBy(id => id))
                : null;

            // Update the project
            await conn.ExecuteAsync(new CommandDefinition(
                @"UPDATE ""Projects"" SET ""CriteriaIds"" = @CriteriaIds, ""UpdatedAt"" = NOW() WHERE ""Id"" = @ProjectId",
                new { CriteriaIds = criteriaIdsString, ProjectId = project.Id },
                cancellationToken: ct));

            updatedCount++;
            _logger.LogDebug("[CRITERIA] Project {ProjectId}: Updated CriteriaIds = '{CriteriaIds}'", project.Id, criteriaIdsString ?? "NULL");
        }

        _logger.LogInformation("[CRITERIA] Updated criteria for {Count} projects", updatedCount);
    }

    private async Task ExpireOldPendingAsync(string connectionString, CancellationToken ct)
    {
        var hours = _kickoffConfig.MaxPendingTime;
        if (hours <= 0)
        {
            _logger.LogDebug("[EXPIRE] MaxPendingTime is {Hours}; skipping pending expiration.", hours);
            return;
        }
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var sql = @"UPDATE ""Students""
                   SET ""Status""=0,
                       ""ProjectId""=NULL,
                       ""ProjectPriority1""=NULL,
                       ""ProjectPriority2""=NULL,
                       ""ProjectPriority3""=NULL,
                       ""ProjectPriority4""=NULL,
                       ""UpdatedAt""=NOW()
                   WHERE ""Status""=1
                     AND ""StartPendingAt"" IS NOT NULL
                     AND ""StartPendingAt"" < NOW() - make_interval(hours => @Hours)";
        var affected = await conn.ExecuteAsync(new CommandDefinition(sql, new { Hours = hours }, cancellationToken: ct));
        if (affected > 0)
        {
            _logger.LogInformation("[EXPIRE] Reset {Count} students from pending due to timeout (>{Hours}h)", affected, hours);
        }
    }

    private async Task LogInstituteDiagnosticsAsync(string connectionString, CancellationToken ct)
    {
        try
        {
            using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync(ct);

            // Per-student view: all students who have any InstitutePriority set
            var studentSql = @"
                SELECT s.""Id"" AS StudentId, s.""InstituteId"", s.""RoleIndex"", s.""Status"",
                       s.""InstitutePriority1"", s.""InstitutePriority2"",
                       s.""InstitutePriority3"", s.""InstitutePriority4"",
                       ro.""Id"" AS RoleId, ro.""Name"" AS RoleName, ro.""Type"" AS RoleType,
                       s.""StartPendingAt"", s.""Coupon""
                FROM ""Students"" s
                LEFT JOIN ""StudentRoles"" sr ON sr.""StudentId"" = s.""Id"" AND sr.""IsActive"" = TRUE
                LEFT JOIN ""Roles"" ro ON ro.""Id"" = sr.""RoleId""
                WHERE s.""InstitutePriority1"" IS NOT NULL
                   OR s.""InstitutePriority2"" IS NOT NULL
                   OR s.""InstitutePriority3"" IS NOT NULL
                   OR s.""InstitutePriority4"" IS NOT NULL
                ORDER BY s.""InstitutePriority1"", s.""Id""";

            var students = (await conn.QueryAsync<InstituteStudentRow>(new CommandDefinition(studentSql, cancellationToken: ct))).ToList();
            _logger.LogInformation("[INSTITUTE-CANDIDATES] Students with InstitutePriority set: {Count}", students.Count);
            foreach (var s in students)
            {
                _logger.LogInformation(
                    "[INSTITUTE-CANDIDATES]   StudentId={StudentId} Status={Status} InstituteId={InstituteId} RoleIndex={RoleIndex} RoleId={RoleId} RoleName={RoleName} RoleType={RoleType} P1={P1} P2={P2} P3={P3} P4={P4} Coupon={Coupon} StartPendingAt={StartPendingAt}",
                    s.StudentId, s.Status, s.InstituteId?.ToString() ?? "NULL", s.RoleIndex,
                    s.RoleId?.ToString() ?? "NULL", s.RoleName ?? "NULL", s.RoleType?.ToString() ?? "NULL",
                    s.InstitutePriority1?.ToString() ?? "NULL", s.InstitutePriority2?.ToString() ?? "NULL",
                    s.InstitutePriority3?.ToString() ?? "NULL", s.InstitutePriority4?.ToString() ?? "NULL",
                    s.Coupon ?? "NULL",
                    s.StartPendingAt?.ToString("u") ?? "NULL");
            }

            // Per-project summary: how many pending students per InstituteProject
            var projectSql = @"
                SELECT p.prjId AS ProjectId, ip.""Title"" AS ProjectName,
                       COUNT(*) AS TotalCount,
                       COUNT(*) FILTER (WHERE s.""Status"" = 1) AS PendingCount,
                       STRING_AGG(s.""Id""::text, ',' ORDER BY s.""Id"") AS StudentIds,
                       STRING_AGG(COALESCE(ro.""Name"", '(no role)'), ',' ORDER BY s.""Id"") AS RoleNames,
                       STRING_AGG(s.""RoleIndex""::text, ',' ORDER BY s.""Id"") AS RoleIndexes,
                       STRING_AGG(s.""Status""::text, ',' ORDER BY s.""Id"") AS Statuses
                FROM ""Students"" s
                CROSS JOIN LATERAL (
                    VALUES (s.""InstitutePriority1"", 1),
                           (s.""InstitutePriority2"", 2),
                           (s.""InstitutePriority3"", 3),
                           (s.""InstitutePriority4"", 4)
                ) AS p(prjId, prio)
                LEFT JOIN ""InstituteProjects"" ip ON ip.""Id"" = p.prjId
                LEFT JOIN ""StudentRoles"" sr ON sr.""StudentId"" = s.""Id"" AND sr.""IsActive"" = TRUE
                LEFT JOIN ""Roles"" ro ON ro.""Id"" = sr.""RoleId""
                WHERE p.prjId IS NOT NULL
                GROUP BY p.prjId, ip.""Title""
                ORDER BY p.prjId";

            var projects = (await conn.QueryAsync<InstituteProjectRow>(new CommandDefinition(projectSql, cancellationToken: ct))).ToList();
            _logger.LogInformation("[INSTITUTE-GROUP] Institute projects with students: {Count}", projects.Count);
            foreach (var p in projects)
            {
                _logger.LogInformation(
                    "[INSTITUTE-GROUP]   ProjectId={ProjectId} Name={Name} Total={Total} Pending(Status=1)={Pending} StudentIds=[{Ids}] Roles=[{Roles}] RoleIndexes=[{Indexes}] Statuses=[{Statuses}]",
                    p.ProjectId, p.ProjectName ?? "Unknown", p.TotalCount, p.PendingCount,
                    p.StudentIds ?? "", p.RoleNames ?? "", p.RoleIndexes ?? "", p.Statuses ?? "");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[INSTITUTE-CANDIDATES] Diagnostic query failed: {Message}", ex.Message);
        }
    }

    private static double PendingWaitHours(StudentCandidate c, DateTime nowUtc)
    {
        var start = c.StartPendingAt ?? nowUtc;
        return (nowUtc - start).TotalHours;
    }

    private static bool NonDeveloperRequirementsSatisfied(IReadOnlyCollection<StudentCandidate> group, KickoffConfig cfg)
    {
        if (cfg.RequireAdmin && group.Count(x => x.IsAdmin) != 1) return false;
        if (cfg.RequireUIUXDesigner && group.Count(x => x.RoleType == 3) != 1) return false;
        if (cfg.RequireProductManager && group.Count(x => x.RoleType == 4) != 1) return false;
        return true;
    }

    private static void RemoveFrontendBackendFromByRole(Dictionary<int, StudentCandidate> byRole, ILogger logger)
    {
        var frontendBackendRoleIds = byRole.Values
            .Where(x => x.RoleType == 2 &&
                ((x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase) ||
                 (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase)))
            .Select(x => x.RoleId!.Value)
            .ToList();

        foreach (var roleId in frontendBackendRoleIds)
        {
            var removed = byRole[roleId];
            byRole.Remove(roleId);
            logger.LogInformation("[SELECT] Removed from group: StudentId={StudentId}, RoleId={RoleId}, RoleName={RoleName} (split devs vs fullstack)",
                removed.Id, roleId, removed.RoleName);
        }
    }

    private static List<StudentCandidate>? SelectGroup(List<StudentCandidate> candidates, KickoffConfig cfg, ILogger logger)
    {
        // Greedy: one student per RoleId; order is priority rank then StartPendingAt (first wins — no IsAdmin tie-break).
        var byRole = new Dictionary<int, StudentCandidate>();
        logger.LogInformation("[SELECT] Evaluating {Count} candidates", candidates.Count);
        
        // DEBUG: Log all candidates with full details
        logger.LogInformation("[SELECT] All candidates details:");
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            logger.LogInformation("[SELECT]   Candidate {Index}: StudentId={StudentId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}, PriorityRank={PriorityRank}, StartPendingAt={StartPendingAt}",
                i + 1, c.Id, c.RoleId?.ToString() ?? "NULL", c.RoleType?.ToString() ?? "NULL", c.RoleName ?? "NULL", c.IsAdmin, c.PriorityRank, c.StartPendingAt?.ToString() ?? "NULL");
        }
        
        // DEBUG: Check for UI/UX in all candidates
        var uiuxCandidates = candidates.Where(x => x.RoleType == 3).ToList();
        if (uiuxCandidates.Any())
        {
            logger.LogInformation("[SELECT] UI/UX candidates found in ALL candidates: [{Details}]", 
                string.Join(", ", uiuxCandidates.Select(c => $"StudentId={c.Id}, RoleId={c.RoleId}, RoleName={c.RoleName}")));
        }
        else
        {
            logger.LogInformation("[SELECT] WARNING: No UI/UX candidates (RoleType=3) found in ALL candidates");
        }
        
        foreach (var c in candidates)
        {
            if (c.RoleId == null)
            {
                logger.LogInformation("[SELECT] Skipping candidate StudentId={StudentId}: RoleId is NULL", c.Id);
                continue;
            }
            var roleKey = c.RoleId.Value;
            if (!byRole.ContainsKey(roleKey))
            {
                byRole[roleKey] = c;
                logger.LogInformation("[SELECT] Added to byRole: StudentId={StudentId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}",
                    c.Id, roleKey, c.RoleType?.ToString() ?? "NULL", c.RoleName ?? "NULL", c.IsAdmin);
            }
            else
            {
                var existing = byRole[roleKey];
                logger.LogInformation("[SELECT] Skipping candidate StudentId={StudentId}: RoleId={RoleId} already taken by StudentId={ExistingId}",
                    c.Id, roleKey, existing.Id);
            }
            
            // Check if we can stop early, but only if all required roles are satisfied
            // CRITICAL: Don't stop early if we haven't processed all candidates yet - we might miss required roles
            var hasMinimumStudents = byRole.Count >= cfg.MinimumStudents;
            var uiuxCount = byRole.Values.Count(x => x.RoleType == 3);
            var pmCount = byRole.Values.Count(x => x.RoleType == 4);
            var hasRequiredUIUX = !cfg.RequireUIUXDesigner || uiuxCount == 1;
            var hasRequiredProductManager = !cfg.RequireProductManager || pmCount == 1;
            
            // Check if there are more candidates that might contain required roles
            // CRITICAL: Always check for remaining required roles, even if current check passes
            // This ensures we don't stop early if a required role candidate is coming up
            var currentIndex = candidates.IndexOf(c);
            var remainingCandidates = candidates.Skip(currentIndex + 1).ToList();
            var hasRemainingCandidatesWithRequiredRoles = false;
            
            // Check for UI/UX in remaining candidates if UI/UX is required and not satisfied
            if (cfg.RequireUIUXDesigner && uiuxCount != 1)
            {
                var hasUIUXInRemaining = remainingCandidates.Any(x => x.RoleType == 3);
                if (hasUIUXInRemaining)
                {
                    hasRemainingCandidatesWithRequiredRoles = true;
                    logger.LogInformation("[SELECT] Found UI/UX candidate in remaining candidates (count={RemainingCount}), will not stop early", remainingCandidates.Count);
                }
            }
            
            // Check for PM in remaining candidates if PM is required and not satisfied
            // CRITICAL: Check based on actual requirement, not the boolean result
            if (cfg.RequireProductManager && pmCount != 1)
            {
                var hasPMInRemaining = remainingCandidates.Any(x => x.RoleType == 4);
                if (hasPMInRemaining)
                {
                    hasRemainingCandidatesWithRequiredRoles = true;
                    logger.LogInformation("[SELECT] Found PM candidate in remaining candidates (count={RemainingCount}), will not stop early. PM candidates: [{PMCandidates}]", 
                        remainingCandidates.Count,
                        string.Join(", ", remainingCandidates.Where(x => x.RoleType == 4).Select(c => $"StudentId={c.Id}, RoleId={c.RoleId}")));
                }
                else
                {
                    logger.LogInformation("[SELECT] No PM candidate in remaining {RemainingCount} candidates", remainingCandidates.Count);
                }
            }
            
            // CRITICAL FIX: Also check if there are remaining candidates that could improve the team
            // Even if all required roles are satisfied, we should continue if there are more candidates
            // that haven't been processed yet (they might be better matches or add missing roles)
            // Only stop early if we've processed ALL candidates
            var hasMoreCandidates = remainingCandidates.Count > 0;
            
            logger.LogInformation("[SELECT] Early stop check: hasMinimumStudents={Min}, hasRequiredUIUX={UIUX} (count={UIUXCount}), hasRequiredProductManager={PM} (count={PMCount}), hasRemainingCandidatesWithRequiredRoles={HasRemaining}, remainingCandidates={RemainingCount}, hasMoreCandidates={HasMore}",
                hasMinimumStudents, hasRequiredUIUX, uiuxCount, hasRequiredProductManager, pmCount, hasRemainingCandidatesWithRequiredRoles, remainingCandidates.Count, hasMoreCandidates);
            
            // Only stop early if:
            // 1. We have minimum students
            // 2. All required roles are satisfied
            // 3. No remaining candidates with required roles
            // 4. We've processed ALL candidates (no more candidates to process)
            if (hasMinimumStudents && hasRequiredUIUX && hasRequiredProductManager && !hasRemainingCandidatesWithRequiredRoles && !hasMoreCandidates)
            {
                logger.LogInformation("[SELECT] Stopping early: byRole.Count={Count} >= MinimumStudents={Min} AND required roles satisfied AND no more candidates to process", byRole.Count, cfg.MinimumStudents);
                break;
            }
            else if (hasMinimumStudents && hasRequiredUIUX && hasRequiredProductManager && !hasRemainingCandidatesWithRequiredRoles && hasMoreCandidates)
            {
                logger.LogInformation("[SELECT] Continuing: All requirements met but {RemainingCount} more candidates to process - may find better matches", remainingCandidates.Count);
            }
            else if (hasMinimumStudents && (!hasRequiredUIUX || !hasRequiredProductManager))
            {
                logger.LogInformation("[SELECT] Continuing: byRole.Count={Count} >= MinimumStudents={Min} but required roles not yet satisfied (UI/UX={UIUX}, PM={PM}), remaining candidates with required roles={HasRemaining}, continuing search...", 
                    byRole.Count, cfg.MinimumStudents, hasRequiredUIUX, hasRequiredProductManager, hasRemainingCandidatesWithRequiredRoles);
            }
        }

        var group = byRole.Values.ToList();

        // DEBUG: Log the selected group before validation
        logger.LogInformation("[SELECT] Selected group before validation: Count={Count}", group.Count);
        foreach (var g in group)
        {
            logger.LogInformation("[SELECT]   Group member: StudentId={StudentId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}",
                g.Id, g.RoleId?.ToString() ?? "NULL", g.RoleType?.ToString() ?? "NULL", g.RoleName ?? "NULL", g.IsAdmin);
        }

        if (group.Count < cfg.MinimumStudents)
        {
            logger.LogInformation("[SELECT] Rejected: not enough unique-role students (have {Have}, need {Need})", group.Count, cfg.MinimumStudents);
            return null;
        }
        
        // FIX: If UI/UX is required but not exactly 1 in group, try to fix it
        if (cfg.RequireUIUXDesigner)
        {
            var uiuxCount = group.Count(x => x.RoleType == 3);
            if (uiuxCount == 0)
            {
                logger.LogInformation("[SELECT] UI/UX required but not in group. Attempting to add UI/UX candidate...");
                var uiuxCandidate = candidates.FirstOrDefault(x => x.RoleType == 3 && x.RoleId != null);
                if (uiuxCandidate != null)
                {
                    var uiuxRoleId = uiuxCandidate.RoleId!.Value;
                    if (byRole.ContainsKey(uiuxRoleId))
                    {
                        // UI/UX role already in group but with wrong RoleType? This shouldn't happen, but log it
                        logger.LogWarning("[SELECT] WARNING: UI/UX RoleId={RoleId} already in group but RoleType mismatch!", uiuxRoleId);
                    }
                    else
                    {
                        // Add UI/UX candidate to group
                        byRole[uiuxRoleId] = uiuxCandidate;
                        group = byRole.Values.ToList();
                        logger.LogInformation("[SELECT] Added UI/UX candidate: StudentId={StudentId}, RoleId={RoleId}, RoleName={RoleName}",
                            uiuxCandidate.Id, uiuxRoleId, uiuxCandidate.RoleName);
                    }
                }
                else
                {
                    logger.LogInformation("[SELECT] No UI/UX candidate found in candidates list to add");
                }
            }
            else if (uiuxCount > 1)
            {
                // Remove extra UI/UX designers, keep only the first one
                var uiuxMembers = group.Where(x => x.RoleType == 3).ToList();
                for (int i = 1; i < uiuxMembers.Count; i++)
                {
                    var toRemove = uiuxMembers[i];
                    var roleId = toRemove.RoleId!.Value;
                    if (byRole.ContainsKey(roleId))
                    {
                        byRole.Remove(roleId);
                        logger.LogInformation("[SELECT] Removed extra UI/UX designer: StudentId={StudentId}, RoleId={RoleId}", toRemove.Id, roleId);
                    }
                }
                group = byRole.Values.ToList();
            }
        }

        // FIX: If Product Manager is required but not exactly 1 in group, try to fix it
        if (cfg.RequireProductManager)
        {
            var pmCount = group.Count(x => x.RoleType == 4);
            logger.LogInformation("[SELECT] PM Fix check: RequireProductManager={Require}, pmCount={Count}, groupCount={GroupCount}", 
                cfg.RequireProductManager, pmCount, group.Count);
            
            if (pmCount == 0)
            {
                logger.LogInformation("[SELECT] Product Manager required but not in group. Attempting to add Product Manager candidate...");
                logger.LogInformation("[SELECT] Searching in {Count} candidates for PM (RoleType=4)...", candidates.Count);
                var pmCandidates = candidates.Where(x => x.RoleType == 4 && x.RoleId != null).ToList();
                logger.LogInformation("[SELECT] Found {Count} PM candidates: [{Details}]", 
                    pmCandidates.Count,
                    string.Join(", ", pmCandidates.Select(c => $"StudentId={c.Id}, RoleId={c.RoleId}, RoleName={c.RoleName}")));
                
                var pmCandidate = candidates.FirstOrDefault(x => x.RoleType == 4 && x.RoleId != null);
                if (pmCandidate != null)
                {
                    var pmRoleId = pmCandidate.RoleId!.Value;
                    logger.LogInformation("[SELECT] Found PM candidate: StudentId={StudentId}, RoleId={RoleId}, checking if already in byRole...", 
                        pmCandidate.Id, pmRoleId);
                    
                    if (byRole.ContainsKey(pmRoleId))
                    {
                        logger.LogWarning("[SELECT] WARNING: Product Manager RoleId={RoleId} already in group but RoleType mismatch! Existing: StudentId={ExistingId}, RoleType={ExistingType}", 
                            pmRoleId, byRole[pmRoleId].Id, byRole[pmRoleId].RoleType);
                    }
                    else
                    {
                        byRole[pmRoleId] = pmCandidate;
                        group = byRole.Values.ToList();
                        logger.LogInformation("[SELECT] Added Product Manager candidate: StudentId={StudentId}, RoleId={RoleId}, RoleName={RoleName}",
                            pmCandidate.Id, pmRoleId, pmCandidate.RoleName);
                    }
                }
                else
                {
                    logger.LogWarning("[SELECT] No Product Manager candidate found in candidates list to add. Total candidates: {Count}", candidates.Count);
                }
            }
            else if (pmCount > 1)
            {
                // Remove extra Product Managers, keep only the first one
                var pmMembers = group.Where(x => x.RoleType == 4).ToList();
                for (int i = 1; i < pmMembers.Count; i++)
                {
                    var toRemove = pmMembers[i];
                    var roleId = toRemove.RoleId!.Value;
                    if (byRole.ContainsKey(roleId))
                    {
                        byRole.Remove(roleId);
                        logger.LogInformation("[SELECT] Removed extra Product Manager: StudentId={StudentId}, RoleId={RoleId}", toRemove.Id, roleId);
                    }
                }
                group = byRole.Values.ToList();
            }
        }

        // Enforce required roles - exactly 1 for UI/UX and Product Manager
        if (cfg.RequireUIUXDesigner)
        {
            var uiuxCount = group.Count(x => x.RoleType == 3);
            var uiuxInGroup = group.Where(x => x.RoleType == 3).ToList();
            logger.LogInformation("[SELECT] UI/UX check: RequireUIUXDesigner={Require}, UIUXCount={Count}, UIUXMembers=[{Members}]",
                cfg.RequireUIUXDesigner, uiuxCount, string.Join(", ", uiuxInGroup.Select(g => $"StudentId={g.Id}, RoleId={g.RoleId}, RoleName={g.RoleName}")));
            
            if (uiuxCount != 1)
            {
                logger.LogInformation("[SELECT] Rejected: UI/UX count is {Count} (exactly 1 required, RoleType=3)", uiuxCount);
                logger.LogInformation("[SELECT] DEBUG: Checking if UI/UX exists in all candidates but wasn't selected...");
                var uiuxInAllCandidates = candidates.Where(x => x.RoleType == 3).ToList();
                if (uiuxInAllCandidates.Any())
                {
                    logger.LogInformation("[SELECT] DEBUG: Found {Count} UI/UX candidate(s) in ALL candidates that were NOT selected: [{Details}]",
                        uiuxInAllCandidates.Count,
                        string.Join(", ", uiuxInAllCandidates.Select(c => $"StudentId={c.Id}, RoleId={c.RoleId}, RoleName={c.RoleName}, PriorityRank={c.PriorityRank}")));
                }
                return null;
            }
        }

        if (cfg.RequireProductManager)
        {
            var pmCount = group.Count(x => x.RoleType == 4);
            var pmInGroup = group.Where(x => x.RoleType == 4).ToList();
            logger.LogInformation("[SELECT] Product Manager check: RequireProductManager={Require}, PMCount={Count}, PMMembers=[{Members}]",
                cfg.RequireProductManager, pmCount, string.Join(", ", pmInGroup.Select(g => $"StudentId={g.Id}, RoleId={g.RoleId}, RoleName={g.RoleName}")));
            
            if (pmCount != 1)
            {
                logger.LogInformation("[SELECT] Rejected: Product Manager count is {Count} (exactly 1 required, RoleType=4)", pmCount);
                logger.LogInformation("[SELECT] DEBUG: Checking if Product Manager exists in all candidates but wasn't selected...");
                var pmInAllCandidates = candidates.Where(x => x.RoleType == 4).ToList();
                if (pmInAllCandidates.Any())
                {
                    logger.LogInformation("[SELECT] DEBUG: Found {Count} Product Manager candidate(s) in ALL candidates that were NOT selected: [{Details}]",
                        pmInAllCandidates.Count,
                        string.Join(", ", pmInAllCandidates.Select(c => $"StudentId={c.Id}, RoleId={c.RoleId}, RoleName={c.RoleName}, PriorityRank={c.PriorityRank}")));
                }
                return null;
            }
        }

        if (cfg.RequireAdmin && group.Count(x => x.IsAdmin) != 1)
        {
            logger.LogInformation("[SELECT] Rejected: admin count is {Count} (RequireAdmin=true, need exactly 1)", group.Count(x => x.IsAdmin));
            return null;
        }

        if (cfg.RequireDeveloperRule)
        {
            var fullstackCount = group.Count(x => x.RoleType == 1);
            var hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
            var hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

            if (fullstackCount == 1 && hasFrontend && hasBackend)
            {
                var fsMember = byRole.Values.First(x => x.RoleType == 1);
                var feMember = byRole.Values.First(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
                var beMember = byRole.Values.First(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

                if (NonDeveloperRequirementsSatisfied(group, cfg))
                {
                    var now = DateTime.UtcNow;
                    var wFs = PendingWaitHours(fsMember, now);
                    var wFe = PendingWaitHours(feMember, now);
                    var wBe = PendingWaitHours(beMember, now);
                    var preferSplitDevs = wFe > wFs || wBe > wFs;
                    if (preferSplitDevs)
                    {
                        logger.LogInformation("[SELECT] Developer triple: FE/BE waited longer (wFs={WFs}, wFe={WFe}, wBe={WBe}) — keeping Frontend+Backend, removing Fullstack", wFs, wFe, wBe);
                        byRole.Remove(fsMember.RoleId!.Value);
                    }
                    else
                    {
                        logger.LogInformation("[SELECT] Developer triple: Fullstack longest or tied wait (wFs={WFs}, wFe={WFe}, wBe={WBe}) — keeping Fullstack, removing FE/BE", wFs, wFe, wBe);
                        RemoveFrontendBackendFromByRole(byRole, logger);
                    }
                }
                else
                {
                    logger.LogInformation("[SELECT] Developer triple but non-developer requirements not all satisfied — default: remove FE/BE, keep Fullstack");
                    RemoveFrontendBackendFromByRole(byRole, logger);
                }

                group = byRole.Values.ToList();
                fullstackCount = group.Count(x => x.RoleType == 1);
                hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
                hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

                if (group.Count < cfg.MinimumStudents)
                {
                    logger.LogInformation("[SELECT] Rejected: not enough students after developer triple resolution (have {Have}, need {Need})", group.Count, cfg.MinimumStudents);
                    return null;
                }
            }
            else if (fullstackCount == 1 && (hasFrontend || hasBackend))
            {
                logger.LogInformation("[SELECT] Fullstack with partial FE/BE — removing split devs in favor of Fullstack");
                RemoveFrontendBackendFromByRole(byRole, logger);
                group = byRole.Values.ToList();
                fullstackCount = group.Count(x => x.RoleType == 1);
                hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
                hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

                if (group.Count < cfg.MinimumStudents)
                {
                    logger.LogInformation("[SELECT] Rejected: not enough unique-role students after developer rule cleanup (have {Have}, need {Need})", group.Count, cfg.MinimumStudents);
                    return null;
                }
            }

            bool satisfies;
            if (fullstackCount == 1)
            {
                satisfies = !hasFrontend && !hasBackend;
                if (!satisfies)
                {
                    logger.LogInformation("[SELECT] Rejected: developer rule violated - Fullstack exists but also has Frontend={FE} or Backend={BE}", hasFrontend, hasBackend);
                }
            }
            else if (fullstackCount == 0)
            {
                satisfies = hasFrontend && hasBackend;
                if (!satisfies)
                {
                    logger.LogInformation("[SELECT] Rejected: developer rule not satisfied - No Fullstack but missing Frontend={FE} or Backend={BE} (need both)", hasFrontend, hasBackend);
                }
            }
            else
            {
                satisfies = false;
                logger.LogInformation("[SELECT] Rejected: developer rule violated - Multiple Fullstack developers ({Count})", fullstackCount);
            }

            if (!satisfies)
            {
                logger.LogInformation("[SELECT] Rejected: developer rule not satisfied (fullstackCount={Fullstack}, hasFrontend={FE}, hasBackend={BE})", fullstackCount, hasFrontend, hasBackend);
                return null;
            }
        }

        if (cfg.RequireAdmin && group.Count(x => x.IsAdmin) != 1)
        {
            logger.LogInformation("[SELECT] Rejected: admin count after developer rule is {Count} (need exactly 1)", group.Count(x => x.IsAdmin));
            return null;
        }

        logger.LogInformation("[SELECT] Accepted group: ids=[{Ids}] roles=[{Roles}]", string.Join(',', group.Select(g => g.Id)), string.Join(',', group.Select(g => $"{g.RoleName}:{g.RoleType}")));
        return group;
    }
}

public record StudentCandidate
{
    public int Id { get; init; }
    public bool IsAdmin { get; init; }
    public DateTime? StartPendingAt { get; init; }
    public int? ProjectId { get; init; }
    public int? RoleId { get; init; }
    public int? RoleType { get; init; }
    public string? RoleName { get; init; }
    public int PriorityRank { get; init; }
}

public class CreateBoardRequest
{
    public int ProjectId { get; set; }
    public List<int> StudentIds { get; set; } = new();
    public string? Title { get; set; }
    public string? DateTime { get; set; }
    public int? DurationMinutes { get; set; }
    public bool IsQuestMode { get; set; }
}

public record ProjectInfo
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
}

public record InstituteStudentRow
{
    public int StudentId { get; init; }
    public int? InstituteId { get; init; }
    public int RoleIndex { get; init; }
    public int Status { get; init; }
    public int? InstitutePriority1 { get; init; }
    public int? InstitutePriority2 { get; init; }
    public int? InstitutePriority3 { get; init; }
    public int? InstitutePriority4 { get; init; }
    public int? RoleId { get; init; }
    public string? RoleName { get; init; }
    public int? RoleType { get; init; }
    public DateTime? StartPendingAt { get; init; }
    public string? Coupon { get; init; }
}

public record InstituteProjectRow
{
    public int ProjectId { get; init; }
    public string? ProjectName { get; init; }
    public int TotalCount { get; init; }
    public int PendingCount { get; init; }
    public string? StudentIds { get; init; }
    public string? RoleNames { get; init; }
    public string? RoleIndexes { get; init; }
    public string? Statuses { get; init; }
}


