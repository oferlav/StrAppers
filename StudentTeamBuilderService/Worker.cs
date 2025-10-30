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

    public Worker(ILogger<Worker> logger, IHttpClientFactory httpClientFactory, IConfiguration configuration, IOptions<KickoffConfig> kickoffConfig)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _kickoffConfig = kickoffConfig.Value;
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
                await conn.ExecuteAsync(new CommandDefinition(
                    "UPDATE \"Students\" SET \"Status\"=2, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                    new { Ids = ids }, transaction: tx, cancellationToken: ct));

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
                        "UPDATE \"Students\" SET \"Status\"=1, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                        new { Ids = ids }, cancellationToken: ct));
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
                    "UPDATE \"Students\" SET \"Status\"=1, \"UpdatedAt\"=NOW() WHERE \"Id\" = ANY(@Ids)",
                    new { Ids = ids }, cancellationToken: ct));
            }
        }

        return 0;
    }

    private static DateTime NextDayNoonUtc()
    {
        var next = DateTime.UtcNow.Date.AddDays(1).AddHours(12);
        return next;
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
        var adminIds = candidates.Where(x => x.IsAdmin && x.RoleId != null).Select(x => x.Id).ToList();
        if (adminIds.Any())
        {
            logger.LogInformation("[SELECT] Admin-flagged candidates: [{Ids}]", string.Join(',', adminIds));
        }
        foreach (var c in candidates)
        {
            if (c.RoleId == null) continue;
            var roleKey = c.RoleId.Value;
            if (!byRole.ContainsKey(roleKey))
            {
                // First time we see this role, take candidate
                byRole[roleKey] = c;
            }
            else
            {
                // Role already taken. If current candidate is admin and existing is not, prefer admin
                var existing = byRole[roleKey];
                if (c.IsAdmin && !existing.IsAdmin)
                {
                    byRole[roleKey] = c;
                }
            }
            if (c.IsAdmin)
            {
                if (admin == null) admin = c; // track one admin reference; exact count computed below
            }
            if (byRole.Count >= cfg.MinimumStudents) break;
        }

        var group = byRole.Values.ToList();

        if (group.Count < cfg.MinimumStudents)
        {
            logger.LogInformation("[SELECT] Rejected: not enough unique-role students (have {Have}, need {Need})", group.Count, cfg.MinimumStudents);
            return null;
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

        // Enforce required roles
        if (cfg.RequireUIUXDesigner && !group.Any(x => x.RoleType == 3))
        {
            logger.LogInformation("[SELECT] Rejected: missing UI/UX role (RoleType=3)");
            return null;
        }
        if (cfg.RequireDeveloperRule)
        {
            // Rule: either exactly one Fullstack (RoleType=1)
            // OR one Frontend (RoleType=2, name contains "Frontend") AND one Backend (RoleType=2, name contains "Backend").
            var fullstackCount = group.Count(x => x.RoleType == 1);
            var hasFrontend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Frontend", StringComparison.OrdinalIgnoreCase));
            var hasBackend = group.Any(x => x.RoleType == 2 && (x.RoleName ?? string.Empty).Contains("Backend", StringComparison.OrdinalIgnoreCase));

            var satisfies = (fullstackCount == 1) || (hasFrontend && hasBackend);
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


