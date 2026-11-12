using ErrorOr;
using System.Text.RegularExpressions;

namespace ArchLens.Domain.Model;

/// <summary>Model rules every stored snapshot must satisfy. Problems are human-readable and sorted.</summary>
public static partial class SnapshotValidator
{
    /// <summary>
    /// Ids are opaque: new ones are UUIDs, imported and reference models keep their own (e.g. DSL ids like
    /// <c>apiGateway</c>). Ids are never derived from names.
    /// </summary>
    public const string IdPatternText = "^[A-Za-z0-9][A-Za-z0-9_.-]{0,127}$";

    [GeneratedRegex(IdPatternText)]
    private static partial Regex IdPattern();

    public static IReadOnlyList<string> Validate(ModelSnapshot snapshot)
    {
        var problems = new List<string>();
        if (snapshot.SchemaVersion != ModelSnapshot.CurrentSchemaVersion)
            problems.Add($"unsupported schemaVersion {snapshot.SchemaVersion} (expected {ModelSnapshot.CurrentSchemaVersion})");

        var byId = new Dictionary<string, Element>(StringComparer.Ordinal);
        foreach (var e in snapshot.Elements)
        {
            if (!IdPattern().IsMatch(e.Id ?? "")) problems.Add($"invalid element id '{e.Id}'");
            if (string.IsNullOrWhiteSpace(e.Name)) problems.Add($"element '{e.Id}' has no name");
            if (!byId.TryAdd(e.Id ?? "", e)) problems.Add($"duplicate element id '{e.Id}'");
        }

        foreach (var e in snapshot.Elements)
        {
            var expectedParent = e.Kind switch
            {
                ElementKind.Container => ElementKind.SoftwareSystem,
                ElementKind.Component => ElementKind.Container,
                _ => (ElementKind?)null,
            };
            if (expectedParent is null)
            {
                if (e.ParentId is not null) problems.Add($"element '{e.Id}' ({ElementKinds.Wire(e.Kind)}) cannot have a parent");
                continue;
            }
            if (e.ParentId is null || !byId.TryGetValue(e.ParentId, out var parent))
                problems.Add($"element '{e.Id}' ({ElementKinds.Wire(e.Kind)}) has a missing parent '{e.ParentId}'");
            else if (parent.Kind != expectedParent)
                problems.Add($"element '{e.Id}' ({ElementKinds.Wire(e.Kind)}) has parent '{parent.Id}' of kind {ElementKinds.Wire(parent.Kind)}, expected {ElementKinds.Wire(expectedParent.Value)}");
        }

        var index = new ModelIndex(snapshot);
        var relIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in snapshot.Relationships)
        {
            if (!IdPattern().IsMatch(r.Id ?? "")) problems.Add($"invalid relationship id '{r.Id}'");
            if (!relIds.Add(r.Id ?? "")) problems.Add($"duplicate relationship id '{r.Id}'");
            if (!byId.ContainsKey(r.SourceId ?? "")) problems.Add($"relationship '{r.Id}' has a missing source '{r.SourceId}'");
            if (!byId.ContainsKey(r.TargetId ?? "")) problems.Add($"relationship '{r.Id}' has a missing target '{r.TargetId}'");
            if (r.SourceId == r.TargetId) problems.Add($"relationship '{r.Id}' cannot connect an element to itself");
            else if (index.IsStrictlyWithin(r.SourceId ?? "", r.TargetId ?? "") || index.IsStrictlyWithin(r.TargetId ?? "", r.SourceId ?? ""))
                problems.Add($"relationship '{r.Id}' cannot connect an element to its parent");
        }

        problems.Sort(StringComparer.Ordinal);
        return problems;
    }

    /// <summary>Normalises the snapshot (nulls → defaults) and validates it.</summary>
    public static ErrorOr<ModelSnapshot> Check(ModelSnapshot snapshot)
    {
        var normalized = snapshot.Normalize();
        var problems = Validate(normalized);
        return problems.Count == 0 ? normalized : Problems.Of(problems);
    }
}
