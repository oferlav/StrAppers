namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for Project Engagement Rules text content
    /// </summary>
    public class EngagementRulesConfig
    {
        public string Title { get; set; } = "Project Engagement Rules";
        public string Description { get; set; } = "These rules govern how juniors can participate in projects and when projects can begin (kickoff). This platform is powered by advanced AI technology for intelligent project management.";
        public string Summary { get; set; } = "This AI-powered platform ensures fair and organized project participation while maintaining quality standards for project delivery. All technical infrastructure is automatically provisioned using intelligent automation.";
        
        public Dictionary<string, RuleSection> Sections { get; set; } = new();
    }
    
    /// <summary>
    /// Configuration for a single rule section
    /// </summary>
    public class RuleSection
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<string> Rules { get; set; } = new();
    }
}






