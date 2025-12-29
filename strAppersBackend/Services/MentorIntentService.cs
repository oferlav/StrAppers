using System.Text.RegularExpressions;

namespace strAppersBackend.Services;

/// <summary>
/// Intent detection result for mentor chatbot
/// </summary>
public class MentorIntent
{
    public string Type { get; set; } = "general"; // "code_review", "github_help", "general"
    public double Confidence { get; set; } = 0.0;
    public Dictionary<string, object>? Parameters { get; set; }
}

/// <summary>
/// Service for detecting user intent in mentor chatbot conversations
/// </summary>
public interface IMentorIntentService
{
    Task<MentorIntent> DetectIntentAsync(string userMessage);
}

public class MentorIntentService : IMentorIntentService
{
    private readonly ILogger<MentorIntentService> _logger;

    // Keywords and patterns for intent detection
    private static readonly string[] CodeReviewKeywords = {
        "review", "review my code", "check my code", "look at my code", "code review",
        "review code", "check code", "examine code", "analyze code", "review my changes",
        "check my changes", "review the code", "code feedback", "feedback on code",
        "what do you think about my code", "how is my code", "is my code good",
        "diff", "diffs", "see diffs", "see any diffs", "do you see diffs", "check diffs",
        "any diffs", "see changes", "check changes", "what changed", "show me changes",
        "see my changes", "check my diffs", "review my diffs", "look at diffs",
        "do you see any diffs in the repo", "are there any diffs", "any changes in repo",
        "review my", "check my repo", "look at my repo",
        "look at", "one commit before", "previous commit", "commit before", "earlier commit",
        "before that", "earlier", "other commit",
        "review this file", "review it", "review that", "please review", "can you review",
        "give feedback", "provide feedback", "feedback on", "review and give feedback",
        "review the file", "check the file", "review file", "check file"
    };

    private static readonly string[] GitHubHelpKeywords = {
        "github", "git", "commit", "push", "how to commit", "how to push",
        "git help", "github help", "how do i commit", "how do i push",
        "commit code", "push code", "git workflow", "github workflow",
        "set up git", "setup git", "git setup", "github setup",
        "how to use git", "how to use github", "git tutorial", "github tutorial",
        "instruct again", "tell me again", "show me again", "repeat", "can you instruct",
        "first github experience", "github basics", "get started with github"
    };

    public MentorIntentService(ILogger<MentorIntentService> logger)
    {
        _logger = logger;
    }

    public Task<MentorIntent> DetectIntentAsync(string userMessage)
    {
        if (string.IsNullOrWhiteSpace(userMessage))
        {
            return Task.FromResult(new MentorIntent { Type = "general", Confidence = 0.0 });
        }

        var normalizedMessage = userMessage.ToLowerInvariant().Trim();

        // Check for code review intent - lower threshold to catch more cases
        var codeReviewScore = CalculateIntentScore(normalizedMessage, CodeReviewKeywords);
        // Lower threshold to 0.3 to catch "review my code" more reliably
        if (codeReviewScore > 0.3)
        {
            _logger.LogDebug("Detected code_review intent with confidence {Confidence}", codeReviewScore);
            return Task.FromResult(new MentorIntent
            {
                Type = "code_review",
                Confidence = codeReviewScore,
                Parameters = new Dictionary<string, object>
                {
                    { "original_message", userMessage }
                }
            });
        }

        // Check for GitHub help intent
        var githubHelpScore = CalculateIntentScore(normalizedMessage, GitHubHelpKeywords);
        if (githubHelpScore > 0.5)
        {
            _logger.LogDebug("Detected github_help intent with confidence {Confidence}", githubHelpScore);
            return Task.FromResult(new MentorIntent
            {
                Type = "github_help",
                Confidence = githubHelpScore,
                Parameters = new Dictionary<string, object>
                {
                    { "original_message", userMessage }
                }
            });
        }

        // Default to general intent
        _logger.LogDebug("Detected general intent");
        return Task.FromResult(new MentorIntent
        {
            Type = "general",
            Confidence = 1.0 - Math.Max(codeReviewScore, githubHelpScore),
            Parameters = new Dictionary<string, object>
            {
                { "original_message", userMessage }
            }
        });
    }

    /// <summary>
    /// Calculates intent score based on keyword matching and patterns
    /// </summary>
    private double CalculateIntentScore(string normalizedMessage, string[] keywords)
    {
        if (string.IsNullOrWhiteSpace(normalizedMessage) || keywords == null || keywords.Length == 0)
        {
            return 0.0;
        }

        var score = 0.0;
        var matchedKeywords = 0;

        // Exact phrase matching (higher weight)
        foreach (var keyword in keywords)
        {
            if (normalizedMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                // Exact phrase match gets higher score
                if (normalizedMessage.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                {
                    score += 1.0;
                    matchedKeywords++;
                }
            }
        }

        // Word-level matching (lower weight)
        var words = normalizedMessage.Split(new[] { ' ', '\t', '\n', '\r', '.', ',', '!', '?' }, 
            StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var keyword in keywords)
        {
            var keywordWords = keyword.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var keywordWord in keywordWords)
            {
                if (words.Any(w => w.Equals(keywordWord, StringComparison.OrdinalIgnoreCase)))
                {
                    score += 0.3;
                    matchedKeywords++;
                    break; // Count each keyword only once
                }
            }
        }

        // Normalize score to 0-1 range
        // Higher score if more keywords matched or exact phrases found
        var normalizedScore = Math.Min(1.0, score / Math.Max(1, keywords.Length * 0.5));
        
        return normalizedScore;
    }
}
