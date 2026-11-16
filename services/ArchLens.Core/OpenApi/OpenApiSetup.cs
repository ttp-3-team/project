using ArchLens.Domain.Model;
using Microsoft.OpenApi;

namespace ArchLens.Core.OpenApi;

/// <summary>
/// OpenAPI generated from the code (the C# records are the contract). Transformers make the document precise:
/// required members are marked required, element/relationship ids carry the opaque-id pattern, and every
/// authenticated operation lists its 401.
/// Enums are strings because every enum declares a string converter.
/// </summary>
public static class OpenApiSetup
{
    public static IServiceCollection AddArchLensOpenApi(this IServiceCollection services) =>
        services.AddOpenApi("v1", o =>
        {
            o.OpenApiVersion = OpenApiSpecVersion.OpenApi3_1;
            o.AddDocumentTransformer((doc, _, _) =>
            {
                doc.Info = new OpenApiInfo
                {
                    Title = "ArchLens Core API",
                    Version = "1.0.0",
                    Description = "Generated from ArchLens Core's code, the single source of truth. Every endpoint except /health needs a JWT minted by the ArchLens web BFF.",
                };
                return Task.CompletedTask;
            });
            o.AddOperationTransformer((operation, context, _) =>
            {
                if (context.Description.RelativePath?.StartsWith("api/v1", StringComparison.Ordinal) == true)
                    (operation.Responses ??= new OpenApiResponses()).TryAdd("401", new OpenApiResponse { Description = "Missing or invalid BFF token" });
                return Task.CompletedTask;
            });
            o.AddSchemaTransformer((schema, context, _) =>
            {
                if (schema is not OpenApiSchema s || s.Properties is null) return Task.CompletedTask;
                var type = context.JsonTypeInfo.Type;
                if ((type == typeof(Element) || type == typeof(Relationship)) && s.Properties.TryGetValue("id", out var id) && id is OpenApiSchema idSchema)
                    idSchema.Pattern = SnapshotValidator.IdPatternText;
                return Task.CompletedTask;
            });
        });
}
