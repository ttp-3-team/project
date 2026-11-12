using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Schema;
using System.Text.Json.Serialization.Metadata;

namespace ArchLens.Domain.Model;

/// <summary>JSON Schema (draft 2020-12) of <see cref="ModelSnapshot"/>, exported from the Domain types themselves.</summary>
public static class ModelSchema
{
    /// <summary>The schema as indented JSON text, deterministic for the same Domain types.</summary>
    public static string Export()
    {
        var options = new JsonSerializerOptions(SnapshotJson.Options) { TypeInfoResolver = new DefaultJsonTypeInfoResolver() };
        var exporter = new JsonSchemaExporterOptions
        {
            TreatNullObliviousAsNonNullable = true,
            TransformSchemaNode = (context, node) =>
            {
                if (context.PropertyInfo?.Name == "id" && node is JsonObject id && context.PropertyInfo.DeclaringType != typeof(ModelSnapshot))
                    id["pattern"] = SnapshotValidator.IdPatternText;
                return node;
            },
        };
        var schema = options.GetJsonSchemaAsNode(typeof(ModelSnapshot), exporter).AsObject();
        schema.Insert(0, "$schema", "https://json-schema.org/draft/2020-12/schema");
        schema.Insert(1, "$id", "https://archlens.dev/schemas/model.schema.json");
        schema.Insert(2, "title", "ModelSnapshot");
        schema.Insert(3, "description", "An immutable snapshot of a C4 model. Generated from ArchLens.Domain; do not edit by hand.");
        return schema.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = SnapshotJson.Options.Encoder }) + "\n";
    }
}
