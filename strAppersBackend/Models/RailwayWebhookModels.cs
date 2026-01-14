using System.Text.Json.Serialization;

namespace strAppersBackend.Models
{
    public class RailwayWebhookPayload
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;
        
        [JsonPropertyName("details")]
        public RailwayWebhookDetails? Details { get; set; }
        
        [JsonPropertyName("resource")]
        public RailwayWebhookResource? Resource { get; set; }
        
        [JsonPropertyName("severity")]
        public string? Severity { get; set; }
        
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }
    }

    public class RailwayWebhookDetails
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("source")]
        public string? Source { get; set; }
        
        [JsonPropertyName("status")]
        public string? Status { get; set; }
        
        [JsonPropertyName("branch")]
        public string? Branch { get; set; }
        
        [JsonPropertyName("commitHash")]
        public string? CommitHash { get; set; }
        
        [JsonPropertyName("commitAuthor")]
        public string? CommitAuthor { get; set; }
        
        [JsonPropertyName("commitMessage")]
        public string? CommitMessage { get; set; }
        
        // Additional fields that Railway might send
        [JsonPropertyName("message")]
        public string? Message { get; set; }
        
        [JsonPropertyName("error")]
        public string? Error { get; set; }
        
        [JsonPropertyName("errorMessage")]
        public string? ErrorMessage { get; set; }
        
        [JsonPropertyName("reason")]
        public string? Reason { get; set; }
    }

    public class RailwayWebhookResource
    {
        [JsonPropertyName("workspace")]
        public RailwayResource? Workspace { get; set; }
        
        [JsonPropertyName("project")]
        public RailwayResource? Project { get; set; }
        
        [JsonPropertyName("environment")]
        public RailwayEnvironment? Environment { get; set; }
        
        [JsonPropertyName("service")]
        public RailwayResource? Service { get; set; }
        
        [JsonPropertyName("deployment")]
        public RailwayResource? Deployment { get; set; }
    }

    public class RailwayResource
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
    }

    public class RailwayEnvironment
    {
        [JsonPropertyName("id")]
        public string? Id { get; set; }
        
        [JsonPropertyName("name")]
        public string? Name { get; set; }
        
        [JsonPropertyName("isEphemeral")]
        public bool? IsEphemeral { get; set; }
    }
}
