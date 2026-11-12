using System.Text.Json.Serialization;

namespace ArchLens.Domain.Views;

/// <summary>Kind of a derived C4 view.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ViewKind>))]
public enum ViewKind
{
    [JsonStringEnumMemberName("context")] Context,
    [JsonStringEnumMemberName("container")] Container,
    [JsonStringEnumMemberName("component")] Component,
}

/// <summary>
/// One edge drawn in a view. Several explicit relationships between descendants may lift to the same
/// (source, target) pair; <see cref="RelationshipIds"/> lists them. <see cref="Implied"/> is true when
/// none of them connects the drawn elements themselves (every one is lifted from descendants).
/// <see cref="Description"/> and <see cref="Technology"/> label the edge: they come from the relationship
/// that directly connects the drawn elements when there is one, otherwise from the first lifted one (by id).
/// </summary>
public sealed record ViewEdge(
    string SourceId,
    string TargetId,
    IReadOnlyList<string> RelationshipIds,
    bool Implied,
    string Description,
    string Technology)
{
    /// <summary>Stable edge id within a view: <c>source|target</c>.</summary>
    public string Key => $"{SourceId}|{TargetId}";
}

/// <summary>A view above the current one (for breadcrumbs). <c>Label</c> is short: "Context" or the scope's name.</summary>
public sealed record ViewAncestor(string Key, string Title, string Label);

/// <summary>A derived view: the elements drawn and the (possibly implied) edges between them.</summary>
public sealed record C4View(
    string Key,
    ViewKind Kind,
    string Title,
    string? ScopeId,
    IReadOnlyList<string> ElementIds,
    IReadOnlyList<ViewEdge> Edges)
{
    /// <summary>Whether the element belongs to the view's scope (otherwise it is a neighbour drawn as external).</summary>
    public bool Owns(Model.ModelIndex index, string id) => index.Get(id)?.ParentId == ScopeId;
}
