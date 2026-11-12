using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArchLens.Domain.Model;

/// <summary>C4 element kind. Hierarchy: person and softwareSystem are top level, container is inside a softwareSystem, component inside a container.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ElementKind>))]
public enum ElementKind
{
    [JsonStringEnumMemberName("person")] Person,
    [JsonStringEnumMemberName("softwareSystem")] SoftwareSystem,
    [JsonStringEnumMemberName("container")] Container,
    [JsonStringEnumMemberName("component")] Component,
}

/// <summary>Wire names and labels of element kinds (one place for every enum-to-string mapping).</summary>
public static class ElementKinds
{
    /// <summary>The JSON name of a kind (same as its <see cref="JsonStringEnumMemberNameAttribute"/>).</summary>
    public static string Wire(ElementKind kind) => JsonNamingPolicy.CamelCase.ConvertName(kind.ToString());

    /// <summary>Label of a kind, as shown to people.</summary>
    public static string Label(ElementKind kind) => kind switch
    {
        ElementKind.Person => "person",
        ElementKind.SoftwareSystem => "software system",
        ElementKind.Container => "container",
        ElementKind.Component => "component",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}

/// <summary>A C4 element. <see cref="Id"/> is stable for the element's whole life; renaming never changes it.</summary>
public sealed record Element
{
    public required string Id { get; init; }
    public required ElementKind Kind { get; init; }
    public required string Name { get; init; }
    public string Description { get; init; } = "";
    public string Technology { get; init; } = "";
    public IReadOnlyList<string> Tags { get; init; } = [];
    public string? ParentId { get; init; }
    public bool External { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Properties { get; init; }

    /// <summary>Value of a free-form property, or null.</summary>
    public string? Property(string key) => Properties is not null && Properties.TryGetValue(key, out var v) ? v : null;
}

/// <summary>An explicit relationship between two elements. Implied relationships are derived in views, never stored.</summary>
public sealed record Relationship
{
    public required string Id { get; init; }
    public required string SourceId { get; init; }
    public required string TargetId { get; init; }
    public string Description { get; init; } = "";
    public string Technology { get; init; } = "";
}

/// <summary>Canvas position of an element in one view.</summary>
public sealed record Point(double X, double Y);

/// <summary>An immutable snapshot of a model. Layout is presentation only and is ignored by the diff.</summary>
public sealed record ModelSnapshot
{
    public const int CurrentSchemaVersion = 1;

    [JsonRequired] public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonRequired] public IReadOnlyList<Element> Elements { get; init; } = [];
    [JsonRequired] public IReadOnlyList<Relationship> Relationships { get; init; } = [];

    /// <summary>View key → element id → position.</summary>
    [JsonRequired]
    public IReadOnlyDictionary<string, IReadOnlyDictionary<string, Point>> Layout { get; init; } =
        new Dictionary<string, IReadOnlyDictionary<string, Point>>();

    /// <summary>Replaces nulls that JSON input may carry with the documented defaults.</summary>
    public ModelSnapshot Normalize() => this with
    {
        Elements = (Elements ?? []).Select(e => e with
        {
            Description = e.Description ?? "",
            Technology = e.Technology ?? "",
            Tags = e.Tags ?? [],
            ParentId = string.IsNullOrEmpty(e.ParentId) ? null : e.ParentId,
        }).ToList(),
        Relationships = (Relationships ?? []).Select(r => r with
        {
            Description = r.Description ?? "",
            Technology = r.Technology ?? "",
        }).ToList(),
        Layout = Layout ?? new Dictionary<string, IReadOnlyDictionary<string, Point>>(),
    };
}
