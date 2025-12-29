# Mentor Code Review Agent - Implementation Plan

## Use Case Flow

1. **User asks to review code** → AI checks GitHub repo → No commits/diffs found → Inform user
2. **User asks for help with GitHub process** → AI provides step-by-step guidance
3. **User commits code** → User asks to review again → AI fetches diffs → Performs code review

## Architecture Overview

### Components Needed

1. **Intent Detection Service**
   - Detect "review code" requests
   - Detect "help with GitHub" requests
   - Detect other code-related intents

2. **GitHub Diff/Commit Service**
   - Fetch recent commits by user
   - Get commit diffs/changes
   - Compare commits (before/after)

3. **Code Review Agent**
   - Analyze code changes
   - Compare against module design
   - Compare against Trello task description
   - Generate review feedback

4. **Mentor Controller Integration**
   - Route intents to appropriate handlers
   - Maintain conversation context
   - Return appropriate responses

## Implementation Steps

### Phase 1: GitHub Diff Service

**New Methods in `IGitHubService`:**

```csharp
Task<List<GitHubCommit>> GetRecentCommitsAsync(string owner, string repo, string username, int count = 10, string? accessToken = null);
Task<GitHubCommitDiff?> GetCommitDiffAsync(string owner, string repo, string commitSha, string? accessToken = null);
Task<List<GitHubFileChange>> GetFileChangesAsync(string owner, string repo, string commitSha, string? accessToken = null);
Task<bool> HasRecentCommitsAsync(string owner, string repo, string username, int hours = 24, string? accessToken = null);
```

**Models:**

```csharp
public class GitHubCommit
{
    public string Sha { get; set; }
    public string Message { get; set; }
    public DateTime CommitDate { get; set; }
    public string Author { get; set; }
    public string Url { get; set; }
}

public class GitHubFileChange
{
    public string FilePath { get; set; }
    public string Status { get; set; } // "added", "modified", "removed"
    public int Additions { get; set; }
    public int Deletions { get; set; }
    public string? Patch { get; set; } // Unified diff format
    public string? Content { get; set; } // Full file content (if needed)
}

public class GitHubCommitDiff
{
    public string CommitSha { get; set; }
    public string CommitMessage { get; set; }
    public DateTime CommitDate { get; set; }
    public List<GitHubFileChange> FileChanges { get; set; }
    public int TotalAdditions { get; set; }
    public int TotalDeletions { get; set; }
}
```

### Phase 2: Intent Detection

**New Service: `MentorIntentService`**

```csharp
public class MentorIntent
{
    public string Type { get; set; } // "code_review", "github_help", "general"
    public double Confidence { get; set; }
    public Dictionary<string, object>? Parameters { get; set; }
}

public interface IMentorIntentService
{
    Task<MentorIntent> DetectIntentAsync(string userMessage);
}
```

**Intent Detection Logic:**
- Use keyword matching + simple pattern recognition
- Patterns:
  - Code review: "review", "check code", "look at my code", "code review"
  - GitHub help: "github", "commit", "push", "how to", "git help"
  - General: fallback

### Phase 3: Code Review Agent

**New Service: `CodeReviewAgent`**

```csharp
public interface ICodeReviewAgent
{
    Task<CodeReviewResult> ReviewCodeAsync(
        GitHubCommitDiff commitDiff,
        string? moduleDescription,
        string? taskDescription,
        string programmingLanguage);
}

public class CodeReviewResult
{
    public bool Success { get; set; }
    public string ReviewText { get; set; }
    public List<CodeReviewIssue> Issues { get; set; }
    public List<CodeReviewSuggestion> Suggestions { get; set; }
    public double AlignmentScore { get; set; } // 0-1, how well code matches requirements
}

public class CodeReviewIssue
{
    public string FilePath { get; set; }
    public int? LineNumber { get; set; }
    public string Severity { get; set; } // "error", "warning", "info"
    public string Message { get; set; }
    public string Category { get; set; } // "requirements", "quality", "best_practices"
}

public class CodeReviewSuggestion
{
    public string FilePath { get; set; }
    public string Suggestion { get; set; }
    public string Reason { get; set; }
}
```

**Code Review Process:**
1. Fetch commit diff
2. Extract changed files and code
3. Fetch module description from DB (using ModuleId from Trello card)
4. Fetch task description from Trello card
5. Call AI with prompt:
   - System: "You are a code reviewer..."
   - User: Code diff + Module design + Task description
   - AI analyzes and provides feedback

### Phase 4: Mentor Controller Integration

**Modify `GetMentorResponse` method:**

```csharp
[HttpPost("use/respond/{aiModelName}")]
public async Task<ActionResult<object>> GetMentorResponse(...)
{
    // 1. Detect intent
    var intent = await _intentService.DetectIntentAsync(request.UserQuestion);
    
    // 2. Route based on intent
    switch (intent.Type)
    {
        case "code_review":
            return await HandleCodeReviewIntent(request, intent, contextResult);
        case "github_help":
            return await HandleGitHubHelpIntent(request, intent, contextResult);
        default:
            return await HandleGeneralMentorResponse(request, intent, contextResult);
    }
}
```

**New Handler Methods:**

```csharp
private async Task<ActionResult<object>> HandleCodeReviewIntent(...)
{
    // 1. Get student's GitHub info
    // 2. Check if repo has commits
    // 3. If no commits: return helpful message
    // 4. If commits exist: fetch diffs and perform review
}

private async Task<ActionResult<object>> HandleGitHubHelpIntent(...)
{
    // Provide step-by-step GitHub workflow guidance
}
```

## Database Considerations

- No new tables needed initially
- Use existing:
  - `Students` table (has `GithubUser`, `BoardId`)
  - `ProjectBoards` table (has `GithubUrl`)
  - `ProjectModules` table (has `Description`)
  - Trello API for task descriptions

## API Endpoints

### New Endpoints (Optional - for direct access):

```
GET /api/Mentor/use/code-review-status/{studentId}/{sprintId}
POST /api/Mentor/use/review-code/{studentId}/{sprintId}
```

## Configuration

Add to `appsettings.json`:

```json
"Mentor": {
  "CodeReview": {
    "MaxCommitsToReview": 5,
    "HoursSinceLastCommit": 24,
    "ReviewPrompt": "..."
  }
}
```

## Testing Strategy

1. **Unit Tests:**
   - Intent detection accuracy
   - GitHub API integration
   - Code review prompt construction

2. **Integration Tests:**
   - Full flow: no commits → help → commits → review
   - Mock GitHub API responses
   - Mock AI responses

3. **Manual Testing:**
   - Test with real GitHub repos
   - Test with different commit scenarios
   - Test with missing module/task info

## Implementation Order

1. ✅ Plan (this document)
2. ⬜ Phase 1: GitHub Diff Service
3. ⬜ Phase 2: Intent Detection
4. ⬜ Phase 3: Code Review Agent
5. ⬜ Phase 4: Mentor Integration
6. ⬜ Testing
7. ⬜ Documentation

## Future Enhancements

- Support for multiple commits comparison
- Line-by-line code review
- Integration with code quality tools (SonarQube, etc.)
- Support for different programming languages
- Code review history tracking
- Automated suggestions for improvements

