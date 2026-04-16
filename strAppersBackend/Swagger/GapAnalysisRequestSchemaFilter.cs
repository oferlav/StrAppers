using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace strAppersBackend.Swagger;

/// <summary>
/// Ensures <c>test</c> on metric request DTOs is documented as default/example false (Swagger UI otherwise often shows true for booleans).
/// </summary>
public sealed class GapAnalysisRequestSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema.Properties == null)
            return;
        if (context.Type?.Name != "GapAnalysisRequest" && context.Type?.Name != "CustomerEngagementRequest")
            return;

        foreach (var key in schema.Properties.Keys)
        {
            if (!string.Equals(key, "test", StringComparison.OrdinalIgnoreCase))
                continue;
            var prop = schema.Properties[key];
            prop.Default = new OpenApiBoolean(false);
            prop.Example = new OpenApiBoolean(false);
            break;
        }
    }
}
