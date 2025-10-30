namespace strAppersBackend.Models;

public class AIConfig
{
    public string Model { get; set; } = "gpt-4o";
    public int MaxTokens { get; set; } = 12000;
    public double Temperature { get; set; } = 0.7;
}
