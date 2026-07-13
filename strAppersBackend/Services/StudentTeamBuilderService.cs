using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using strAppersBackend.Data;
using strAppersBackend.Models;
using System.Text;
using System.Text.Json;

namespace strAppersBackend.Services
{
    public class StudentTeamBuilderService : IStudentTeamBuilderService
    {
        private readonly ApplicationDbContext _context;
        private readonly ITrelloSprintMergeService _sprintMergeService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly ILogger<StudentTeamBuilderService> _logger;
        private readonly KickoffConfig _kickoffConfig;
        private readonly ISmtpEmailService _emailService;

        public StudentTeamBuilderService(
            ApplicationDbContext context,
            ITrelloSprintMergeService sprintMergeService,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<StudentTeamBuilderService> logger,
            IOptions<KickoffConfig> kickoffConfig,
            ISmtpEmailService emailService)
        {
            _context = context;
            _sprintMergeService = sprintMergeService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _logger = logger;
            _kickoffConfig = kickoffConfig.Value;
            _emailService = emailService;
        }

        /// <inheritdoc />
        public async Task<(int MergedCount, int ErrorCount, IReadOnlyList<string> Errors)> RunDueSprintMergesAsync()
        {
            var errors = new List<string>();
            var mergedCount = 0;
            var errorCount = 0;

            var uniqueBoardIds = await _context.Students
                .Where(s => s.Status == 3 && s.BoardId != null && s.BoardId != "")
                .Select(s => s.BoardId!)
                .Distinct()
                .ToListAsync();

            _logger.LogInformation("[STUDENT-TEAM-BUILDER] Found {Count} unique board(s) for students with status=3 and BoardId not null.", uniqueBoardIds.Count);

            foreach (var boardId in uniqueBoardIds)
            {
                var projectBoard = await _context.ProjectBoards
                    .AsNoTracking()
                    .FirstOrDefaultAsync(pb => pb.Id == boardId);
                if (projectBoard == null)
                {
                    _logger.LogWarning("[STUDENT-TEAM-BUILDER] BoardId={BoardId} has no ProjectBoard; skipping.", boardId);
                    continue;
                }
                var projectId = projectBoard.ProjectId;

                var nowUtc = DateTime.UtcNow;
                var allMerges = await _context.ProjectBoardSprintMerges
                    .Where(m => m.ProjectBoardId == boardId)
                    .OrderBy(m => m.SprintNumber)
                    .ToListAsync();
                var mergeBySprint = allMerges.ToDictionary(m => m.SprintNumber);
                var dueSprints = new List<strAppersBackend.Models.ProjectBoardSprintMerge>();
                foreach (var m in allMerges)
                {
                    if (m.MergedAt != null)
                        continue;
                    if (m.SprintNumber <= 1)
                        continue;
                    if (!mergeBySprint.TryGetValue(m.SprintNumber - 1, out var prev) || prev.DueDate == null || prev.DueDate.Value > nowUtc)
                        continue;
                    dueSprints.Add(m);
                }

                if (dueSprints.Count == 0)
                    continue;

                _logger.LogInformation("[STUDENT-TEAM-BUILDER] BoardId={BoardId} ProjectId={ProjectId}: {Count} overdue sprint(s) to merge.", boardId, projectId, dueSprints.Count);

                foreach (var mergeRow in dueSprints)
                {
                    var (success, error, _, listCreated) = await _sprintMergeService.ExecuteMergeSprintAsync(projectId, boardId, mergeRow.SprintNumber, merge: true);
                    if (success)
                    {
                        mergedCount++;
                        _logger.LogInformation("[STUDENT-TEAM-BUILDER] Merged BoardId={BoardId}, SprintNumber={SprintNumber}, ListCreated={ListCreated}.", boardId, mergeRow.SprintNumber, listCreated);
                        // Auto-assessment after merge was removed intentionally: assessments run
                        // on demand only, from the staff dashboard (POST /api/Metrics/use/run-student-sprint).
                    }
                    else
                    {
                        errorCount++;
                        var msg = $"BoardId={boardId}, SprintNumber={mergeRow.SprintNumber}: {error}";
                        errors.Add(msg);
                        _logger.LogWarning("[STUDENT-TEAM-BUILDER] {Message}", msg);
                    }
                }
            }

            _logger.LogInformation("[STUDENT-TEAM-BUILDER] RunDueSprintMerges completed. Merged: {Merged}, Errors: {Errors}.", mergedCount, errorCount);
            return (mergedCount, errorCount, errors);
        }

