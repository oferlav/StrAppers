namespace strAppersBackend.Models
{
    /// <summary>
    /// Configuration for AI prompts used in the application
    /// </summary>
    public class PromptConfig
    {
        public ProjectModulesPrompts ProjectModules { get; set; } = new();
        public MentorPrompts Mentor { get; set; } = new();
    }

    /// <summary>
    /// AI prompts specifically for Mentor chatbot functionality
    /// </summary>
    public class MentorPrompts
    {
        public string SystemPrompt { get; set; } = string.Empty;
        public string UserPromptTemplate { get; set; } = string.Empty;
        public int ChatHistoryLength { get; set; } = 5; // Number of previous messages to include in context
        public CodeReviewPrompts CodeReview { get; set; } = new();
        public EnhancedPromptPrompts EnhancedPrompt { get; set; } = new();
    }

    /// <summary>
    /// Code review prompt configuration
    /// </summary>
    public class CodeReviewPrompts
    {
        public int MaxCommitsToReview { get; set; } = 5;
        public int HoursSinceLastCommit { get; set; } = 168;
        public string ReviewSystemPrompt { get; set; } = string.Empty;
        public string ReviewUserPromptHeader { get; set; } = string.Empty;
        public string ReviewInstructions { get; set; } = string.Empty;
    }

    /// <summary>
    /// Enhanced prompt configuration for context-aware system prompts
    /// </summary>
    public class EnhancedPromptPrompts
    {
        public string DeveloperCapabilitiesInfo { get; set; } = string.Empty;
        public string NonDeveloperCapabilitiesInfo { get; set; } = string.Empty;
        public string ContextReminder { get; set; } = string.Empty;
        public string GitHubContextTemplate { get; set; } = string.Empty;
    }

    /// <summary>
    /// AI prompts specifically for ProjectModules functionality
    /// </summary>
    public class ProjectModulesPrompts
    {
        public InitiateModulesPrompt InitiateModules { get; set; } = new();
        public CreateDataModelPrompt CreateDataModel { get; set; } = new();
        public UpdateModulePrompt UpdateModule { get; set; } = new();
    }

    /// <summary>
    /// Prompt configuration for initiating modules
    /// </summary>
    public class InitiateModulesPrompt
    {
        public string SystemPrompt { get; set; } = @"You are an expert software architect. Your task is to analyze a project description and generate a comprehensive skeleton of modules/screens that would be needed for this project.

For each module, provide:
- Title: A clear, descriptive title for the module
- Description: A detailed description of what the module does, its purpose, and functionality
- Inputs: What data or parameters the module receives
- Outputs: What data or results the module produces

Format your response as JSON with the following structure:
{
  ""modules"": [
    {
      ""title"": ""Module Title"",
      ""description"": ""Detailed description of the module"",
      ""inputs"": ""What inputs this module receives"",
      ""outputs"": ""What outputs this module produces""
    }
  ]
}

Be comprehensive and think about all the different screens/modules a user would need to interact with this system.";

        public string UserPromptTemplate { get; set; } = @"Project Description: {0}

Please analyze this project and generate a skeleton of all the modules/screens that would be needed. Consider user interfaces, data processing modules, authentication, and any other components necessary for a complete system.";
    }

    /// <summary>
    /// Prompt configuration for creating data models
    /// </summary>
    public class CreateDataModelPrompt
    {
        public string SystemPrompt { get; set; } = @"You are an expert database architect. Your task is to analyze the project modules and generate a comprehensive SQL CREATE script for the entire database.

Based on the provided modules, create:
1. All necessary tables with appropriate data types
2. Primary keys and foreign key relationships
3. Indexes for performance
4. Constraints and validations
5. Comments explaining the purpose of each table and column

Format your response as a complete SQL script that can be executed directly. Include:
- CREATE TABLE statements
- PRIMARY KEY constraints
- FOREIGN KEY constraints
- INDEXES for performance
- Comments for documentation

Make sure the database design supports all the functionality described in the modules.";

        public string UserPromptTemplate { get; set; } = @"Based on the following project modules, create a comprehensive database schema:

{0}

Generate a complete SQL CREATE script for the entire database that supports all these modules.";
    }

    /// <summary>
    /// Prompt configuration for updating modules
    /// </summary>
    public class UpdateModulePrompt
    {
        public string SystemPrompt { get; set; } = @"You are an expert software architect and technical writer. Your task is to analyze a module description and user feedback to provide an improved, comprehensive description.

Guidelines:
1. Analyze the current module description
2. Consider the user's feedback and suggestions
3. Provide a detailed, comprehensive description that addresses the feedback
4. Make the description clear, technical, and actionable
5. Include specific details about functionality, inputs, outputs, and implementation considerations
6. End your response with: ""Is this what you meant? Please let me know if you need any adjustments.""

Your response should be professional, detailed, and ready to be used as a module specification.";

        public string UserPromptTemplate { get; set; } = @"Current Module Description:
{0}

User Feedback:
{1}

Please analyze the current description and user feedback, then provide an improved, comprehensive module description that addresses the feedback and provides more detail.";
    }
}






