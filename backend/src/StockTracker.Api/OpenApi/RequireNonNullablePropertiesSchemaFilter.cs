using System.Reflection;
using System.Text.Json;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace StockTracker.Api.OpenApi;

/// <summary>
/// Marks non-nullable value type properties (bool, int, Guid, DateTime...) as required so the
/// generated TypeScript types are not optional. Reference types are handled by
/// NonNullableReferenceTypesAsRequired.
/// </summary>
public sealed class RequireNonNullablePropertiesSchemaFilter : ISchemaFilter
{
    public void Apply(IOpenApiSchema schema, SchemaFilterContext context)
    {
        if (schema is not OpenApiSchema concrete || concrete.Properties is null || concrete.Properties.Count == 0)
        {
            return;
        }

        var valueTypeProperties = context.Type
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType.IsValueType && Nullable.GetUnderlyingType(p.PropertyType) is null)
            .Select(p => JsonNamingPolicy.CamelCase.ConvertName(p.Name));

        foreach (var name in valueTypeProperties.Where(concrete.Properties.ContainsKey))
        {
            concrete.Required ??= new HashSet<string>();
            concrete.Required.Add(name);
        }
    }
}
