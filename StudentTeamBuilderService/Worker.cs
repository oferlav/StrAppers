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

        _logger.LogInformation("Student Team Builder Worker started. Interval: {Interval} minutes, Backend: {Backend}, DB: {HasConn}", intervalMinutes, baseUrl, !string.IsNullOrWhiteSpace(connectionString));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("[ITERATION] Starting iteration at {Time}", DateTime.UtcNow);
                await ExpireOldPendingAsync(connectionString, stoppingToken);
                await UpdateProjectCriteriaAsync(connectionString, stoppingToken);

                var created = await TryCreateBoardsAsync(connectionString, baseUrl, stoppingToken);
                _logger.LogInformation("[ITERATION] Completed at {Time}. Boards created: {Created}", DateTime.UtcNow, created);
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
                    WHERE s.""Status"" = 1 AND p.prjId IS NOT NULL";

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
            client.Timeout = TimeSpan.FromMinutes(5);

            var body = new CreateBoardRequest
            {
                ProjectId = projectId,
                StudentIds = ids.ToList(),
                Title = $"{projectTitle} Kickoff meeting",
                DateTime = NextDayNoonUtc().ToString("yyyy-MM-ddTHH:mm:ssZ"),
                DurationMinutes = 30
            };

            try
            {
                var resp = await client.PostAsJsonAsync($"{baseUrl}/api/Boards/use/create", body, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    var errorText = await resp.Content.ReadAsStringAsync(ct);
                    _logger.LogWarning("[CREATE_BOARD] Failed for project {ProjectId}. Status={Status}. Body={Body}", projectId, resp.StatusCode, errorText);
                    await conn.ExecuteAsync(new CommandDefinition(
                        "UPDATE \"Students\" SET \"Status\"=1, \"ProjectId\"=NULL, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                        new { Ids = ids }, cancellationToken: ct));
                    _logger.LogInformation("[ROLLBACK] Reset {Count} students: Status=1, ProjectId=NULL", ids.Length);
                    continue;
                }
                var okText = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogInformation("[CREATE_BOARD] Success project {ProjectId}: students=[{Ids}] Response={Response}", projectId, string.Join(",", ids), okText);
                return 1;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[CREATE_BOARD] Exception for project {ProjectId}: {Message}", projectId, ex.Message);
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE \"Students\" SET \"Status\"=1, \"ProjectId\"=NULL, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                    new { Ids = ids }, cancellationToken: ct));
                _logger.LogInformation("[ROLLBACK] Reset {Count} students: Status=1, ProjectId=NULL", ids.Length);
            }
        }

        return 0;
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
        using var conn = new NpgsqlConnection(connectionString);
        await conn.OpenAsync(ct);
        var hours = _kickoffConfig.MaxPendingTime;
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

    private static List<StudentCandidate>? SelectGroup(List<StudentCandidate> candidates, KickoffConfig cfg, ILogger logger)
    {
        // Greedy: unique roles, exactly one admin, at least MinimumStudents
        var byRole = new Dictionary<int, StudentCandidate>();
        StudentCandidate? admin = null;
        logger.LogInformation("[SELECT] Evaluating {Count} candidates", candidates.Count);
        
        // DEBUG: Log all candidates with full details
        logger.LogInformation("[SELECT] All candidates details:");
        for (int i = 0; i < candidates.Count; i++)
        {
            var c = candidates[i];
            logger.LogInformation("[SELECT]   Candidate {Index}: StudentId={StudentId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}, PriorityRank={PriorityRank}, StartPendingAt={StartPendingAt}",
                i + 1, c.Id, c.RoleId?.ToString() ?? "NULL", c.RoleType?.ToString() ?? "NULL", c.RoleName ?? "NULL", c.IsAdmin, c.PriorityRank, c.StartPendingAt?.ToString() ?? "NULL");
        }
        
        var adminIds = candidates.Where(x => x.IsAdmin && x.RoleId != null).Select(x => x.Id).ToList();
        if (adminIds.Any())
        {
            logger.LogInformation("[SELECT] Admin-flagged candidates: [{Ids}]", string.Join(',', adminIds));
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
                // First time we see this role, take candidate
                byRole[roleKey] = c;
                logger.LogInformation("[SELECT] Added to byRole: StudentId={StudentId}, RoleId={RoleId}, RoleType={RoleType}, RoleName={RoleName}, IsAdmin={IsAdmin}",
                    c.Id, roleKey, c.RoleType?.ToString() ?? "NULL", c.RoleName ?? "NULL", c.IsAdmin);
            }
            else
            {
                // Role already taken. If current candidate is admin and existing is not, prefer admin
                var existing = byRole[roleKey];
                if (c.IsAdmin && !existing.IsAdmin)
                {
                    logger.LogInformation("[SELECT] Replacing in byRole (preferring admin): Old StudentId={OldId}, New StudentId={NewId}, RoleId={RoleId}",
                        existing.Id, c.Id, roleKey);
                    byRole[roleKey] = c;
                }
                else
                {
                    logger.LogInformation("[SELECT] Skipping candidate StudentId={StudentId}: RoleId={RoleId} already taken by StudentId={ExistingId}",
                        c.Id, roleKey, existing.Id);
                }
            }
            if (c.IsAdmin)
            {
                if (admin == null) admin = c; // track one admin reference; exact count computed below
            }
            
            // Check if we can stop early, but only if all required roles are satisfied
            var hasMinimumStudents = byRole.Count >= cfg.MinimumStudents;
            var hasRequiredUIUX = !cfg.RequireUIUXDesigner || byRole.Values.Count(x => x.RoleType == 3) == 1;
            var hasRequiredProductManager = !cfg.RequireProductManager || byRole.Values.Count(x => x.RoleType == 4) == 1;
            
            if (hasMinimumStudents && hasRequiredUIUX && hasRequiredProductManager)
            {
                logger.LogInformation("[SELECT] Stopping early: byRole.Count={Count} >= MinimumStudents={Min} AND required roles satisfied", byRole.Count, cfg.MinimumStudents);
                break;
            }
            else if (hasMinimumStudents && (!hasRequiredUIUX || !hasRequiredProductManager))
            {
                logger.LogInformation("[SELECT] Continuing: byRole.Count={Count} >= MinimumStudents={Min} but required roles not yet satisfied (UI/UX={UIUX}, PM={PM}), continuing search...", 
                    byRole.Count, cfg.MinimumStudents, hasRequiredUIUX, hasRequiredProductManager);
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
            if (pmCount == 0)
            {
                logger.LogInformation("[SELECT] Product Manager required but not in group. Attempting to add Product Manager candidate...");
                var pmCandidate = candidates.FirstOrDefault(x => x.RoleType == 4 && x.RoleId != null);
                if (pmCandidate != null)
                {
                    var pmRoleId = pmCandidate.RoleId!.Value;
                    if (byRole.ContainsKey(pmRoleId))
                    {
                        logger.LogWarning("[SELECT] WARNING: Product Manager RoleId={RoleId} already in group but RoleType mismatch!", pmRoleId);
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
                    logger.LogInformation("[SELECT] No Product Manager candidate found in candidates list to add");
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
        var adminCount = group.Count(x => x.IsAdmin);
        if (cfg.RequireAdmin && adminCount != 1)
        {
            // Try to enforce exactly one admin by replacing a same-role non-admin with an admin from candidates
            var anyAdmin = candidates.FirstOrDefault(x => x.IsAdmin && x.RoleId != null);
            if (anyAdmin != null)
            {
                var key = anyAdmin.RoleId!.Value;
                if (byRole.ContainsKey(key))
                {
                    // Replace existing (even if already admin we will recompute)
                    byRole[key] = anyAdmin;
                }
                else
                {
                    // If role not in group, add and possibly evict a non-admin duplicate role to keep size
                    byRole[key] = anyAdmin;
                }
                group = byRole.Values.ToList();
                adminCount = group.Count(x => x.IsAdmin);
            }

            if (adminCount != 1)
            {
                logger.LogInformation("[SELECT] Rejected: admin count {AdminCount} (require exactly 1)", adminCount);
                return null;
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
        if (cfg.RequireDeveloperRule)
        {
            // Rule: 
            // - If exactly one Fullstack (RoleType=1) exists, there must be NO Frontend and NO Backend developers
            // - If NO Fullstack exists, there must be BOTH Frontend (RoleType=2, name contains "Frontend") 
            //   AND Backend (RoleType=2, name contains "Backend")
            var fullstackCount = group.Count(x => x.RoleType == 1);
            var hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
            var hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

            // If Fullstack exists, automatically remove Frontend and Backend developers
            if (fullstackCount == 1 && (hasFrontend || hasBackend))
            {
                logger.LogInformation("[SELECT] Fullstack developer found - removing Frontend and Backend developers from group");
                
                // Remove Frontend and Backend developers from byRole dictionary
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
                    logger.LogInformation("[SELECT] Removed from group: StudentId={StudentId}, RoleId={RoleId}, RoleName={RoleName} (redundant with Fullstack)",
                        removed.Id, roleId, removed.RoleName);
                }
                
                // Update group after removal
                group = byRole.Values.ToList();
                
                // Re-check counts after removal
                fullstackCount = group.Count(x => x.RoleType == 1);
                hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
                hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));
                
                logger.LogInformation("[SELECT] After removal: fullstackCount={Fullstack}, hasFrontend={FE}, hasBackend={BE}, groupCount={Count}",
                    fullstackCount, hasFrontend, hasBackend, group.Count);
                
                // Re-check minimum students after removal
                if (group.Count < cfg.MinimumStudents)
                {
                    logger.LogInformation("[SELECT] Rejected: not enough unique-role students after developer rule cleanup (have {Have}, need {Need})", group.Count, cfg.MinimumStudents);
                    return null;
                }
            }
            
            // Now validate the developer rule
            bool satisfies;
            if (fullstackCount == 1)
            {
                // If we have exactly one Fullstack, we should NOT have Frontend or Backend
                satisfies = !hasFrontend && !hasBackend;
                if (!satisfies)
                {
                    logger.LogInformation("[SELECT] Rejected: developer rule violated - Fullstack exists but also has Frontend={FE} or Backend={BE} (Fullstack should be alone)", hasFrontend, hasBackend);
                }
            }
            else if (fullstackCount == 0)
            {
                // If we have NO Fullstack, we must have BOTH Frontend AND Backend
                satisfies = hasFrontend && hasBackend;
                if (!satisfies)
                {
                    logger.LogInformation("[SELECT] Rejected: developer rule not satisfied - No Fullstack but missing Frontend={FE} or Backend={BE} (need both)", hasFrontend, hasBackend);
                }
            }
            else
            {
                // More than one Fullstack is invalid
                satisfies = false;
                logger.LogInformation("[SELECT] Rejected: developer rule violated - Multiple Fullstack developers ({Count})", fullstackCount);
            }

            if (!satisfies)
            {
                logger.LogInformation("[SELECT] Rejected: developer rule not satisfied (fullstackCount={Fullstack}, hasFrontend={FE}, hasBackend={BE})", fullstackCount, hasFrontend, hasBackend);
                return null;
            }
        }

        // Ensure exactly one admin if required
        if (cfg.RequireAdmin)
        {
            // If none admin in selected roles but there is one in candidates, swap first to admin
            if (adminCount == 0)
            {
                var anyAdmin = candidates.FirstOrDefault(x => x.IsAdmin && x.RoleId != null);
                if (anyAdmin != null)
                {
                    if (!byRole.ContainsKey(anyAdmin.RoleId!.Value))
                    {
                        // replace first entry
                        var firstKey = byRole.Keys.First();
                        byRole[firstKey] = anyAdmin;
                        group = byRole.Values.ToList();
                    }
                }
            }
            // Recheck
            if (group.Count(x => x.IsAdmin) != 1)
            {
                logger.LogInformation("[SELECT] Rejected after admin swap attempt: admin count still != 1");
                return null;
            }
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
}

public record ProjectInfo
{
    public int Id { get; init; }
    public DateTime CreatedAt { get; init; }
}


