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

// Custom JSON converter to handle Inputs/Outputs as either string or object
public class FlexibleStringConverter : JsonConverter<string>
{
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        // Handle string directly
        if (reader.TokenType == JsonTokenType.String)
        {
            return reader.GetString() ?? string.Empty;
        }
        
        // Handle object - read entire object and convert to JSON string
        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                return doc.RootElement.GetRawText();
            }
        }
        
        // Handle array - read entire array and convert to JSON string
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            using (var doc = JsonDocument.ParseValue(ref reader))
            {
                return doc.RootElement.GetRawText();
            }
        }
        
        // Handle number
        if (reader.TokenType == JsonTokenType.Number)
        {
            if (reader.TryGetInt64(out var intValue))
            {
                return intValue.ToString();
            }
            return reader.GetDouble().ToString();
        }
        
        // Handle boolean
        if (reader.TokenType == JsonTokenType.True || reader.TokenType == JsonTokenType.False)
        {
            return reader.GetBoolean().ToString();
        }
        
        // Handle null
        if (reader.TokenType == JsonTokenType.Null)
        {
            return string.Empty;
        }
        
        // For any other type, try to read as string (this shouldn't happen but safe fallback)
        try
        {
            return reader.GetString() ?? string.Empty;
        }
        catch
        {
            // If we can't read as string, return empty
            return string.Empty;
        }
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
    
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert == typeof(string);
    }
}





