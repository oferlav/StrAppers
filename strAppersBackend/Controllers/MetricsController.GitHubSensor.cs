using System.Text;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;
using strAppersBackend.Services;

namespace strAppersBackend.Controllers;

/// <summary>
/// The "Codebase &amp; GitHub" sensor of the Data Assessment Engine.
///
/// Provides direct evidence — the actual diff and branch metadata for the student's sprint branch —
/// rather than the single CI status string the sensor used to emit. See GitHub_Codebase_Sensor_Plan.md.
///
/// The critical design point is the resolution order in <see cref="FetchGitHubTrackEvidenceAsync"/>:
/// pull requests are looked up BEFORE the branch compare, because after a squash merge the compare
/// returns zero files (and the branch may be deleted entirely). Assessments normally run after a
/// sprint has ended, so a compare-first implementation would report an empty diff for every completed
/// sprint and the model would score zero for work that was actually delivered.
/// </summary>
public partial class MetricsController
{
    // ---------------------------------------------------------------------------------------------
    // Configuration
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Limits for the GitHub sensor, read from <c>GitHub:AssessmentSensor</c>. Defaults match the
    /// budget the gap-analysis GitHub evidence already runs with in production, so prompt sizes stay
    /// in a range known to work.
    /// </summary>
    internal sealed class GitHubSensorOptions
    {
        public bool Enabled { get; init; } = true;
        public int MaxFilesPerTrack { get; init; } = 20;
        public int DiffCharBudgetPerTrack { get; init; } = 12000;
        public int MaxCommitMessages { get; init; } = 30;
        public int CommitMessageMaxChars { get; init; } = 200;
        public int MaxPullRequestsListed { get; init; } = 10;
        public string BaseBranch { get; init; } = "main";
        public string[] FallbackBaseBranches { get; init; } = { "master", "develop" };
        public string[] ExcludedPathPatterns { get; init; } = DefaultExcludedPathPatterns;

