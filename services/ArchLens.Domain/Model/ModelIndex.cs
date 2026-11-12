namespace ArchLens.Domain.Model;

/// <summary>Lookup structure over a snapshot: elements by id, children, ancestry.</summary>
public sealed class ModelIndex
{
    private readonly Dictionary<string, Element> _byId;
    private readonly Dictionary<string, List<Element>> _children;

    public ModelIndex(ModelSnapshot snapshot)
    {
        Snapshot = snapshot;
        _byId = new Dictionary<string, Element>(StringComparer.Ordinal);
        foreach (var e in snapshot.Elements) _byId.TryAdd(e.Id, e);
        _children = new Dictionary<string, List<Element>>(StringComparer.Ordinal);
        foreach (var e in snapshot.Elements.Where(e => e.ParentId is not null))
        {
            if (!_children.TryGetValue(e.ParentId!, out var list)) _children[e.ParentId!] = list = [];
            list.Add(e);
        }
        foreach (var list in _children.Values) list.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
    }

    public ModelSnapshot Snapshot { get; }
    public IEnumerable<Element> Elements => Snapshot.Elements;

    public Element? Get(string id) => _byId.GetValueOrDefault(id);

    public IReadOnlyList<Element> Children(string id) => _children.TryGetValue(id, out var list) ? list : [];

    private const int MaxDepth = 8;

    /// <summary>The element itself followed by its ancestors, nearest first.</summary>
    public IEnumerable<string> SelfAndAncestors(string id)
    {
        var depth = 0;
        for (var current = Get(id); current is not null && depth++ < MaxDepth; current = current.ParentId is null ? null : Get(current.ParentId))
            yield return current.Id;
    }

    public bool IsWithin(string id, string ancestorId) => SelfAndAncestors(id).Contains(ancestorId);

    public bool IsStrictlyWithin(string id, string ancestorId) => id != ancestorId && IsWithin(id, ancestorId);

    public string TopLevel(string id) => SelfAndAncestors(id).LastOrDefault() ?? id;

    public string? AncestorOfKind(string id, ElementKind kind) =>
        SelfAndAncestors(id).FirstOrDefault(x => Get(x)?.Kind == kind);

    public string? NearestVisible(string id, IReadOnlySet<string> visible) =>
        SelfAndAncestors(id).FirstOrDefault(visible.Contains);
}
