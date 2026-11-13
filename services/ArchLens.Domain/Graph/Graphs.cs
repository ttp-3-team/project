using System.Text.Json.Serialization;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using ArchLens.Domain.Views;

namespace ArchLens.Domain.Graph;

/// <summary>Change status of a node or edge in a compare graph.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ChangeStatus>))]
public enum ChangeStatus
{
    [JsonStringEnumMemberName("added")] Added,
    [JsonStringEnumMemberName("removed")] Removed,
    [JsonStringEnumMemberName("modified")] Modified,
    [JsonStringEnumMemberName("unchanged")] Unchanged,
}

/// <summary>Identity of a drawn view. <c>ScopeId</c>: the system (container view) or container (component view), null for context.</summary>
public sealed record GraphView(string Key, ViewKind Kind, string Title, string Label, string? ScopeId, IReadOnlyList<ViewAncestor> Ancestors)
{
    internal static GraphView Of(ModelSnapshot snapshot, C4View view) =>
        new(view.Key, view.Kind, view.Title, ViewDerivation.Label(snapshot, view), view.ScopeId, ViewDerivation.Ancestors(snapshot, view));
}

/// <summary>
/// An element drawn in a view. <c>Outside</c>: a neighbour outside the view's scope (drawn as external).
/// <c>ChildViewKey</c>: the view one level down, if the element can be opened; <c>HasChildren</c>: that view has content.
/// <c>Position</c>: from the snapshot layout, null when the layout has none (the client places it).
/// </summary>
public sealed record ViewGraphNode(
    string ElementId, string Name, ElementKind Kind, string Technology, string Description,
    bool External, bool Outside, string? ChildViewKey, bool HasChildren, Point? Position);

/// <summary>An edge drawn in a view, labelled by its own description and technology.</summary>
public sealed record ViewGraphEdge(
    string Id, string SourceId, string TargetId, string Description, string Technology, bool Implied, IReadOnlyList<string> RelationshipIds);

/// <summary>Everything the editor canvas draws for one view.</summary>
public sealed record ViewGraph(GraphView View, IReadOnlyList<ViewGraphNode> Nodes, IReadOnlyList<ViewGraphEdge> Edges)
{
    /// <summary>Every view the editor can open, in the editor's node/edge shape.</summary>
    public static IReadOnlyList<ViewGraph> All(ModelSnapshot snapshot)
    {
        var index = new ModelIndex(snapshot);
        return ViewDerivation.Editable(snapshot).Select(view =>
        {
            var layout = snapshot.Layout.GetValueOrDefault(view.Key);
            var nodes = view.ElementIds.Select(id =>
            {
                var e = index.Get(id)!;
                var child = ViewDerivation.ChildKey(e);
                return new ViewGraphNode(e.Id, e.Name, e.Kind, e.Technology, e.Description, e.External, !view.Owns(index, id),
                    child, index.Children(e.Id).Count > 0, layout?.GetValueOrDefault(id));
            }).ToList();
            var edges = view.Edges.Select(e => new ViewGraphEdge(e.Key, e.SourceId, e.TargetId, e.Description, e.Technology, e.Implied, e.RelationshipIds)).ToList();
            return new ViewGraph(GraphView.Of(snapshot, view), nodes, edges);
        }).ToList();
    }
}

/// <summary>A node of the compare graph: a view node plus its change status and field changes.</summary>
public sealed record CompareNode(
    string ElementId, string Name, ElementKind Kind, string Technology, string Description,
    bool External, bool Outside, string? ChildViewKey, bool HasChildren,
    ChangeStatus Status, IReadOnlyList<FieldChange> Changes, Point? Position);

/// <summary>An edge of the compare graph with its change status and the field changes of its relationships.</summary>
public sealed record CompareEdge(
    string Id, string SourceId, string TargetId, string Description, string Technology, bool Implied,
    IReadOnlyList<string> RelationshipIds, ChangeStatus Status, IReadOnlyList<FieldChange> Changes);

