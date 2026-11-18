using System.Text;
using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;

namespace ArchLens.Domain.Render;

/// <summary>The Markdown PR comment. User-supplied names are escaped so they cannot ping people or break formatting.</summary>
public static class PrComment
{
    /// <summary>Marker that identifies the sticky comment.</summary>
    public const string Marker = "<!-- archlens-pr-check -->";

    /// <summary>Renders the comment. <paramref name="images"/> maps a view key to its image URL or relative path.</summary>
    public static string Render(ModelSnapshot baseModel, ModelSnapshot headModel, DiffResult diff, DriftResult? drift, IReadOnlyDictionary<string, string> images)
    {
        var names = baseModel.Elements.Concat(headModel.Elements).GroupBy(e => e.Id).ToDictionary(g => g.Key, g => g.Last().Name, StringComparer.Ordinal);
        string NameOf(string id) => names.GetValueOrDefault(id, id);
        var md = new StringBuilder();
        md.AppendLine(Marker);
        md.AppendLine("## ArchLens: architecture changes");
        md.AppendLine();
        var s = diff.Summary;
        md.AppendLine(diff.IsEmpty
            ? "The model did not change (layout changes are ignored)."
            : $"Elements: +{s.ElementsAdded} −{s.ElementsRemoved} ~{s.ElementsModified} · Relationships: +{s.RelationshipsAdded} −{s.RelationshipsRemoved} ~{s.RelationshipsModified}");
        md.AppendLine();
        foreach (var e in diff.Elements.Added) md.AppendLine($"- 🟢 added {ElementKinds.Label(e.Kind)} '{Escape(e.Name)}'");
        foreach (var e in diff.Elements.Removed) md.AppendLine($"- 🔴 removed {ElementKinds.Label(e.Kind)} '{Escape(e.Name)}'");
        foreach (var m in diff.Elements.Modified)
            md.AppendLine($"- 🟡 modified {ElementKinds.Label(m.Kind)} '{Escape(m.Name)}': {Changes(m.Changes)}");
        string Rel(string sourceId, string targetId, string description) =>
            $"'{Escape(NameOf(sourceId))}' → '{Escape(NameOf(targetId))}'{(description.Length > 0 ? $" ({Escape(description)})" : "")}";
        foreach (var r in diff.Relationships.Added) md.AppendLine($"- 🟢 added relationship {Rel(r.SourceId, r.TargetId, r.Description)}");
        foreach (var r in diff.Relationships.Removed) md.AppendLine($"- 🔴 removed relationship {Rel(r.SourceId, r.TargetId, r.Description)}");
        foreach (var r in diff.Relationships.Modified) md.AppendLine($"- 🟡 modified relationship {Rel(r.SourceId, r.TargetId, "")}: {Changes(r.Changes)}");
        md.AppendLine();
        var affected = diff.Views.Where(v => v.Affected).ToList();
        md.AppendLine($"### Affected views: {affected.Count} / {diff.Views.Count}");
        foreach (var v in affected)
        {
            md.AppendLine();
            md.AppendLine($"**{Escape(v.Title)}**");
            foreach (var r in v.Reasons) md.AppendLine($"- {Escape(r)}");
            if (images.TryGetValue(v.Key, out var image)) md.AppendLine().AppendLine($"![{Escape(v.Title)}]({image})");
        }
        if (drift is not null)
        {
            md.AppendLine();
            md.AppendLine(drift.Ok ? "### ✅ Deployment check (R1): no differences" : $"### ❌ Deployment check (R1): differences: {drift.Messages.Count}");
            foreach (var m in drift.Messages) md.AppendLine($"- {Escape(m)}");
        }
        return md.ToString();
    }

    private static string Changes(IEnumerable<FieldChange> changes) =>
        string.Join("; ", changes.Select(c => $"{FieldLabels.Of(c.Field)}: '{Escape(c.From)}' → '{Escape(c.To)}'"));

    /// <summary>Escapes Markdown and HTML metacharacters; <c>@</c> becomes an entity so a name never mentions anyone.</summary>
    public static string Escape(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '@': sb.Append("&#64;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '&': sb.Append("&amp;"); break;
                case '\\' or '`' or '*' or '_' or '[' or ']' or '#' or '|' or '~' or '!':
                    sb.Append('\\').Append(ch);
                    break;
                case '\n' or '\r': sb.Append(' '); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }
}
