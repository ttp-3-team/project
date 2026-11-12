using ArchLens.Domain.Model;

namespace ArchLens.Domain.Views;

/// <summary>
/// Derives C4 views from a snapshot. Views are never stored. Keys: <c>context</c>;
/// <c>container:&lt;systemId&gt;</c>; <c>component:&lt;containerId&gt;</c>.
/// </summary>
public static class ViewDerivation
{
    public const string ContextKey = "context";

    public static string ContainerKey(string systemId) => $"container:{systemId}";
    public static string ComponentKey(string containerId) => $"component:{containerId}";

    /// <summary>
    /// Views that have content: context, container views of systems with containers and component views of
    /// containers with components. Ordered context → container → component, then by key. The diff reports on these.
    /// </summary>
    public static IReadOnlyList<C4View> All(ModelSnapshot snapshot) => Derive(new ModelIndex(snapshot), includeEmpty: false).ToList();

    /// <summary>
    /// Every view the editor can open: also container views of internal systems without containers and component
    /// views of containers without components, so the first child can be added there.
    /// </summary>
    public static IReadOnlyList<C4View> Editable(ModelSnapshot snapshot) => Derive(new ModelIndex(snapshot), includeEmpty: true).ToList();

    /// <summary>One view by key, or null when the snapshot has no such view (or it would be empty).</summary>
    public static C4View? Find(ModelSnapshot snapshot, string key) => All(snapshot).FirstOrDefault(v => v.Key == key);

    /// <summary>The key of the view one level below an element, or null for people, external systems and components.</summary>
    public static string? ChildKey(Element element) => element switch
    {
        { Kind: ElementKind.SoftwareSystem, External: false } => ContainerKey(element.Id),
        { Kind: ElementKind.Container } => ComponentKey(element.Id),
        _ => null,
    };

    /// <summary>The views above <paramref name="view"/>, from the context view down.</summary>
    public static IReadOnlyList<ViewAncestor> Ancestors(ModelSnapshot snapshot, C4View view)
    {
        if (view.Kind == ViewKind.Context) return [];
        var index = new ModelIndex(snapshot);
        var trail = new List<ViewAncestor> { new(ContextKey, ContextTitle, ContextLabel) };
        if (view.Kind == ViewKind.Component && view.ScopeId is { } containerId && index.Get(containerId)?.ParentId is { } systemId && index.Get(systemId) is { } system)
            trail.Add(new(ContainerKey(system.Id), ContainerTitle(system), system.Name));
        return trail;
    }

    /// <summary>Short label of a view for breadcrumbs: "Context", or the name of the scope element.</summary>
    public static string Label(ModelSnapshot snapshot, C4View view) =>
        view.ScopeId is { } scope ? new ModelIndex(snapshot).Get(scope)?.Name ?? scope : ContextLabel;

    private const string ContextTitle = "System context";
    private const string ContextLabel = "Context";
    private static string ContainerTitle(Element system) => $"Containers: {system.Name}";
    private static string ComponentTitle(Element container) => $"Components: {container.Name}";

    private static IEnumerable<C4View> Derive(ModelIndex index, bool includeEmpty)
    {
        yield return Context(index);
        var ordered = index.Elements.OrderBy(e => e.Id, StringComparer.Ordinal).ToList();
        foreach (var system in ordered.Where(e => e.Kind == ElementKind.SoftwareSystem))
            if (index.Children(system.Id).Count > 0 || (includeEmpty && !system.External)) yield return Container(index, system);
        foreach (var container in ordered.Where(e => e.Kind == ElementKind.Container))
            if (index.Children(container.Id).Count > 0 || includeEmpty) yield return Component(index, container);
    }

    private static C4View Context(ModelIndex index)
    {
        var visible = index.Elements.Where(e => e.ParentId is null).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        return Build(index, ContextKey, ViewKind.Context, ContextTitle, null, visible);
    }

    private static C4View Container(ModelIndex index, Element system)
    {
        var visible = index.Children(system.Id).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var r in index.Snapshot.Relationships)
        {
            var sourceInside = index.IsWithin(r.SourceId, system.Id);
            var targetInside = index.IsWithin(r.TargetId, system.Id);
            if (sourceInside == targetInside || r.SourceId == system.Id || r.TargetId == system.Id) continue;
            visible.Add(index.TopLevel(sourceInside ? r.TargetId : r.SourceId));
        }
        return Build(index, ContainerKey(system.Id), ViewKind.Container, ContainerTitle(system), system.Id, visible);
    }

    private static C4View Component(ModelIndex index, Element container)
    {
        var system = container.ParentId ?? "";
        var visible = index.Children(container.Id).Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var r in index.Snapshot.Relationships)
        {
            var sourceInside = index.IsStrictlyWithin(r.SourceId, container.Id);
            var targetInside = index.IsStrictlyWithin(r.TargetId, container.Id);
            if (sourceInside == targetInside) continue;
            var outside = sourceInside ? r.TargetId : r.SourceId;
            if (outside == container.Id) continue;
            visible.Add(index.IsWithin(outside, system) ? index.AncestorOfKind(outside, ElementKind.Container) ?? outside : index.TopLevel(outside));
        }
        return Build(index, ComponentKey(container.Id), ViewKind.Component, ComponentTitle(container), container.Id, visible);
    }

    private static C4View Build(ModelIndex index, string key, ViewKind kind, string title, string? scope, HashSet<string> visible)
    {
        var groups = new SortedDictionary<string, (string Source, string Target, List<Relationship> Lifted, List<Relationship> Direct)>(StringComparer.Ordinal);
        foreach (var r in index.Snapshot.Relationships.OrderBy(r => r.Id, StringComparer.Ordinal))
        {
            var source = index.NearestVisible(r.SourceId, visible);
            var target = index.NearestVisible(r.TargetId, visible);
            if (source is null || target is null || source == target) continue;
            var edgeKey = $"{source}|{target}";
            if (!groups.TryGetValue(edgeKey, out var group)) groups[edgeKey] = group = (source, target, [], []);
            (source == r.SourceId && target == r.TargetId ? group.Direct : group.Lifted).Add(r);
        }
        var edges = groups.Values.Select(g =>
        {
            var label = g.Direct.FirstOrDefault() ?? g.Lifted[0];
            var ids = g.Direct.Concat(g.Lifted).Select(r => r.Id).Order(StringComparer.Ordinal).ToList();
            return new ViewEdge(g.Source, g.Target, ids, g.Direct.Count == 0, label.Description, label.Technology);
        }).ToList();
        return new C4View(key, kind, title, scope, visible.Order(StringComparer.Ordinal).ToList(), edges);
    }
}