/// <summary>Union of a view in base and head, with statuses from the diff and resolved positions.</summary>
public sealed record CompareGraph(GraphView View, IReadOnlyList<CompareNode> Nodes, IReadOnlyList<CompareEdge> Edges)
{
    /// <summary>
    /// The compare graph of one view: union of the base and head view; status and field changes from <paramref name="diff"/>;
    /// positions from the base layout for elements in the base view (unchanged, modified, removed), otherwise from the head
    /// layout when it does not overlap a base box, otherwise null. Returns null when neither snapshot has the view.
    /// </summary>
    public static CompareGraph? Build(ModelSnapshot baseModel, ModelSnapshot headModel, DiffResult diff, string viewKey)
    {
        var bv = ViewDerivation.Editable(baseModel).FirstOrDefault(v => v.Key == viewKey);
        var hv = ViewDerivation.Editable(headModel).FirstOrDefault(v => v.Key == viewKey);
        if (bv is null && hv is null) return null;
        var bIdx = new ModelIndex(baseModel);
        var hIdx = new ModelIndex(headModel);

        var added = diff.Elements.Added.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var removed = diff.Elements.Removed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var modified = diff.Elements.Modified.ToDictionary(m => m.Id, m => m.Changes, StringComparer.Ordinal);
        var relAdded = diff.Relationships.Added.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var relRemoved = diff.Relationships.Removed.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var relModified = diff.Relationships.Modified.ToDictionary(r => r.Id, r => r.Changes, StringComparer.Ordinal);

        var inBase = (bv?.ElementIds ?? []).ToHashSet(StringComparer.Ordinal);
        var inHead = (hv?.ElementIds ?? []).ToHashSet(StringComparer.Ordinal);
        var baseLayout = baseModel.Layout.GetValueOrDefault(viewKey);
        var headLayout = headModel.Layout.GetValueOrDefault(viewKey);

        var basePlaced = inBase.Select(id => baseLayout?.GetValueOrDefault(id)).OfType<Point>().ToList();
        Point? PositionOf(string id)
        {
            if (inBase.Contains(id)) return baseLayout?.GetValueOrDefault(id);
            var p = headLayout?.GetValueOrDefault(id);
            return p is not null && basePlaced.Any(b => NodeBox.Overlap(b, p)) ? null : p;
        }

        var nodes = inBase.Union(inHead).Order(StringComparer.Ordinal).Select(id =>
        {
            var idx = hIdx.Get(id) is not null ? hIdx : bIdx;
            var e = idx.Get(id)!;
            var view = inHead.Contains(id) ? hv! : bv!;
            var status = added.Contains(id) ? ChangeStatus.Added
                : removed.Contains(id) ? ChangeStatus.Removed
                : modified.ContainsKey(id) ? ChangeStatus.Modified : ChangeStatus.Unchanged;
            var position = PositionOf(id);
            return new CompareNode(e.Id, e.Name, e.Kind, e.Technology, e.Description, e.External, !view.Owns(idx, id),
                ViewDerivation.ChildKey(e), idx.Children(e.Id).Count > 0,
                status, modified.GetValueOrDefault(id) ?? [], position);
        }).ToList();

        var bEdges = (bv?.Edges ?? []).ToDictionary(e => e.Key, StringComparer.Ordinal);
        var hEdges = (hv?.Edges ?? []).ToDictionary(e => e.Key, StringComparer.Ordinal);
        var edges = bEdges.Keys.Union(hEdges.Keys).Order(StringComparer.Ordinal).Select(key =>
        {
            var e = hEdges.GetValueOrDefault(key) ?? bEdges[key];
            var ids = (hEdges.GetValueOrDefault(key)?.RelationshipIds ?? []).Union(bEdges.GetValueOrDefault(key)?.RelationshipIds ?? [])
                .Order(StringComparer.Ordinal).ToList();
            var status = !bEdges.ContainsKey(key) ? ChangeStatus.Added
                : !hEdges.ContainsKey(key) ? ChangeStatus.Removed
                : ids.Any(id => relAdded.Contains(id) || relRemoved.Contains(id) || relModified.ContainsKey(id)) ? ChangeStatus.Modified
                : ChangeStatus.Unchanged;
            var changes = ids.SelectMany(id => relModified.GetValueOrDefault(id) ?? []).ToList();
            return new CompareEdge(key, e.SourceId, e.TargetId, e.Description, e.Technology, e.Implied, ids, status, changes);
        }).ToList();

        return new CompareGraph(GraphView.Of(hv is null ? baseModel : headModel, hv ?? bv!), nodes, edges);
    }
}
