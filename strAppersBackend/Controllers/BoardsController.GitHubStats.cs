using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using strAppersBackend.Models;

namespace strAppersBackend.Controllers;

public sealed class GitHubBranchStatDto
{
    public string RepoKind { get; set; } = "";
    public string Branch { get; set; } = "";
    public int Commits { get; set; }
    public int OpenPullRequests { get; set; }
    public int MergedPullRequests { get; set; }
}

public sealed class GitHubStatsResponseDto
{
    public bool Success { get; set; }
    public string BoardId { get; set; } = "";
    /// <summary>frontend | backend | both — effective repo scope after role/student resolution.</summary>
    public string Scope { get; set; } = "both";
    public int TotalCommits { get; set; }
    public int TotalOpenPullRequests { get; set; }
    public int TotalMergedPullRequests { get; set; }
    public List<GitHubBranchStatDto> BranchDetails { get; set; } = new();
    public List<string> Messages { get; set; } = new();
}

public partial class BoardsController
{
    private static readonly Regex RxFeSprintBranch = new(@"^(\d+-F|Bugs-F)(-\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex RxBeSprintBranch = new(@"^(\d+-B|Bugs-B)(-\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private sealed record GitHubRepoRef(string Kind, string Owner, string Repo);

    private sealed class GitStatTarget
    {
        public string RepoKind { get; init; } = "";
        public string Owner { get; init; } = "";
        public string Repo { get; init; } = "";
        public string Branch { get; init; } = "";
        public string Key => $"{Owner}/{Repo}\u001f{Branch}";
    }

    private enum ReposScope
    {
        Both,
        FrontendOnly,
        BackendOnly,
    }

    /// <summary>
    /// GitHub commit/PR stats for a board’s linked FE/BE repos. Optional filters: branch, sprint, role/student → repo scope.
    /// Route <c>github/stats</c> avoids <c>GET {{boardId}}</c> (e.g. <c>github-stats</c>) and <c>stats/{{boardId}}</c> (e.g. <c>stats/github</c>).
    /// GET /api/Boards/use/github/stats?boardId=...
    /// </summary>
    [HttpGet("github/stats")]
    public async Task<ActionResult<GitHubStatsResponseDto>> GetGitHubStats(
        [FromQuery] string boardId,
        [FromQuery] bool? isBackend,
        [FromQuery] string? branchName,
        [FromQuery] int? sprintNumber,
        [FromQuery] int? roleId,
        [FromQuery] int? studentId,
        CancellationToken cancellationToken)
    {
        var response = new GitHubStatsResponseDto { BoardId = boardId ?? "" };
        if (string.IsNullOrWhiteSpace(boardId))
        {
            response.Messages.Add("boardId is required.");
            return BadRequest(response);
        }

        var board = await _context.ProjectBoards.AsNoTracking()
            .FirstOrDefaultAsync(pb => pb.Id == boardId, cancellationToken);
        if (board == null)
        {
            response.Messages.Add("Project board not found.");
            return NotFound(response);
        }

        GitHubRepoRef? fe = ParseGitHubRepoUrl(board.GithubFrontendUrl, "frontend");
        GitHubRepoRef? be = ParseGitHubRepoUrl(board.GithubBackendUrl, "backend");
        if (fe == null && be == null)
        {
            response.Success = true;
            response.Messages.Add("No GitHubFrontendUrl or GithubBackendUrl on this board.");
            return Ok(response);
        }

        var scope = await ResolveReposScopeAsync(isBackend, roleId, studentId, boardId, cancellationToken);
        response.Scope = scope switch
        {
            ReposScope.FrontendOnly => "frontend",
            ReposScope.BackendOnly => "backend",
            _ => "both",
        };

        var targets = new List<GitStatTarget>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        void AddTarget(string kind, string owner, string repo, string branch)
        {
            var t = new GitStatTarget { RepoKind = kind, Owner = owner, Repo = repo, Branch = branch };
            if (seen.Add(t.Key))
                targets.Add(t);
        }

        var devSuffix = "";
        if (board.IsSingleRole && studentId is > 0)
        {
            var stu = await _context.Students.AsNoTracking()
                .Where(s => s.Id == studentId.Value && s.BoardId == boardId)
                .Select(s => new { s.Id, s.RoleIndex })
                .FirstOrDefaultAsync(cancellationToken);
            if (stu?.RoleIndex > 0)
                devSuffix = $"-{stu.RoleIndex}";
        }

        var hasExplicitBranch = !string.IsNullOrWhiteSpace(branchName);
        var bn = branchName?.Trim() ?? "";
        var sprint = sprintNumber is > 0 ? sprintNumber.Value : (int?)null;

        if (hasExplicitBranch)
        {
            foreach (var t in ResolveExplicitBranchTargets(fe, be, scope, bn))
                AddTarget(t.RepoKind, t.Owner, t.Repo, t.Branch);
        }
        else if (sprint != null)
        {
            AppendSprintTargets(fe, be, scope, sprint.Value, AddTarget, devSuffix);
        }
        else
        {
            await AppendAllSprintStyleBranchesAsync(fe, be, scope, AddTarget, cancellationToken);
        }

        if (sprint != null)
            AppendSprintBugBranches(fe, be, AddTarget, devSuffix);

        if (targets.Count == 0)
        {
            response.Messages.Add("No branches to query (check repo URLs and filters).");
            response.Success = true;
            return Ok(response);
        }

        var details = new List<GitHubBranchStatDto>(targets.Count);
        foreach (var t in targets)
        {
            var commits = await _gitHubService.CountCommitsOnBranchPagedAsync(t.Owner, t.Repo, t.Branch);
            var prs = await _gitHubService.CountPullRequestsForHeadBranchPagedAsync(t.Owner, t.Repo, t.Branch);
            details.Add(new GitHubBranchStatDto
            {
                RepoKind = t.RepoKind,
                Branch = t.Branch,
                Commits = commits,
                OpenPullRequests = prs.Open,
                MergedPullRequests = prs.Merged,
            });
            response.TotalCommits += commits;
            response.TotalOpenPullRequests += prs.Open;
            response.TotalMergedPullRequests += prs.Merged;
        }

        response.BranchDetails = details.OrderBy(d => d.RepoKind).ThenBy(d => d.Branch).ToList();
        response.Success = true;
        return Ok(response);
    }

    private static void AppendSprintTargets(
        GitHubRepoRef? fe,
        GitHubRepoRef? be,
        ReposScope scope,
        int sprintN,
        Action<string, string, string, string> add,
        string devSuffix = "")
    {
        var f = $"{sprintN}-F{devSuffix}";
        var b = $"{sprintN}-B{devSuffix}";
        switch (scope)
        {
            case ReposScope.FrontendOnly:
                if (fe != null) add(fe.Kind, fe.Owner, fe.Repo, f);
                break;
            case ReposScope.BackendOnly:
                if (be != null) add(be.Kind, be.Owner, be.Repo, b);
                break;
            default:
                if (fe != null) add(fe.Kind, fe.Owner, fe.Repo, f);
                if (be != null) add(be.Kind, be.Owner, be.Repo, b);
                break;
        }
    }

    /// <summary>Whenever <paramref name="sprintNumber"/> is set, include Bugs-F (FE repo) and Bugs-B (BE repo) if those repos exist.</summary>
    private static void AppendSprintBugBranches(
        GitHubRepoRef? fe,
        GitHubRepoRef? be,
        Action<string, string, string, string> add,
        string devSuffix = "")
    {
        if (fe != null) add(fe.Kind, fe.Owner, fe.Repo, $"Bugs-F{devSuffix}");
        if (be != null) add(be.Kind, be.Owner, be.Repo, $"Bugs-B{devSuffix}");
    }

    private IEnumerable<GitStatTarget> ResolveExplicitBranchTargets(
        GitHubRepoRef? fe,
        GitHubRepoRef? be,
        ReposScope scope,
        string branch)
    {
        var kind = InferRepoKindFromBranchName(branch);
        if (kind == "frontend" && fe != null)
        {
            yield return new GitStatTarget { RepoKind = fe.Kind, Owner = fe.Owner, Repo = fe.Repo, Branch = branch };
            yield break;
        }
        if (kind == "backend" && be != null)
        {
            yield return new GitStatTarget { RepoKind = be.Kind, Owner = be.Owner, Repo = be.Repo, Branch = branch };
            yield break;
        }

        switch (scope)
        {
            case ReposScope.FrontendOnly:
                if (fe != null)
                    yield return new GitStatTarget { RepoKind = fe.Kind, Owner = fe.Owner, Repo = fe.Repo, Branch = branch };
                break;
            case ReposScope.BackendOnly:
                if (be != null)
                    yield return new GitStatTarget { RepoKind = be.Kind, Owner = be.Owner, Repo = be.Repo, Branch = branch };
                break;
            default:
                if (fe != null)
                    yield return new GitStatTarget { RepoKind = fe.Kind, Owner = fe.Owner, Repo = fe.Repo, Branch = branch };
                if (be != null)
                    yield return new GitStatTarget { RepoKind = be.Kind, Owner = be.Owner, Repo = be.Repo, Branch = branch };
                break;
        }
    }

    /// <returns>"frontend", "backend", or null if ambiguous.</returns>
    private static string? InferRepoKindFromBranchName(string branch)
    {
        // Branch format: {sprint}-{letter} | {sprint}-{letter}-{idx} | Bugs-{letter} | Bugs-{letter}-{idx}
        // The role letter is always at index 1 after splitting on '-'.
        var parts = branch.Split('-');
        if (parts.Length < 2) return null;
        var letter = parts[1].Trim();
        if (letter.Equals("F", StringComparison.OrdinalIgnoreCase)) return "frontend";
        if (letter.Equals("B", StringComparison.OrdinalIgnoreCase)) return "backend";
        return null;
    }

    private async Task AppendAllSprintStyleBranchesAsync(
        GitHubRepoRef? fe,
        GitHubRepoRef? be,
        ReposScope scope,
        Action<string, string, string, string> add,
        CancellationToken cancellationToken)
    {
        if (fe != null && (scope == ReposScope.Both || scope == ReposScope.FrontendOnly))
        {
            var names = await _gitHubService.ListBranchNamesPagedAsync(fe.Owner, fe.Repo);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var n in names.Where(n => RxFeSprintBranch.IsMatch(n)))
                add(fe.Kind, fe.Owner, fe.Repo, n);
        }
        if (be != null && (scope == ReposScope.Both || scope == ReposScope.BackendOnly))
        {
            var names = await _gitHubService.ListBranchNamesPagedAsync(be.Owner, be.Repo);
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var n in names.Where(n => RxBeSprintBranch.IsMatch(n)))
                add(be.Kind, be.Owner, be.Repo, n);
        }
    }