        /// <inheritdoc />
        public async Task<(int Created, int Skipped, IReadOnlyList<string> Messages)> RunInstituteTeamBuildingAsync()
        {
            var messages = new List<string>();
            var created = 0;
            var skipped = 0;
            var processedStudentIds = new HashSet<int>();

            // All institute students not yet on a board, with at least one priority set
            var eligibleStudents = await _context.Students
                .Include(s => s.StudentRoles).ThenInclude(sr => sr.Role)
                .Include(s => s.ProgrammingLanguage)
                .Where(s =>
                    s.InstituteId != null &&
                    s.BoardId == null &&
                    (s.Status == null || s.Status < 3) &&
                    (s.InstitutePriority1 != null || s.InstitutePriority2 != null ||
                     s.InstitutePriority3 != null || s.InstitutePriority4 != null))
                .ToListAsync();

            if (!eligibleStudents.Any())
            {
                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] No eligible institute students found.");
                return (0, 0, messages);
            }

            _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] Found {Count} eligible institute students.", eligibleStudents.Count);

            var byInstitute = eligibleStudents.GroupBy(s => s.InstituteId!.Value);

            foreach (var instituteGroup in byInstitute)
            {
                var instituteId = instituteGroup.Key;
                var instituteStudents = instituteGroup.ToList();

                var institute = await _context.Institutes.AsNoTracking()
                    .FirstOrDefaultAsync(i => i.Id == instituteId);
                var isSingleQuestMode = institute != null && institute.QuestMode && institute.SingleQuest;

                var activeTemplates = await _context.InstituteTemplates
                    .Include(t => t.Squad)
                        .ThenInclude(sq => sq.Roles)
                    .Where(t => t.InstituteId == instituteId && t.IsActive)
                    .ToListAsync();

                // Base roles for built-in project path: institute-specific first, fall back to global catalog
                var baseRoles = await _context.Roles
                    .Where(r => r.InstituteId == instituteId && r.SquadId == null && r.IsActive)
                    .ToListAsync();
                if (!baseRoles.Any())
                    baseRoles = await _context.Roles
                        .Where(r => r.InstituteId == null && r.IsActive)
                        .ToListAsync();

                // Check each priority level in order (highest priority first)
                Func<Student, int?>[] priorityAccessors = new Func<Student, int?>[]
                {
                    s => s.InstitutePriority1,
                    s => s.InstitutePriority2,
                    s => s.InstitutePriority3,
                    s => s.InstitutePriority4,
                };

                foreach (var getPriority in priorityAccessors)
                {
                    var studentsAtPriority = instituteStudents
                        .Where(s => getPriority(s) != null && !processedStudentIds.Contains(s.Id))
                        .ToList();

                    if (!studentsAtPriority.Any()) continue;

                    var byProject = studentsAtPriority.GroupBy(s => getPriority(s)!.Value);

                    foreach (var projectGroup in byProject)
                    {
                        var ipId = projectGroup.Key;
                        var candidates = projectGroup.ToList();

                        var template = activeTemplates.FirstOrDefault(t => t.InstituteProjectId == ipId);

                        var ip = await _context.InstituteProjects.FirstOrDefaultAsync(p => p.Id == ipId);
                        if (ip == null)
                        {
                            _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] Institute {InstituteId}, IpId {IpId}: InstituteProject not found, skipping.", instituteId, ipId);
                            skipped++;
                            continue;
                        }
                        if (!ip.BaseProjectId.HasValue)
                        {
                            _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] Institute {InstituteId}, IpId {IpId}: no BaseProjectId, skipping.", instituteId, ipId);
                            skipped++;
                            continue;
                        }

                        // Partition candidates by coupon: students with a coupon only team with
                        // same-coupon students; students without a coupon form their own pool.
                        var couponGroups = candidates
                            .GroupBy(s => string.IsNullOrWhiteSpace(s.Coupon) ? (string?)null : s.Coupon)
                            .ToList();

                        foreach (var couponGroup in couponGroups)
                        {
                            var couponCandidates = couponGroup.ToList();
                            var couponLabel = couponGroup.Key ?? "(no coupon)";

                            // SingleQuest: QuestMode institute where each student gets their own board immediately
                            if (isSingleQuestMode)
                            {
                                var eligible = couponCandidates.Where(s => !processedStudentIds.Contains(s.Id)).ToList();
                                var batchStart = DateTime.UtcNow;
                                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] SingleQuest batch start — Institute={InstituteId}, IpId={IpId}, Coupon={Coupon}, Students={Count}, Time={Time:O}",
                                    instituteId, ipId, couponLabel, eligible.Count, batchStart);

                                var sem = new SemaphoreSlim(5);
                                var localMessages = new System.Collections.Concurrent.ConcurrentBag<(bool Success, int StudentId, string Msg, TimeSpan Elapsed)>();

                                await Task.WhenAll(eligible.Select(async student =>
                                {
                                    await sem.WaitAsync();
                                    var studentStart = DateTime.UtcNow;
                                    _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] SingleQuest provisioning start — StudentId={StudentId}, Time={Time:O}", student.Id, studentStart);
                                    try
                                    {
                                        var singleBoard = await CallCreateBoardAsync(ip.BaseProjectId.Value, ip.Id, new List<Student> { student }, false, ip.Title);
                                        var elapsed = DateTime.UtcNow - studentStart;
                                        var msg = singleBoard.Success
                                            ? $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel} (SingleQuest): board created for student {student.Id} in {elapsed.TotalSeconds:F1}s."
                                            : $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel} (SingleQuest): CreateBoard FAILED for student {student.Id} after {elapsed.TotalSeconds:F1}s — {singleBoard.Error}";
                                        _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] SingleQuest provisioning end — StudentId={StudentId}, Success={Success}, ElapsedSeconds={Elapsed:F1}",
                                            student.Id, singleBoard.Success, elapsed.TotalSeconds);
                                        localMessages.Add((singleBoard.Success, student.Id, msg, elapsed));
                                    }
                                    finally { sem.Release(); }
                                }));

                                var batchElapsed = DateTime.UtcNow - batchStart;
                                var successCount = localMessages.Count(r => r.Success);
                                var failCount = localMessages.Count(r => !r.Success);

                                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] SingleQuest batch end — Institute={InstituteId}, IpId={IpId}, Success={S}, Failed={F}, TotalSeconds={Elapsed:F1}",
                                    instituteId, ipId, successCount, failCount, batchElapsed.TotalSeconds);

                                foreach (var (success, studentId, msg, _) in localMessages)
                                {
                                    messages.Add(msg);
                                    if (success) { created++; processedStudentIds.Add(studentId); _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] {Message}", msg); }
                                    else { skipped++; _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] {Message}", msg); }
                                }

                                // Email timing report
                                try
                                {
                                    var adminEmail = _configuration["AdminNotificationEmail"] ?? "oferlav@gmail.com";
                                    var rows = localMessages
                                        .OrderBy(r => r.StudentId)
                                        .Select(r => $"  Student {r.StudentId}: {(r.Success ? "OK" : "FAILED")} — {r.Elapsed.TotalSeconds:F1}s  {(r.Success ? "" : r.Msg.Split('—').LastOrDefault()?.Trim())}");
                                    var body = $"SingleQuest Provisioning Report\n" +
                                               $"================================\n" +
                                               $"Institute:    {instituteId}\n" +
                                               $"Project:      {ip.Title} (IpId={ipId})\n" +
                                               $"Coupon:       {couponLabel}\n" +
                                               $"Started:      {batchStart:yyyy-MM-dd HH:mm:ss} UTC\n" +
                                               $"Total time:   {batchElapsed.TotalSeconds:F1}s\n" +
                                               $"Students:     {eligible.Count} total, {successCount} OK, {failCount} failed\n\n" +
                                               $"Per-student timings:\n" +
                                               string.Join("\n", rows);
                                    await _emailService.SendPlainEmailAsync(adminEmail, $"[SingleQuest] {successCount}/{eligible.Count} boards created — {batchElapsed.TotalSeconds:F1}s", body);
                                }
                                catch (Exception emailEx)
                                {
                                    _logger.LogWarning(emailEx, "[INSTITUTE-TEAM-BUILDER] SingleQuest: failed to send timing email (non-critical)");
                                }

                                continue;
                            }

                            if (template == null)
                            {
                                // No active template — handle as built-in project using institute base roles
                                if (!ip.IsAvailable)
                                {
                                    _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] Institute {InstituteId}, IpId {IpId}: no template and project not available, skipping.", instituteId, ipId);
                                    continue;
                                }
                                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] Institute {InstituteId}, IpId {IpId}, Coupon={Coupon}: no active template, attempting built-in team build.", instituteId, ipId, couponLabel);
                                var builtInTeam = TryBuildBuiltInTeam(couponCandidates, baseRoles, instituteId, ipId, ref skipped);
                                if (builtInTeam == null || builtInTeam.Count == 0)
                                    continue;

                                var builtInBoard = await CallCreateBoardAsync(ip.BaseProjectId.Value, ip.Id, builtInTeam, false, ip.Title);
                                if (builtInBoard.Success)
                                {
                                    created++;
                                    foreach (var s in builtInTeam) processedStudentIds.Add(s.Id);
                                    var msg = $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel} (built-in): board created for {builtInTeam.Count} student(s).";
                                    messages.Add(msg);
                                    _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] {Message}", msg);
                                }
                                else
                                {
                                    skipped++;
                                    var msg = $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel} (built-in): CreateBoard failed — {builtInBoard.Error}";
                                    messages.Add(msg);
                                    _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] {Message}", msg);
                                }
                                continue;
                            }

                            if (string.IsNullOrWhiteSpace(ip.TrelloBoardJson))
                            {
                                _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] Institute {InstituteId}, IpId {IpId}: TrelloBoardJson is empty, skipping.", instituteId, ipId);
                                skipped++;
                                continue;
                            }

                            List<Student>? team = null;
                            var isSingleRole = false;

                            if (string.Equals(template.CourseType, "Role", StringComparison.OrdinalIgnoreCase))
                            {
                                (team, isSingleRole) = TryBuildRoleTeam(couponCandidates, template, instituteId, ipId, ref skipped);
                            }
                            else
                            {
                                team = TryBuildSquadTeam(couponCandidates, template, instituteId, ipId, ref skipped);
                            }

                            if (team == null || team.Count == 0)
                                continue;

                            // Assign RoleIndex for role-based courses before calling CreateBoard
                            if (isSingleRole)
                            {
                                for (var i = 0; i < team.Count; i++)
                                {
                                    team[i].RoleIndex = i + 1;
                                    team[i].UpdatedAt = DateTime.UtcNow;
                                    _context.Entry(team[i]).Property(x => x.RoleIndex).IsModified = true;
                                }
                                try { await _context.SaveChangesAsync(); }
                                catch (Exception ex)
                                {
                                    _logger.LogError(ex, "[INSTITUTE-TEAM-BUILDER] Failed to save RoleIndex assignments for IpId {IpId}.", ipId);
                                    skipped++;
                                    continue;
                                }
                                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] Assigned RoleIndex 1..{Count} for IpId {IpId}.", team.Count, ipId);
                            }

                            var boardCreated = await CallCreateBoardAsync(ip.BaseProjectId.Value, ip.Id, team, isSingleRole, ip.Title);
                            if (boardCreated.Success)
                            {
                                created++;
                                foreach (var s in team) processedStudentIds.Add(s.Id);
                                var msg = $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel}: board created for {team.Count} student(s).";
                                messages.Add(msg);
                                _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] {Message}", msg);
                            }
                            else
                            {
                                skipped++;
                                var msg = $"Institute {instituteId}, IpId {ipId}, Coupon={couponLabel}: CreateBoard failed — {boardCreated.Error}";
                                messages.Add(msg);
                                _logger.LogWarning("[INSTITUTE-TEAM-BUILDER] {Message}", msg);
                                // Note: RoleIndex is NOT reverted on failure. The HTTP call to BoardsController
                                // can time out (Azure 230s request limit) while the board is still being created
                                // server-side. Reverting would clobber a valid assignment.
                            }
                        }
                    }
                }
            }

            _logger.LogInformation("[INSTITUTE-TEAM-BUILDER] Complete. Created: {Created}, Skipped: {Skipped}.", created, skipped);
            return (created, skipped, messages);
        }

        // ──────────────────────────────────────────────────────────────────
        // Private helpers
        // ──────────────────────────────────────────────────────────────────

        private (List<Student>? Team, bool IsSingleRole) TryBuildRoleTeam(
            List<Student> candidates,
            InstituteTemplate template,
            int instituteId,
            int ipId,
            ref int skipped)
        {
            var roleCount = template.RoleCount ?? 1;

            // Group candidates by their active role; all must share the same role for a Role-type course
            var byRole = candidates
                .Where(s => s.StudentRoles.Any(sr => sr.IsActive))
                .GroupBy(s => s.StudentRoles.First(sr => sr.IsActive).RoleId);

            foreach (var roleGroup in byRole)
            {
                var studentsWithRole = roleGroup.ToList();
                if (studentsWithRole.Count < roleCount)
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}, Role {RoleId}: {Have}/{Need} students, skipping.",
                        instituteId, ipId, roleGroup.Key, studentsWithRole.Count, roleCount);
                    skipped++;
                    continue;
                }

                // All team members must have a programming language set (required for BE provisioned setup)
                var withLang = studentsWithRole
                    .Where(s => s.ProgrammingLanguageId != null)
                    .Take(roleCount)
                    .ToList();

                if (withLang.Count < roleCount)
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: only {Have}/{Need} students have programming language, skipping.",
                        instituteId, ipId, withLang.Count, roleCount);
                    skipped++;
                    continue;
                }

                return (withLang, true);
            }

            return (null, false);
        }

        internal List<Student>? TestTryBuildSquadTeam(
            List<Student> candidates,
            InstituteTemplate template,
            int instituteId,
            int ipId) { var s = 0; return TryBuildSquadTeam(candidates, template, instituteId, ipId, ref s); }

        internal List<Student>? TestTryBuildBuiltInTeam(
            List<Student> candidates,
            List<Role> baseRoles,
            int instituteId,
            int ipId) { var s = 0; return TryBuildBuiltInTeam(candidates, baseRoles, instituteId, ipId, ref s); }

        /// <summary>
        /// Developer exclusivity rule (ported from the B2C Worker.SelectGroup): a team gets EITHER one
        /// full-stack (Type=1) OR the FE+BE pair (Type=2 × ≥2) — never both. When both sides have
        /// candidates, the side that waited longer (StartPendingAt) wins; ties keep the full-stack
        /// (same default as the worker). The losing side is returned to <paramref name="available"/>.
        /// Returns the seated developers, or null when the rule cannot be satisfied (skip the team).
        /// </summary>
        private List<Student>? ResolveDeveloperBundle(
            List<Student> available,
            List<Role> bundleRoles,
            int instituteId,
            int ipId)
        {
            var fullStack = new List<Student>();
            var pair = new List<Student>();
            foreach (var role in bundleRoles)
            {
                var matched = available.FirstOrDefault(s =>
                    s.StudentRoles.Any(sr =>
                        sr.IsActive &&
                        sr.Role != null &&
                        string.Equals(sr.Role.Name, role.Name, StringComparison.OrdinalIgnoreCase)));
                if (matched == null) continue;

                available.Remove(matched);
                if (role.Type == 1) fullStack.Add(matched);
                else pair.Add(matched);
            }

            // Both sides matched → resolve exclusively, never seat both.
            if (fullStack.Count > 0 && pair.Count > 0)
            {
                var now = DateTime.UtcNow;
                double Wait(Student s) => s.StartPendingAt.HasValue ? (now - s.StartPendingAt.Value).TotalHours : 0;
                var pairComplete = pair.Count >= 2;
                var preferPair = pairComplete && pair.Max(Wait) > fullStack.Max(Wait);

                if (preferPair)
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: developer conflict — FE+BE pair waited longer; keeping pair, returning full-stack ({Ids}) to pool.",
                        instituteId, ipId, string.Join(",", fullStack.Select(s => s.Id)));
                    available.AddRange(fullStack);
                    fullStack.Clear();
                }
                else
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: developer conflict — keeping full-stack, returning FE/BE ({Ids}) to pool.",
                        instituteId, ipId, string.Join(",", pair.Select(s => s.Id)));
                    available.AddRange(pair);
                    pair.Clear();
                }
            }

            var ruleMet = (fullStack.Count >= 1 && pair.Count == 0)
                       || (pair.Count >= 2 && fullStack.Count == 0);
            if (!ruleMet)
            {
                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: developer rule not met (FullStack={FS}, Pair={P}), skipping.",
                    instituteId, ipId, fullStack.Count, pair.Count);
                return null;
            }

            _logger.LogInformation(
                "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: developer rule met (FullStack={FS}, Pair={P}).",
                instituteId, ipId, fullStack.Count, pair.Count);
            return fullStack.Concat(pair).ToList();
        }

        private List<Student>? TryBuildSquadTeam(
            List<Student> candidates,
            InstituteTemplate template,
            int instituteId,
            int ipId,
            ref int skipped)
        {
            if (template.Squad == null || !template.Squad.Roles.Any())
            {
                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: squad has no active roles, skipping.",
                    instituteId, ipId);
                skipped++;
                return null;
            }

            var activeRoles = template.Squad.Roles.Where(r => r.IsActive).ToList();
            var team = new List<Student>();
            var available = candidates.ToList();

            // ── Type=3 (Required) and Type=4 (Leadership) ─────────────────
            // Both are mandatory: team cannot form without a matching candidate.
            var mandatoryRoles = activeRoles.Where(r => r.Type == 3 || r.Type == 4).ToList();
            foreach (var squadRole in mandatoryRoles)
            {
                var matched = available.FirstOrDefault(s =>
                    s.StudentRoles.Any(sr =>
                        sr.IsActive &&
                        sr.Role != null &&
                        string.Equals(sr.Role.Name, squadRole.Name, StringComparison.OrdinalIgnoreCase)));

                if (matched == null)
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: no candidate for mandatory squad role '{Role}' (Type={Type}), skipping.",
                        instituteId, ipId, squadRole.Name, squadRole.Type);
                    skipped++;
                    return null;
                }

                team.Add(matched);
                available.Remove(matched);
                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: matched mandatory role '{Role}' (Type={Type}) → student {StudentId}.",
                    instituteId, ipId, squadRole.Name, squadRole.Type, matched.Id);
            }

            // ── Type=1/2 (Bundle / developer) ─────────────────────────────
            // Only enforced when InstituteSquad.RequireDeveloperRule is true.
            // Rule (exclusive): ONE full-stack (Type=1) OR the FE+BE pair (Type=2 × ≥2) — never both.
            var bundleRoles = activeRoles.Where(r => r.Type == 1 || r.Type == 2).ToList();
            if (bundleRoles.Any())
            {
                if (template.Squad.RequireDeveloperRule)
                {
                    var seated = ResolveDeveloperBundle(available, bundleRoles, instituteId, ipId);
                    if (seated == null)
                    {
                        skipped++;
                        return null;
                    }
                    team.AddRange(seated);
                }
                else
                {
                    // RequireDeveloperRule=false: bundle roles are optional — add matched, skip unmatched
                    foreach (var squadRole in bundleRoles)
                    {
                        var matched = available.FirstOrDefault(s =>
                            s.StudentRoles.Any(sr =>
                                sr.IsActive &&
                                sr.Role != null &&
                                string.Equals(sr.Role.Name, squadRole.Name, StringComparison.OrdinalIgnoreCase)));

                        if (matched != null)
                        {
                            team.Add(matched);
                            available.Remove(matched);
                        }
                    }
                }
            }

            // ── Type=0 (Default) ───────────────────────────────────────────
            // Optional: add matched candidates, silently skip unmatched.
            var optionalRoles = activeRoles.Where(r => r.Type == 0).ToList();
            foreach (var squadRole in optionalRoles)
            {
                var matched = available.FirstOrDefault(s =>
                    s.StudentRoles.Any(sr =>
                        sr.IsActive &&
                        sr.Role != null &&
                        string.Equals(sr.Role.Name, squadRole.Name, StringComparison.OrdinalIgnoreCase)));

                if (matched != null)
                {
                    team.Add(matched);
                    available.Remove(matched);
                }
                else
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId}: no candidate for optional role '{Role}' (Type=0), continuing.",
                        instituteId, ipId, squadRole.Name);
                }
            }

            return team.Count > 0 ? team : null;
        }

        private List<Student>? TryBuildBuiltInTeam(
            List<Student> candidates,
            List<Role> baseRoles,
            int instituteId,
            int ipId,
            ref int skipped)
        {
            var team = new List<Student>();
            var available = candidates.ToList();

            // Type=3 (Required) and Type=4 (Team Lead) are mandatory
            var mandatoryRoles = baseRoles.Where(r => r.Type == 3 || r.Type == 4).ToList();
            foreach (var role in mandatoryRoles)
            {
                var matched = available.FirstOrDefault(s =>
                    s.StudentRoles.Any(sr =>
                        sr.IsActive &&
                        sr.Role != null &&
                        string.Equals(sr.Role.Name, role.Name, StringComparison.OrdinalIgnoreCase)));

                if (matched == null)
                {
                    _logger.LogInformation(
                        "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId} (built-in): no candidate for mandatory role '{Role}' (Type={Type}), skipping.",
                        instituteId, ipId, role.Name, role.Type);
                    skipped++;
                    return null;
                }

                team.Add(matched);
                available.Remove(matched);
                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId} (built-in): matched mandatory role '{Role}' (Type={Type}) → student {StudentId}.",
                    instituteId, ipId, role.Name, role.Type, matched.Id);
            }

            // Type=1/2 (developer bundle) — uses KickoffConfig.RequireDeveloperRule (no squad flag available)
            // Rule (exclusive): ONE full-stack (Type=1) OR the FE+BE pair (Type=2 × ≥2) — never both.
            var bundleRoles = baseRoles.Where(r => r.Type == 1 || r.Type == 2).ToList();
            if (bundleRoles.Any())
            {
                if (_kickoffConfig.RequireDeveloperRule)
                {
                    var seated = ResolveDeveloperBundle(available, bundleRoles, instituteId, ipId);
                    if (seated == null)
                    {
                        skipped++;
                        return null;
                    }
                    team.AddRange(seated);
                }
                else
                {
                    // Rule off: bundle roles behave like optional roles — seat every match.
                    foreach (var role in bundleRoles)
                    {
                        var matched = available.FirstOrDefault(s =>
                            s.StudentRoles.Any(sr =>
                                sr.IsActive &&
                                sr.Role != null &&
                                string.Equals(sr.Role.Name, role.Name, StringComparison.OrdinalIgnoreCase)));

                        if (matched != null)
                        {
                            team.Add(matched);
                            available.Remove(matched);
                        }
                    }
                }
            }

            // Type=0 (optional) — add matched, skip unmatched
            var optionalRoles = baseRoles.Where(r => r.Type == 0).ToList();
            foreach (var role in optionalRoles)
            {
                var matched = available.FirstOrDefault(s =>
                    s.StudentRoles.Any(sr =>
                        sr.IsActive &&
                        sr.Role != null &&
                        string.Equals(sr.Role.Name, role.Name, StringComparison.OrdinalIgnoreCase)));

                if (matched != null)
                {
                    team.Add(matched);
                    available.Remove(matched);
                }
            }

            if (team.Count < _kickoffConfig.MinimumStudents)
            {
                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] Institute {II}, IpId {IpId} (built-in): not enough students ({Have}/{Need}), skipping.",
                    instituteId, ipId, team.Count, _kickoffConfig.MinimumStudents);
                skipped++;
                return null;
            }

            return team;
        }

        private async Task<(bool Success, string? Error)> CallCreateBoardAsync(
            int baseProjectId,
            int instituteProjectId,
            List<Student> team,
            bool isSingleRole,
            string? title = null,
            int durationMinutes = 60)
        {
            var apiBaseUrl = (_configuration["ApiBaseUrl"] ?? "http://localhost:5000").TrimEnd('/');
            var endpoint = $"{apiBaseUrl}/api/boards/use/create";

            var payload = new
            {
                projectId = baseProjectId,
                studentIds = team.Select(s => s.Id).ToList(),
                instituteProjectId,
                isSingleRole,
                title,
                durationMinutes
            };

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var json = JsonSerializer.Serialize(payload);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation(
                    "[INSTITUTE-TEAM-BUILDER] POST {Endpoint} — ProjectId={Pid}, IpId={IpId}, Students=[{Ids}], IsSingleRole={Sr}",
                    endpoint, baseProjectId, instituteProjectId,
                    string.Join(",", team.Select(s => s.Id)), isSingleRole);

                var response = await client.PostAsync(endpoint, content);
                var body = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                    return (true, null);

                return (false, $"HTTP {(int)response.StatusCode}: {body}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[INSTITUTE-TEAM-BUILDER] HTTP call to {Endpoint} threw exception.", endpoint);
                return (false, ex.Message);
            }
        }
    }
}