        /// <summary>Base branches to try, in order, without duplicates.</summary>
        public IEnumerable<string> BasesInOrder() =>
            new[] { BaseBranch }.Concat(FallbackBaseBranches)
                .Where(b => !string.IsNullOrWhiteSpace(b))
                .Distinct(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Generated / vendored paths excluded from the diff before file selection. Lock files and build
    /// output otherwise dominate a student's diff and burn the character budget on machine-written
    /// code the student did not author.
    /// </summary>
    internal static readonly string[] DefaultExcludedPathPatterns =
    {
        "**/package-lock.json", "**/yarn.lock", "**/pnpm-lock.yaml",
        "**/*.min.js", "**/*.min.css",
        "**/dist/**", "**/build/**", "**/node_modules/**", "**/vendor/**",
        "**/bin/**", "**/obj/**",
        "**/*.png", "**/*.jpg", "**/*.jpeg", "**/*.gif", "**/*.svg", "**/*.ico", "**/*.pdf",
    };

    private GitHubSensorOptions ReadGitHubSensorOptions()
    {
        var section = _configuration.GetSection("GitHub:AssessmentSensor");
        var configured = section.Get<GitHubSensorOptions>();
        if (configured == null)
            return new GitHubSensorOptions();

        // A section that omits the arrays would bind them to empty, silently disabling exclusion and
        // leaving no fallback bases. Restore the defaults in that case.
        return new GitHubSensorOptions
        {
            Enabled = configured.Enabled,
            MaxFilesPerTrack = configured.MaxFilesPerTrack > 0 ? configured.MaxFilesPerTrack : 20,
            DiffCharBudgetPerTrack = configured.DiffCharBudgetPerTrack > 0 ? configured.DiffCharBudgetPerTrack : 12000,
            MaxCommitMessages = configured.MaxCommitMessages > 0 ? configured.MaxCommitMessages : 30,
            CommitMessageMaxChars = configured.CommitMessageMaxChars > 0 ? configured.CommitMessageMaxChars : 200,
            MaxPullRequestsListed = configured.MaxPullRequestsListed > 0 ? configured.MaxPullRequestsListed : 10,
            BaseBranch = string.IsNullOrWhiteSpace(configured.BaseBranch) ? "main" : configured.BaseBranch,
            FallbackBaseBranches = configured.FallbackBaseBranches is { Length: > 0 }
                ? configured.FallbackBaseBranches
                : new[] { "master", "develop" },
            ExcludedPathPatterns = configured.ExcludedPathPatterns is { Length: > 0 }
                ? configured.ExcludedPathPatterns
                : DefaultExcludedPathPatterns,
        };
    }

    // ---------------------------------------------------------------------------------------------
    // Track resolution (pure)
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Which repository tracks a student owns. A backend developer owns one, a full stack developer
    /// can own both, a non-developer owns none.
    /// </summary>
    internal readonly record struct GitHubTrackSelection(bool Backend, bool Frontend)
    {
        public bool Any => Backend || Frontend;
    }

    /// <summary>
    /// Resolves the repository tracks from the student's role, and — for full stack roles on
    /// single-role boards — the sprint card's CardId custom field, whose B/F letters scope a full
    /// stack student to one repo. Mirrors the scoping the Adherence and Gap Analysis metrics already
    /// apply, so the three metrics do not disagree about which repos a student owns.
    /// </summary>
    internal static GitHubTrackSelection ResolveGitHubTrackSelection(string? roleName, string? sprintCardIdValue)
    {
        var rn = roleName?.Trim() ?? string.Empty;
        if (rn.Length == 0 || !ContainsDeveloper(rn))
            return new GitHubTrackSelection(false, false);

        if (IsFullStackRole(rn))
        {
            if (!string.IsNullOrWhiteSpace(sprintCardIdValue))
            {
                var hasB = sprintCardIdValue.Contains('B', StringComparison.OrdinalIgnoreCase);
                var hasF = sprintCardIdValue.Contains('F', StringComparison.OrdinalIgnoreCase);
                if (hasB || hasF)
                    return new GitHubTrackSelection(hasB, hasF);
            }
            return new GitHubTrackSelection(true, true);
        }

        if (IsBackendDeveloperRole(rn)) return new GitHubTrackSelection(true, false);
        if (IsFrontendDeveloperRole(rn)) return new GitHubTrackSelection(false, true);

        // Generic "Developer" with no track in the name: check both, same as Adherence.
        return new GitHubTrackSelection(true, true);
    }

    /// <summary>
    /// The platform's sprint branch convention: "{sprint}-B" / "{sprint}-F", with a "-{roleIndex}"
    /// suffix on single-role boards, and "Bugs-B" / "Bugs-F" for sprint 0.
    /// </summary>
    internal static string BuildSprintBranchName(int sprintNumber, bool isBackend, int roleIndex)
    {
        var idxSuffix = roleIndex > 0 ? $"-{roleIndex}" : string.Empty;
        var letter = isBackend ? "B" : "F";
        return sprintNumber == 0 ? $"Bugs-{letter}{idxSuffix}" : $"{sprintNumber}-{letter}{idxSuffix}";
    }

    /// <summary>
    /// Matches a file path against the exclusion patterns. Supports the leading "**/" and trailing
    /// "/**" forms plus "*" wildcards used in the configured patterns — deliberately not a full glob
    /// engine, since the patterns are ours.
    /// </summary>
    internal static bool IsExcludedDiffPath(string filePath, IEnumerable<string> patterns)
    {
        if (string.IsNullOrWhiteSpace(filePath)) return false;
        var path = filePath.Replace('\\', '/');

        foreach (var raw in patterns)
        {
            var pattern = raw?.Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(pattern)) continue;

            // "**/dir/**" — the segment must appear anywhere in the path
            if (pattern.StartsWith("**/", StringComparison.Ordinal) && pattern.EndsWith("/**", StringComparison.Ordinal))
            {
                var segment = pattern[3..^3];
                if (segment.Length > 0 &&
                    (path.Contains($"/{segment}/", StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith($"{segment}/", StringComparison.OrdinalIgnoreCase)))
                    return true;
                continue;
            }

            // "**/name" or "**/*.ext" — match the file name only
            if (pattern.StartsWith("**/", StringComparison.Ordinal))
            {
                var fileName = path[(path.LastIndexOf('/') + 1)..];
                if (WildcardMatches(fileName, pattern[3..])) return true;
                continue;
            }

            if (WildcardMatches(path, pattern)) return true;
        }

        return false;
    }

    /// <summary>Case-insensitive match supporting "*" (any run of characters, including none).</summary>
    private static bool WildcardMatches(string value, string pattern)
    {
        if (!pattern.Contains('*', StringComparison.Ordinal))
            return string.Equals(value, pattern, StringComparison.OrdinalIgnoreCase);

        var parts = pattern.Split('*');
        var index = 0;
        for (var i = 0; i < parts.Length; i++)
        {
            var part = parts[i];
            if (part.Length == 0) continue;

            if (i == 0)
            {
                if (!value.StartsWith(part, StringComparison.OrdinalIgnoreCase)) return false;
                index = part.Length;
                continue;
            }

            var found = value.IndexOf(part, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0) return false;
            index = found + part.Length;
        }

        var last = parts[^1];
        return last.Length == 0 || value.EndsWith(last, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------------------------------
    // Evidence model
    // ---------------------------------------------------------------------------------------------

    /// <summary>Why a track has no usable evidence. Each value is a different score, not a synonym.</summary>
    internal enum GitHubEvidenceStatus
    {
        /// <summary>Evidence was retrieved (which may still mean "nothing was delivered").</summary>
        Observed,
        /// <summary>No repository URL is configured for this track — a platform gap, not a student outcome.</summary>
        NoRepositoryUrl,
        /// <summary>The configured repository URL could not be parsed — a platform gap.</summary>
        InvalidRepositoryUrl,
        /// <summary>No GitHub token configured — a platform gap.</summary>
        NoToken,
        /// <summary>The GitHub API call failed — a platform gap, never evidence of missing work.</summary>
        ApiError,
    }

    internal sealed class GitHubPullRequestSummary
    {
        public int Number { get; init; }
        public string State { get; init; } = "";
        public bool Merged { get; init; }
        public string Title { get; init; } = "";
    }

    internal sealed class GitHubTrackEvidence
    {
        public bool IsBackend { get; init; }
        public string TrackName => IsBackend ? "Backend" : "Frontend";
        public GitHubEvidenceStatus Status { get; set; } = GitHubEvidenceStatus.Observed;

        public string? Owner { get; set; }
        public string? Repo { get; set; }
        public string Branch { get; set; } = "";
        /// <summary>How the branch name was derived — the naming convention, or a Trello BranchContext override.</summary>
        public string BranchOrigin { get; set; } = "naming convention";
        public bool BranchExists { get; set; }

        /// <summary>The PRs we have detail for — the representative PR from the branch lookup.</summary>
        public List<GitHubPullRequestSummary> PullRequests { get; } = new();
        /// <summary>
        /// Total PRs ever opened for this branch, from the paged count. Can exceed
        /// <see cref="PullRequests"/> because only the most recent PR is detailed — a student who
        /// opened and closed several PRs for one sprint shows a higher total here.
        /// </summary>
        public int PullRequestCount { get; set; }
        public bool AnyMerged => PullRequests.Any(p => p.Merged);

        public int CommitCount { get; set; }
        public DateTime? FirstCommitDate { get; set; }
        public DateTime? LastCommitDate { get; set; }
        public List<GitHubCommit> Commits { get; } = new();

        public List<GitHubFileChange> Files { get; } = new();
        public int TotalAdditions { get; set; }
        public int TotalDeletions { get; set; }
        /// <summary>Where the diff came from — the merged PR's file list, or a branch compare.</summary>
        public string? DiffOrigin { get; set; }

        public int ExcludedFileCount { get; set; }
        public int ExcludedAdditions { get; set; }
        public int ExcludedDeletions { get; set; }

        public bool HasAnyDelivery => CommitCount > 0 || PullRequests.Count > 0 || Files.Count > 0;
    }

    // ---------------------------------------------------------------------------------------------
    // Evidence fetching
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Fetches metadata and diff for one repository track, PR-first (see the class remarks for why).
    /// Never throws: any failure is recorded on the evidence object so the renderer can distinguish
    /// "nothing delivered" from "the platform could not observe".
    /// </summary>
    private async Task<GitHubTrackEvidence> FetchGitHubTrackEvidenceAsync(
        string? repoUrl,
        string branch,
        string branchOrigin,
        bool isBackend,
        string? token,
        GitHubSensorOptions options,
        CancellationToken ct)
    {
        var evidence = new GitHubTrackEvidence
        {
            IsBackend = isBackend,
            Branch = branch,
            BranchOrigin = branchOrigin,
        };

        if (string.IsNullOrWhiteSpace(token))
        {
            evidence.Status = GitHubEvidenceStatus.NoToken;
            return evidence;
        }

        if (string.IsNullOrWhiteSpace(repoUrl))
        {
            evidence.Status = GitHubEvidenceStatus.NoRepositoryUrl;
            return evidence;
        }

        if (!TryParseOwnerRepo(repoUrl, out var owner, out var repo))
        {
            evidence.Status = GitHubEvidenceStatus.InvalidRepositoryUrl;
            return evidence;
        }

        evidence.Owner = owner;
        evidence.Repo = repo;

        try
        {
            // 1. Pull requests first — open, merged, and the client-side closed-PR scan, which is the
            //    only way to find a merged PR whose branch has since been deleted.
            var pr = await _githubService.GetPullRequestForGapAnalysisAsync(owner, repo, branch, token);
            ct.ThrowIfCancellationRequested();

            if (pr != null)
            {
                evidence.PullRequests.Add(new GitHubPullRequestSummary
                {
                    Number = pr.Number,
                    State = pr.State ?? "",
                    Merged = pr.Merged,
                    Title = pr.Title ?? "",
                });
            }

            var (openCount, mergedCount) = await _githubService.CountPullRequestsForHeadBranchPagedAsync(
                owner, repo, branch, accessToken: token);
            ct.ThrowIfCancellationRequested();
            var totalPrCount = openCount + mergedCount;

            // 2. The diff. A merged PR's own file list survives a squash merge; the branch compare
            //    does not. Prefer the compare only when it actually produced files.
            GitHubCommitDiff? compare = null;
            foreach (var b in options.BasesInOrder())
            {
                compare = await _githubService.GetCompareDiffAsync(owner, repo, b, branch, token);
                ct.ThrowIfCancellationRequested();
                if (compare != null) break;
            }

            evidence.BranchExists = compare != null;

            if (compare is { TotalFilesChanged: > 0 })
            {
                ApplyDiff(evidence, compare.FileChanges, "branch compare", options);
                evidence.CommitCount = compare.CommitsCount;
                AddCommits(evidence, compare.Commits, options);
            }
            else if (pr is { Merged: true, Number: > 0 })
            {
                var prDiff = await _githubService.GetPullRequestFilesAsync(owner, repo, pr.Number, token);
                ct.ThrowIfCancellationRequested();
                if (prDiff != null)
                    ApplyDiff(evidence, prDiff.FileChanges, $"merged PR #{pr.Number} file list", options);

                var prCommits = await _githubService.GetPullRequestCommitsAsync(owner, repo, pr.Number, token);
                ct.ThrowIfCancellationRequested();
                evidence.CommitCount = prCommits.Count;
                AddCommits(evidence, prCommits, options);
            }
            else if (compare != null)
            {
                // Branch exists but has no files ahead of the base (and nothing merged).
                evidence.CommitCount = compare.CommitsCount;
                AddCommits(evidence, compare.Commits, options);
            }

            // An open PR with no compare files is unusual but possible (e.g. the base moved). Fall back
            // to the PR's own files so an in-flight sprint still shows code.
            if (evidence.Files.Count == 0 && pr is { Merged: false, Number: > 0 })
            {
                var openPrDiff = await _githubService.GetPullRequestFilesAsync(owner, repo, pr.Number, token);
                ct.ThrowIfCancellationRequested();
                if (openPrDiff is { TotalFilesChanged: > 0 })
                    ApplyDiff(evidence, openPrDiff.FileChanges, $"open PR #{pr.Number} file list", options);
                if (evidence.CommitCount == 0)
                {
                    var openPrCommits = await _githubService.GetPullRequestCommitsAsync(owner, repo, pr.Number, token);
                    evidence.CommitCount = openPrCommits.Count;
                    AddCommits(evidence, openPrCommits, options);
                }
            }

            // The representative PR above is one row; the paged count is the real total.
            evidence.PullRequestCount = Math.Max(totalPrCount, evidence.PullRequests.Count);

            _logger.LogInformation(
                "GitHub sensor: {Owner}/{Repo} branch={Branch} prs={Prs} merged={Merged} commits={Commits} files={Files}",
                owner, repo, branch, totalPrCount, evidence.AnyMerged, evidence.CommitCount, evidence.Files.Count);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub sensor failed for {Owner}/{Repo} branch {Branch}", owner, repo, branch);
            evidence.Status = GitHubEvidenceStatus.ApiError;
        }

        return evidence;
    }

    /// <summary>
    /// Applies a file list to the evidence: drops generated paths (recording their totals separately),
    /// then keeps the largest remaining changes until the file count or character budget is reached.
    /// </summary>
    private static void ApplyDiff(
        GitHubTrackEvidence evidence,
        IEnumerable<GitHubFileChange> files,
        string origin,
        GitHubSensorOptions options)
    {
        evidence.DiffOrigin = origin;

        var kept = new List<GitHubFileChange>();
        foreach (var file in files)
        {
            if (IsExcludedDiffPath(file.FilePath, options.ExcludedPathPatterns))
            {
                evidence.ExcludedFileCount++;
                evidence.ExcludedAdditions += file.Additions;
                evidence.ExcludedDeletions += file.Deletions;
                continue;
            }
            kept.Add(file);
        }

        // Totals reflect everything the student authored, not just what fits in the budget.
        evidence.TotalAdditions = kept.Sum(f => f.Additions);
        evidence.TotalDeletions = kept.Sum(f => f.Deletions);

        var budget = options.DiffCharBudgetPerTrack;
        foreach (var file in kept.OrderByDescending(f => f.Additions + f.Deletions).ThenBy(f => f.FilePath))
        {
            if (evidence.Files.Count >= options.MaxFilesPerTrack) break;
            if (budget <= 0) break;

            var patchLength = file.Patch?.Length ?? 0;
            if (patchLength > budget)
            {
                file.Patch = Truncate(file.Patch ?? "", budget);
                budget = 0;
            }
            else
            {
                budget -= patchLength;
            }

            evidence.Files.Add(file);
        }
    }

    private static void AddCommits(GitHubTrackEvidence evidence, IReadOnlyList<GitHubCommit> commits, GitHubSensorOptions options)
    {
        if (commits.Count == 0) return;

        var dated = commits.Where(c => c.CommitDate != default).Select(c => c.CommitDate).ToList();
        if (dated.Count > 0)
        {
            evidence.FirstCommitDate = dated.Min();
            evidence.LastCommitDate = dated.Max();
        }

        foreach (var c in commits.Take(options.MaxCommitMessages))
            evidence.Commits.Add(c);
    }

    // ---------------------------------------------------------------------------------------------
    // Entry point
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Builds the whole "Code &amp; GitHub" section for one student and sprint: a combined summary,
    /// a sub-block per repository track, and the CI status sub-block. Replaces the board-state-only
    /// block the sensor used to emit.
    /// </summary>
    internal async Task AppendAssessmentCodeAndGitHubAsync(
        StringBuilder sb,
        string boardId,
        ProjectBoard board,
        Student student,
        int sprintNumber,
        string? trelloRoleLabel,
        bool sprintWindowOpen,
        CancellationToken ct)
    {
        sb.AppendLine("### Code & GitHub");

        var options = ReadGitHubSensorOptions();
        if (!options.Enabled)
        {
            sb.AppendLine("_(the GitHub evidence sensor is disabled by platform configuration — no code evidence was collected. This is a platform setting, not a statement about this student. Do not score code delivery from this section.)_");
            sb.AppendLine();
            return;
        }

        // Decision: Bugs-branch work (sprint 0) is not counted toward a numbered sprint.
        if (sprintNumber == 0)
        {
            sb.AppendLine("_(sprint 0 is the bug-fix track; it is deliberately excluded from sprint code assessment.)_");
            sb.AppendLine();
            return;
        }

        var roleName = student.StudentRoles?.FirstOrDefault(sr => sr.IsActive)?.Role?.Name?.Trim()
                       ?? trelloRoleLabel;

        // Full stack students on single-role boards can be scoped to one repo by the sprint card's
        // CardId custom field.
        string? cardIdValue = null;
        if (board.IsSingleRole && !string.IsNullOrWhiteSpace(trelloRoleLabel) && IsFullStackRole(roleName ?? ""))
        {
            try
            {
                cardIdValue = await _trelloService.GetSprintCardCustomFieldValueAsync(
                    boardId, sprintNumber, trelloRoleLabel, "CardId");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub sensor: CardId scope lookup failed for board {BoardId}", boardId);
            }
        }

        var selection = ResolveGitHubTrackSelection(roleName, cardIdValue);
        if (!selection.Any)
        {
            sb.AppendLine($"_This sensor measures code delivered to a sprint branch. This student's role is **{roleName ?? "(unknown)"}**, which has no repository or branch, so there is nothing here to measure. This is expected. Do not create a score category for it and do not reduce any score because this section is empty._");
            sb.AppendLine();
            return;
        }

        var roleIndex = board.IsSingleRole && student.RoleIndex > 0 ? student.RoleIndex : 0;
        var token = _configuration["GitHub:AccessToken"];

        // QuestMode: board-level URLs are null; per-student URLs live on QuestBoards.
        var backendUrl = board.GithubBackendUrl;
        var frontendUrl = board.GithubFrontendUrl;
        if (string.IsNullOrEmpty(backendUrl) && string.IsNullOrEmpty(frontendUrl) && student.Id > 0)
        {
            var qb = await _context.QuestBoards.AsNoTracking()
                .FirstOrDefaultAsync(q => q.BoardId == boardId && q.StudentId == student.Id, ct);
            if (qb != null) { backendUrl = qb.GithubBackendUrl; frontendUrl = qb.GithubFrontendUrl; }
        }

        // A BranchContext custom field on the sprint card overrides the naming convention.
        string? branchContext = null;
        if (!string.IsNullOrWhiteSpace(trelloRoleLabel))
        {
            try
            {
                branchContext = await _trelloService.GetSprintCardBranchContextAsync(boardId, sprintNumber, trelloRoleLabel);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GitHub sensor: BranchContext lookup failed for board {BoardId}", boardId);
            }
        }

        var tracks = new List<GitHubTrackEvidence>();
        foreach (var isBackend in new[] { true, false })
        {
            if (isBackend && !selection.Backend) continue;
            if (!isBackend && !selection.Frontend) continue;

            var defaultBranch = BuildSprintBranchName(sprintNumber, isBackend, roleIndex);
            var branch = defaultBranch;
            var origin = "naming convention";
            if (!string.IsNullOrWhiteSpace(branchContext))
            {
                branch = ResolveGapAnalysisHeadFromBranchContext(branchContext.Trim(), isBackend);
                origin = $"Trello BranchContext `{branchContext.Trim()}`";
            }

            var evidence = await FetchGitHubTrackEvidenceAsync(
                isBackend ? backendUrl : frontendUrl, branch, origin, isBackend, token, options, ct);

            // BranchContext often points at a differently-named branch than the one actually pushed to.
            // Retry the sprint default before concluding nothing was delivered.
            if (!evidence.HasAnyDelivery
                && evidence.Status == GitHubEvidenceStatus.Observed
                && !string.Equals(branch, defaultBranch, StringComparison.Ordinal))
            {
                var retry = await FetchGitHubTrackEvidenceAsync(
                    isBackend ? backendUrl : frontendUrl, defaultBranch, "naming convention (BranchContext branch was empty)",
                    isBackend, token, options, ct);
                if (retry.HasAnyDelivery) evidence = retry;
            }

            tracks.Add(evidence);
        }

        var sectionStart = sb.Length;

        AppendGitHubSummaryLine(sb, tracks);
        foreach (var track in tracks)
            AppendGitHubTrackBlock(sb, track, sprintWindowOpen, options);

        await AppendGitHubCiStatusAsync(sb, boardId, sprintNumber,
            tracks.Select(t => t.Branch).Where(b => !string.IsNullOrWhiteSpace(b)).ToList(), ct);

        if (DebugAiContext)
            await SendGitHubSensorDebugEmailAsync(
                boardId, student, sprintNumber, tracks, sb.ToString()[sectionStart..], ct);
    }

    /// <summary>
    /// Emails the sensor's raw GitHub diagnostics when Debug:AiContext is on, using the same pipeline
    /// as the assessment engine's prompt debug mail.
    ///
    /// This exists because the GitHub lookups swallow HTTP failures into null, so a 401, a 403, a
    /// rate-limit and a genuinely missing branch all render identically as "Nothing delivered". The
    /// probe reports the raw status of every endpoint the sensor depends on, plus the head refs of
    /// recent closed PRs — which is what identifies a merged PR the head filter failed to match.
    /// Never interrupts the assessment.
    /// </summary>
    private async Task SendGitHubSensorDebugEmailAsync(
        string boardId,
        Student student,
        int sprintNumber,
        IReadOnlyCollection<GitHubTrackEvidence> tracks,
        string renderedSection,
        CancellationToken ct)
    {
        try
        {
            var token = _configuration["GitHub:AccessToken"];
            var body = new StringBuilder();

            body.AppendLine("=== GitHub Sensor Debug ===");
            body.AppendLine($"Board:   {boardId}");
            body.AppendLine($"Student: {student.Id} ({student.FirstName} {student.LastName})");
            body.AppendLine($"Sprint:  {sprintNumber}");
            body.AppendLine($"Tracks:  {tracks.Count}");
            body.AppendLine();

            foreach (var track in tracks)
            {
                body.AppendLine($"--- {track.TrackName} track ---------------------------------------------");
                body.AppendLine($"Resolved status: {track.Status}");
                body.AppendLine($"Branch:          {track.Branch} (from {track.BranchOrigin})");
                body.AppendLine($"Sensor result:   commits={track.CommitCount} prs={track.PullRequestCount} " +
                                $"merged={track.AnyMerged} files={track.Files.Count} diffOrigin={track.DiffOrigin ?? "(none)"}");
                body.AppendLine();

                if (track.Owner != null && track.Repo != null)
                {
                    body.AppendLine("HTTP probe:");
                    var probe = await _githubService.DiagnoseBranchAccessAsync(track.Owner, track.Repo, track.Branch, token);
                    foreach (var line in probe) body.AppendLine("  " + line);
                }
                else
                {
                    body.AppendLine("HTTP probe skipped — no owner/repo resolved for this track.");
                }
                body.AppendLine();
            }

            body.AppendLine("--- Rendered section -------------------------------------------------");
            body.AppendLine(renderedSection);

            await _smtpEmailService.SendPlainEmailAsync(
                "ofer@skill-in.com",
                $"[GitHub Sensor Debug] Board {boardId} | Student {student.Id} | Sprint {sprintNumber}",
                body.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub sensor debug email failed (non-critical)");
        }
    }

    /// <summary>
    /// Scoring rules for the Code &amp; GitHub section, appended to the system prompt only when the
    /// sensor is enabled on the metric. Deliberately phrased neutrally so they suit any rubric — the
    /// gap-analysis GitHub wording is specific to implementation-completeness scoring and would
    /// corrupt a rubric about, say, code readability.
    /// </summary>
    internal static string BuildGitHubScoringRules() => """

            Rules for the "Code & GitHub" section:
            - A merged pull request means the work landed. An empty branch comparison after a merge is the
              normal result of a squash merge and is NOT evidence of missing work — score from the diff shown.
            - Where a track says "Could not observe", the platform failed to read the repository. That is not
              a student outcome: do not score that track and do not lower any score because of it.
            - Where the sprint window is still open, do not treat an unmerged branch as a failure to deliver.
            - Commit counts and dates are facts, not productivity measures — force-push and rebase rewrite them.
            - Generated files excluded from the diff are not the student's authored work; never score them.
            """;

    /// <summary>Combined one-line summary across all tracks, so the model sees the shape before the detail.</summary>
    internal static void AppendGitHubSummaryLine(StringBuilder sb, IReadOnlyCollection<GitHubTrackEvidence> tracks)
    {
        var observed = tracks.Where(t => t.Status == GitHubEvidenceStatus.Observed).ToList();
        if (observed.Count == 0)
        {
            sb.AppendLine("**Summary:** no repository could be observed for this student (see per-track detail below).");
            sb.AppendLine();
            return;
        }

        var commits = observed.Sum(t => t.CommitCount);
        var prs = observed.Sum(t => t.PullRequestCount);
        var files = observed.Sum(t => t.Files.Count);
        var adds = observed.Sum(t => t.TotalAdditions);
        var dels = observed.Sum(t => t.TotalDeletions);
        var merged = observed.Any(t => t.AnyMerged);

        sb.AppendLine($"**Summary across {observed.Count} repository track(s):** {commits} commit(s), {prs} pull request(s), "
                      + $"merged: {(merged ? "yes" : "no")}, {files} file(s) changed, +{adds}/-{dels}.");
        sb.AppendLine();
    }

    // ---------------------------------------------------------------------------------------------
    // Rendering
    // ---------------------------------------------------------------------------------------------

    /// <summary>
    /// One-line statement of what a track shows, in the vocabulary the scoring rules refer to. Each
    /// case is a different score: "could not observe" must never be read as "nothing was delivered".
    /// </summary>
    internal static string DescribeGitHubDeliveryState(GitHubTrackEvidence e, bool sprintWindowOpen)
    {
        switch (e.Status)
        {
            case GitHubEvidenceStatus.NoToken:
                return "**Could not observe** — no GitHub access token is configured on the platform. This is a platform limitation, NOT evidence about this student. Do not score this track.";
            case GitHubEvidenceStatus.NoRepositoryUrl:
                return "**Could not observe** — no repository is configured for this track on this board. This is a platform/setup gap, NOT evidence about this student. Do not score this track.";
            case GitHubEvidenceStatus.InvalidRepositoryUrl:
                return "**Could not observe** — the configured repository URL could not be parsed. This is a platform/setup gap, NOT evidence about this student. Do not score this track.";
            case GitHubEvidenceStatus.ApiError:
                return "**Could not observe** — the GitHub API call failed. This is a platform limitation, NOT evidence about this student. Do not score this track.";
        }

        if (e.AnyMerged)
            return "**Delivered** — a pull request for this branch was merged, so the work landed.";

        if (e.PullRequestCount > 0)
            return sprintWindowOpen
                ? "**Submitted, not yet merged** — a pull request is open and the sprint is still in progress. An unmerged branch is not a failure to deliver at this point."
                : "**Submitted, not merged** — a pull request was opened for this branch but nothing was merged.";

        if (e.CommitCount > 0 || e.Files.Count > 0)
            return sprintWindowOpen
                ? "**Work in progress** — there are commits on the branch and no pull request yet; the sprint is still in progress."
                : "**Never submitted for review** — there are commits on the branch but no pull request was ever opened.";

        if (!e.BranchExists)
            return "**Nothing delivered** — the sprint branch does not exist and no pull request was found.";

        return "**Nothing delivered** — the sprint branch exists but has no commits ahead of the base branch and no pull request.";
    }

    /// <summary>Renders one repository track: delivery state, metadata, commit messages, then the diff.</summary>
    private static void AppendGitHubTrackBlock(
        StringBuilder sb, GitHubTrackEvidence e, bool sprintWindowOpen, GitHubSensorOptions options)
    {
        sb.AppendLine($"#### {e.TrackName} repository");

        var repoLabel = e.Owner != null && e.Repo != null ? $"`{e.Owner}/{e.Repo}`" : "_(not configured)_";
        sb.AppendLine($"- Repository: {repoLabel}");
        sb.AppendLine($"- Branch: `{e.Branch}` (from {e.BranchOrigin})");
        sb.AppendLine($"- State: {DescribeGitHubDeliveryState(e, sprintWindowOpen)}");

        if (e.Status != GitHubEvidenceStatus.Observed)
        {
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"- Commits on this branch: {e.CommitCount}"
            + (e.FirstCommitDate.HasValue && e.LastCommitDate.HasValue
                ? $" (first {e.FirstCommitDate:yyyy-MM-dd HH:mm} UTC, last {e.LastCommitDate:yyyy-MM-dd HH:mm} UTC)"
                : ""));
        sb.AppendLine($"- Pull requests for this branch: {e.PullRequestCount}");

        foreach (var pr in e.PullRequests.Take(options.MaxPullRequestsListed))
            sb.AppendLine($"  - PR #{pr.Number}: state={pr.State}, merged={(pr.Merged ? "yes" : "no")}, title={pr.Title}");

        sb.AppendLine($"- Files changed: {e.Files.Count}, +{e.TotalAdditions}/-{e.TotalDeletions}"
            + (e.DiffOrigin != null ? $" (diff source: {e.DiffOrigin})" : ""));

        if (e.ExcludedFileCount > 0)
            sb.AppendLine($"- {e.ExcludedFileCount} generated file(s) excluded from the diff below (+{e.ExcludedAdditions}/-{e.ExcludedDeletions}) — lock files, build output and binary assets. These are not the student's authored work.");

        if (e.Commits.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Commit messages** (oldest first):");
            foreach (var c in e.Commits)
            {
                var firstLine = (c.Message ?? "").Split('\n')[0].Trim();
                sb.AppendLine($"- [{c.CommitDate:yyyy-MM-dd HH:mm}] {Truncate(firstLine, options.CommitMessageMaxChars)}");
            }
            if (e.CommitCount > e.Commits.Count)
                sb.AppendLine($"- _(+{e.CommitCount - e.Commits.Count} more commit(s) not listed)_");
        }

        if (e.Files.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("**Diff:**");
            foreach (var f in e.Files)
            {
                sb.AppendLine($"--- {f.FilePath} ({f.Status}, +{f.Additions}/-{f.Deletions})");
                sb.AppendLine("```diff");
                sb.AppendLine(string.IsNullOrWhiteSpace(f.Patch)
                    ? "(patch omitted by GitHub — file too large or binary; the +/- counts above are still accurate)"
                    : f.Patch);
                sb.AppendLine("```");
            }
        }

        sb.AppendLine();
    }

    /// <summary>
    /// CI status for this sprint, from the board-state table. Scoped by the resolved branch names
    /// rather than the derived Backend/Frontend field, whose value comes from testing whether the
    /// branch name contains the letter "b" and so misclassifies any descriptive branch name.
    /// </summary>
    internal async Task AppendGitHubCiStatusAsync(
        StringBuilder sb, string boardId, int sprintNumber, IReadOnlyCollection<string> branches, CancellationToken ct)
    {
        sb.AppendLine("#### CI Status (build & tests)");

        var rows = await _context.BoardStates.AsNoTracking()
            .Where(s => s.BoardId == boardId && s.SprintNumber == sprintNumber)
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync(ct);

        if (branches.Count > 0)
        {
            var scoped = rows
                .Where(s => !string.IsNullOrWhiteSpace(s.GithubBranch)
                            && branches.Contains(s.GithubBranch!, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (scoped.Count > 0) rows = scoped;
        }

        var withStatus = rows
            .Where(s => !string.IsNullOrWhiteSpace(s.LastBuildStatus) || !string.IsNullOrWhiteSpace(s.LastTestStatus))
            .ToList();

        if (withStatus.Count == 0)
        {
            sb.AppendLine("_(no build or test results recorded for this sprint)_");
            sb.AppendLine();
            return;
        }

        foreach (var s in withStatus)
        {
            var branch = !string.IsNullOrWhiteSpace(s.GithubBranch) ? s.GithubBranch : s.BranchName;
            sb.Append($"- Source: {s.Source} | Branch: {branch ?? "—"}");
            if (!string.IsNullOrWhiteSpace(s.LastBuildStatus)) sb.Append($" | Build: {s.LastBuildStatus}");
            if (!string.IsNullOrWhiteSpace(s.LastTestStatus)) sb.Append($" | Tests: {s.LastTestStatus}");
            if (s.LastTestRunDate.HasValue) sb.Append($" | Last run: {s.LastTestRunDate:yyyy-MM-dd HH:mm} UTC");
            sb.AppendLine();
        }

        sb.AppendLine();
    }
}
