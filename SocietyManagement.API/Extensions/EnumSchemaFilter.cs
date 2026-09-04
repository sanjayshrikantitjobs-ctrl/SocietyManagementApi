using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace SocietyManagement.API.Extensions;

/// <summary>Swashbuckle serializes enums in the OpenAPI schema as bare
/// integers by default — no member names anywhere. Harmless for the
/// Angular app (it hand-declares its own matching numeric union types), but
/// it means NSwag (SocietyManagement.Mobile's client generator) has nothing
/// to name its generated C# enum members from, and falls back to "_1",
/// "_2", etc. Adding the "x-enum-varnames" extension — an NJsonSchema/NSwag
/// convention, ignored by everything else that reads this spec — is enough
/// for NSwag to generate the real enum names instead.</summary>
public class EnumSchemaFilter : ISchemaFilter
{
    public void Apply(OpenApiSchema schema, SchemaFilterContext context)
    {
        if (!context.Type.IsEnum) return;

        var names = new OpenApiArray();
        foreach (var name in Enum.GetNames(context.Type))
        {
            names.Add(new OpenApiString(name));
        }
        // "x-enumNames" (NOT the more commonly-seen "x-enum-varnames") is
        // NJsonSchema's own convention — NSwag's client generator only reads
        // this exact key.
        schema.Extensions["x-enumNames"] = names;
    }
}
