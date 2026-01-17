namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for testing mode - allows skipping expensive API calls
    /// </summary>
    public class TestingConfig
    {
        /// <summary>
        /// Skip Trello API calls (default: false)
        /// When true, a mock board ID will be generated instead of calling Trello
        /// </summary>
        public bool SkipTrelloApi { get; set; } = false;

        /// <summary>
        /// Skip AI service calls (default: false)
        /// When true, a fallback sprint plan will be used instead of calling AI
        /// </summary>
        public bool SkipAIService { get; set; } = false;
    }
}