    private async Task<ReposScope> ResolveReposScopeAsync(
        bool? isBackend,
        int? roleId,
        int? studentId,
        string boardId,
        CancellationToken cancellationToken)
    {
        if (isBackend == true)
            return ReposScope.BackendOnly;
        if (isBackend == false)
            return ReposScope.FrontendOnly;

        if (roleId is > 0)
        {
            var role = await _context.Roles.AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == roleId.Value, cancellationToken);
            return MapRoleToScope(role?.Name);
        }

        if (studentId is > 0)
        {
            var student = await _context.Students.AsNoTracking()
                .Include(s => s.StudentRoles)
                .ThenInclude(sr => sr.Role)
                .FirstOrDefaultAsync(s => s.Id == studentId.Value && s.BoardId == boardId, cancellationToken);
            if (student == null)
                return ReposScope.Both;
            return InferScopeFromStudentRoles(student);
        }

        return ReposScope.Both;
    }

    private static ReposScope MapRoleToScope(string? roleName)
    {
        var inferred = InferIsBackendFromRoleName(roleName);
        if (inferred == true)
            return ReposScope.BackendOnly;
        if (inferred == false)
            return ReposScope.FrontendOnly;
        return ReposScope.Both;
    }

    private static ReposScope InferScopeFromStudentRoles(Student student)
    {
        var active = student.StudentRoles.Where(sr => sr.IsActive && sr.Role != null).Select(sr => sr.Role!).ToList();
        if (active.Count == 0)
            return ReposScope.Both;

        var flags = active.Select(r => InferIsBackendFromRoleName(r.Name)).ToList();
        if (flags.Any(f => f == null))
            return ReposScope.Both;

        var distinct = flags.Distinct().ToList();
        if (distinct.Count > 1)
            return ReposScope.Both;
        return distinct[0] == true ? ReposScope.BackendOnly : ReposScope.FrontendOnly;
    }

    /// <summary>true = backend-only, false = frontend-only, null = fullstack / unknown → both repos.</summary>
    private static bool? InferIsBackendFromRoleName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return null;
        var n = name.Trim().ToLowerInvariant();
        if (n.Contains("fullstack", StringComparison.Ordinal) || n.Contains("full stack", StringComparison.Ordinal) || n.Contains("full-stack", StringComparison.Ordinal))
            return null;
        if (n.Contains("backend", StringComparison.Ordinal) || n.Contains("back-end", StringComparison.Ordinal) || n == "be")
            return true;
        if (n.Contains("frontend", StringComparison.Ordinal) || n.Contains("front-end", StringComparison.Ordinal) || n == "fe")
            return false;
        return null;
    }

    private static GitHubRepoRef? ParseGitHubRepoUrl(string? url, string kind)
    {
        if (string.IsNullOrWhiteSpace(url))
            return null;
        var u = url.Trim();
        if (!u.StartsWith("https://github.com/", StringComparison.OrdinalIgnoreCase) &&
            !u.StartsWith("http://github.com/", StringComparison.OrdinalIgnoreCase))
            return null;
        var path = u.Replace("https://github.com/", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://github.com/", "", StringComparison.OrdinalIgnoreCase)
            .TrimEnd('/').Trim();
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2)
            return null;
        return new GitHubRepoRef(kind, parts[0], parts[1]);
    }
}
