using ArchLens.Domain.Model;

namespace ArchLens.Domain.Diff;

/// <summary>
/// Semantic diff of two snapshots (port of the PoC's diff.ts). Elements and relationships are matched by
/// their stable id, so a rename is one modification, never remove + add. Layout and property ordering are
/// ignored. Output is sorted and contains no timestamps: same input → byte-identical JSON.
/// </summary>
public static class SemanticDiff
{
    public static DiffResult Compute(ModelSnapshot baseModel, ModelSnapshot headModel)
    {
        var elements = DiffElements(baseModel, headModel);
        var relationships = DiffRelationships(baseModel, headModel);
        var summary = new DiffSummary(
            elements.Added.Count, elements.Removed.Count, elements.Modified.Count,
            relationships.Added.Count, relationships.Removed.Count, relationships.Modified.Count);
        var views = ViewImpactAnalysis.Analyze(baseModel, headModel, elements, relationships);
        return new DiffResult(elements, relationships, views, summary);
    }

    private static ElementChanges DiffElements(ModelSnapshot baseModel, ModelSnapshot headModel)
    {
        var (added, removed, modified) = Match(baseModel.Elements, headModel.Elements, e => e.Id,
            e => new ElementRef(e.Id, e.Kind, e.Name, e.ParentId),
            (x, y) => Changes(
                ("name", x.Name, y.Name),
                ("description", x.Description, y.Description),
                ("technology", x.Technology, y.Technology),
                ("tags", Tags(x.Tags), Tags(y.Tags)),
                ("parentId", x.ParentId ?? "", y.ParentId ?? ""),
                ("external", x.External ? "true" : "false", y.External ? "true" : "false")),
            (y, changes) => new ElementModified(y.Id, y.Kind, y.Name, y.ParentId, changes));
        return new ElementChanges(added, removed, modified);
    }

    private static RelationshipChanges DiffRelationships(ModelSnapshot baseModel, ModelSnapshot headModel)
    {
        var (added, removed, modified) = Match(baseModel.Relationships, headModel.Relationships, r => r.Id,
            r => new RelationshipRef(r.Id, r.SourceId, r.TargetId, r.Description, r.Technology),
            (x, y) => Changes(
                ("description", x.Description, y.Description),
                ("technology", x.Technology, y.Technology),
                ("sourceId", x.SourceId, y.SourceId),
                ("targetId", x.TargetId, y.TargetId)),
            (y, changes) => new RelationshipModified(y.Id, y.SourceId, y.TargetId, y.Description, y.Technology, changes));
        return new RelationshipChanges(added, removed, modified);
    }

    internal static string Tags(IEnumerable<string> tags) =>
        string.Join(", ", tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal));

    private static (List<TRef> Added, List<TRef> Removed, List<TModified> Modified) Match<T, TRef, TModified>(
        IEnumerable<T> baseItems, IEnumerable<T> headItems, Func<T, string> idOf, Func<T, TRef> toRef,
        Func<T, T, List<FieldChange>> changesOf, Func<T, List<FieldChange>, TModified> toModified)
    {
        var b = baseItems.ToDictionary(idOf, StringComparer.Ordinal);
        var h = headItems.ToDictionary(idOf, StringComparer.Ordinal);
        var added = new List<TRef>();
        var removed = new List<TRef>();
        var modified = new List<TModified>();
        foreach (var id in b.Keys.Union(h.Keys).Order(StringComparer.Ordinal))
        {
            if (!b.TryGetValue(id, out var x)) added.Add(toRef(h[id]));
            else if (!h.TryGetValue(id, out var y)) removed.Add(toRef(x));
            else if (changesOf(x, y) is { Count: > 0 } changes) modified.Add(toModified(y, changes));
        }
        return (added, removed, modified);
    }

    private static List<FieldChange> Changes(params (string Field, string From, string To)[] fields) =>
        fields.Where(f => !string.Equals(f.From, f.To, StringComparison.Ordinal)).Select(f => new FieldChange(f.Field, f.From, f.To)).ToList();
}
