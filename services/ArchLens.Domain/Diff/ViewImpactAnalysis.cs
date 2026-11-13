using ArchLens.Domain.Model;
using ArchLens.Domain.Views;

namespace ArchLens.Domain.Diff;

internal static class ViewImpactAnalysis
{
    public static IReadOnlyList<ViewImpact> Analyze(ModelSnapshot baseModel, ModelSnapshot headModel, ElementChanges elements, RelationshipChanges relationships)
    {
        var baseViews = ViewDerivation.All(baseModel).ToDictionary(v => v.Key, StringComparer.Ordinal);
        var headViews = ViewDerivation.All(headModel).ToDictionary(v => v.Key, StringComparer.Ordinal);
        var baseIndex = new ModelIndex(baseModel);
        var headIndex = new ModelIndex(headModel);
        string Name(string id) => headIndex.Get(id)?.Name ?? baseIndex.Get(id)?.Name ?? id;

        var added = elements.Added.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var removed = elements.Removed.Select(e => e.Id).ToHashSet(StringComparer.Ordinal);
        var modified = elements.Modified.ToDictionary(m => m.Id, m => m.Changes, StringComparer.Ordinal);
        var relModified = relationships.Modified.ToDictionary(m => m.Id, m => m.Changes, StringComparer.Ordinal);

        var result = new List<ViewImpact>();
        var keys = baseViews.Values.Concat(headViews.Values)
            .Select(v => (v.Key, v.Kind)).Distinct()
            .OrderBy(v => v.Kind).ThenBy(v => v.Key, StringComparer.Ordinal).Select(v => v.Key);
        foreach (var key in keys)
        {
            baseViews.TryGetValue(key, out var bv);
            headViews.TryGetValue(key, out var hv);
            var view = (hv ?? bv)!;
            if (bv is null || hv is null)
            {
                result.Add(new ViewImpact(key, view.Kind, view.Title, bv is null ? ViewStatus.Added : ViewStatus.Removed, true,
                    [bv is null ? "view added" : "view removed"]));
                continue;
            }

            var reasons = new List<string>();
            var bEls = bv.ElementIds.ToHashSet(StringComparer.Ordinal);
            var hEls = hv.ElementIds.ToHashSet(StringComparer.Ordinal);
            foreach (var id in hv.ElementIds.Where(x => !bEls.Contains(x)))
                reasons.Add(added.Contains(id) ? $"added element '{Name(id)}'" : $"now in the view: '{Name(id)}'");
            foreach (var id in bv.ElementIds.Where(x => !hEls.Contains(x)))
                reasons.Add(removed.Contains(id) ? $"removed element '{Name(id)}'" : $"no longer in the view: '{Name(id)}'");
            var shown = hv.ElementIds.Where(bEls.Contains).ToList();
            if (hv.ScopeId is not null && !hEls.Contains(hv.ScopeId)) shown.Insert(0, hv.ScopeId);
            foreach (var id in shown)
                if (modified.TryGetValue(id, out var changes))
                    reasons.Add($"modified element '{Name(id)}': {FieldLabels.Join(changes)}");

            var bEdges = bv.Edges.ToDictionary(e => e.Key, StringComparer.Ordinal);
            var hEdges = hv.Edges.ToDictionary(e => e.Key, StringComparer.Ordinal);
            string Edge(ViewEdge e) => $"relationship '{Name(e.SourceId)}' → '{Name(e.TargetId)}'{(e.Implied ? " (implied)" : "")}";
            foreach (var e in hv.Edges.Where(e => !bEdges.ContainsKey(e.Key))) reasons.Add($"added {Edge(e)}");
            foreach (var e in bv.Edges.Where(e => !hEdges.ContainsKey(e.Key))) reasons.Add($"removed {Edge(e)}");
            foreach (var e in hv.Edges.Where(e => bEdges.ContainsKey(e.Key)))
            {
                var changes = e.RelationshipIds.Where(relModified.ContainsKey).SelectMany(id => relModified[id]).ToList();
                if (changes.Count > 0)
                    reasons.Add($"modified {Edge(e)}: {FieldLabels.Join(changes)}");
                else if (!e.RelationshipIds.SequenceEqual(bEdges[e.Key].RelationshipIds))
                    reasons.Add($"modified {Edge(e)}: the relationships it is derived from");
            }

            result.Add(new ViewImpact(key, view.Kind, hv.Title, reasons.Count > 0 ? ViewStatus.Changed : ViewStatus.Unchanged, reasons.Count > 0, reasons));
        }
        return result;
    }
}
