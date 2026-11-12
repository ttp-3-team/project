using System.Text.Json.Serialization;
using ArchLens.Domain.Model;
using ArchLens.Domain.Views;

namespace ArchLens.Domain.Diff;

/// <summary>Reference to an added or removed element.</summary>
public sealed record ElementRef(string Id, ElementKind Kind, string Name, string? ParentId);

/// <summary>One changed field; values rendered as strings (tags joined with ", ", null as "").</summary>
public sealed record FieldChange(string Field, string From, string To);

/// <summary>An element present in both models with at least one changed field.</summary>
public sealed record ElementModified(string Id, ElementKind Kind, string Name, string? ParentId, IReadOnlyList<FieldChange> Changes);

/// <summary>Reference to an added or removed relationship.</summary>
public sealed record RelationshipRef(string Id, string SourceId, string TargetId, string Description, string Technology);

/// <summary>A relationship present in both models with at least one changed field.</summary>
public sealed record RelationshipModified(string Id, string SourceId, string TargetId, string Description, string Technology, IReadOnlyList<FieldChange> Changes);

/// <summary>Element changes.</summary>
public sealed record ElementChanges(IReadOnlyList<ElementRef> Added, IReadOnlyList<ElementRef> Removed, IReadOnlyList<ElementModified> Modified);

/// <summary>Relationship changes.</summary>
public sealed record RelationshipChanges(IReadOnlyList<RelationshipRef> Added, IReadOnlyList<RelationshipRef> Removed, IReadOnlyList<RelationshipModified> Modified);

/// <summary>Status of a view between base and head.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ViewStatus>))]
public enum ViewStatus
{
    [JsonStringEnumMemberName("added")] Added,
    [JsonStringEnumMemberName("removed")] Removed,
    [JsonStringEnumMemberName("changed")] Changed,
    [JsonStringEnumMemberName("unchanged")] Unchanged,
}

/// <summary>How a view is affected by the change, with human-readable reasons.</summary>
public sealed record ViewImpact(string Key, ViewKind Kind, string Title, ViewStatus Status, bool Affected, IReadOnlyList<string> Reasons);

/// <summary>Counts of changes.</summary>
public sealed record DiffSummary(
    int ElementsAdded,
    int ElementsRemoved,
    int ElementsModified,
    int RelationshipsAdded,
    int RelationshipsRemoved,
    int RelationshipsModified);

/// <summary>Deterministic semantic diff of two model snapshots.</summary>
public sealed record DiffResult(ElementChanges Elements, RelationshipChanges Relationships, IReadOnlyList<ViewImpact> Views, DiffSummary Summary)
{
    [JsonIgnore]
    public bool IsEmpty => Summary is { ElementsAdded: 0, ElementsRemoved: 0, ElementsModified: 0, RelationshipsAdded: 0, RelationshipsRemoved: 0, RelationshipsModified: 0 };
}
