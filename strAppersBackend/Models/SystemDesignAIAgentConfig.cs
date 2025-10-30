namespace strAppersBackend.Models
{
    public class SystemDesignAIAgentConfig
    {
        public int MaxModules { get; set; } = 10;
        public int MinWordsPerModule { get; set; } = 100;
        public int DefaultSprintCount { get; set; } = 12;
    }
}
