namespace ArchLens.Domain.Diff;

/// <summary>Labels of changed fields, shared by affected-view reasons, the SVG and the PR comment.</summary>
public static class FieldLabels
{
    private static readonly Dictionary<string, string> Labels = new(StringComparer.Ordinal)
    {
        ["name"] = "name",
        ["description"] = "description",
        ["technology"] = "technology",
        ["tags"] = "tags",
        ["parentId"] = "parent",
        ["external"] = "external",
        ["sourceId"] = "source",
        ["targetId"] = "target",
    };

    /// <summary>Label of a field, or the field name itself when unknown.</summary>
    public static string Of(string field) => Labels.GetValueOrDefault(field, field);

    /// <summary>Distinct labels of the changed fields, in order, comma separated.</summary>
    public static string Join(IEnumerable<FieldChange> changes) => string.Join(", ", changes.Select(c => Of(c.Field)).Distinct());
}
