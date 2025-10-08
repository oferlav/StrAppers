namespace strAppersBackend.Models;

public class GoogleWorkspaceConfig
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string ServiceAccountEmail { get; set; } = string.Empty;
    public string ServiceAccountKeyPath { get; set; } = string.Empty;
    public List<string> Scopes { get; set; } = new List<string>();
}

