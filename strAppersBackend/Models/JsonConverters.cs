using System.Text.Json;
using System.Text.Json.Serialization;

namespace strAppersBackend.Models;

public class CleanTextJsonConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var value = reader.GetString() ?? string.Empty;
            
            // Clean up common text issues
            return value
                .Replace("\r\n", " ")  // Replace Windows line breaks
                .Replace("\n", " ")    // Replace Unix line breaks
                .Replace("\r", " ")    // Replace Mac line breaks
                .Replace("\t", " ")    // Replace tabs
                .Replace("  ", " ")    // Replace double spaces
                .Trim();               // Remove leading/trailing whitespace
        }
        
        return string.Empty;
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}
