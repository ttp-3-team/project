using ErrorOr;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace ArchLens.Domain.Model;

/// <summary>Canonical JSON settings shared by Core, the CLI and tests (camelCase, deterministic, readable Unicode).</summary>
public static class SnapshotJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        WriteIndented = false,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.Strict,
    };

    public static readonly JsonSerializerOptions Indented = new(Options) { WriteIndented = true };

    /// <summary>Parses snapshot JSON text. Returns the problem instead of throwing on malformed input.</summary>
    public static ErrorOr<ModelSnapshot> Parse(string json)
    {
        try
        {
            var snapshot = JsonSerializer.Deserialize<ModelSnapshot>(json, Options);
            return snapshot is null
                ? Problems.Of("empty JSON")
                : snapshot.Normalize();
        }
        catch (JsonException e)
        {
            return Problems.Of($"invalid JSON: {e.Message}");
        }
    }

    public static string Serialize<T>(T value, bool indented = false) =>
        JsonSerializer.Serialize(value, indented ? Indented : Options);
}
