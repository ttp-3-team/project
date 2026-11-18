using System.Globalization;
using System.Xml.Linq;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;

namespace ArchLens.Domain.Render;

/// <summary>
/// Draws a <see cref="CompareGraph"/> as SVG with the web compare view's colour semantics: added green, removed red
/// dashed ghost, modified amber with "was:" lines. Statuses and positions come from the graph; nodes without a
/// position are placed on a grid to the right, existing ones first and added ones after, so an addition never shifts
/// other boxes. Output is deterministic: same graph → byte-identical SVG.
/// </summary>
public static class SvgDiffRenderer
{
    private const double NodeWidth = NodeBox.Width, NodeHeight = NodeBox.Height, PersonRadius = 28, BoxRadius = 8;
    private const double Margin = 40, TitleBand = 30, LegendBand = 40, GridGapX = 300, GridGapY = 170;
    private const int GridRows = 4, MaxChangeLines = 3;
    private const double NameY = 34, TechY = 54, BodyY = 78, LineHeight = 15, LabelLift = 6, LegendSpacing = 100;
    private const string Added = "#2ea043", Removed = "#e5534b", Modified = "#d29922", Muted = "#8b949e";

    private static readonly Dictionary<ElementKind, string> Fill = new()
    {
        [ElementKind.Person] = "#08427b",
        [ElementKind.SoftwareSystem] = "#1168bd",
        [ElementKind.Container] = "#438dd5",
        [ElementKind.Component] = "#85bbf0",
    };

    /// <summary>Final positions: the graph's own, then a grid for the rest (existing elements first, added ones last).</summary>
    public static IReadOnlyDictionary<string, Point> Positions(CompareGraph graph)
    {
        var result = new SortedDictionary<string, Point>(StringComparer.Ordinal);
        foreach (var n in graph.Nodes)
            if (n.Position is not null) result[n.ElementId] = n.Position;
        var nextX = result.Count > 0 ? result.Values.Max(p => p.X) + GridGapX : 0;
        var topY = result.Count > 0 ? result.Values.Min(p => p.Y) : 0;
        var row = 0;
        var unplaced = graph.Nodes.Where(n => n.Position is null)
            .OrderBy(n => n.Status == ChangeStatus.Added ? 1 : 0).ThenBy(n => n.ElementId, StringComparer.Ordinal);
        foreach (var n in unplaced)
        {
            result[n.ElementId] = new Point(nextX, topY + row * GridGapY);
            if (++row == GridRows) { row = 0; nextX += GridGapX; }
        }
        return result;
    }

    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";

    private static XElement El(string name, params object?[] content) => new(Svg + name, content);

    private static XAttribute A(string name, object value) => new(name, value is double d ? N(d) : value);

    /// <summary>Renders the graph as an SVG document (XLinq escapes all text).</summary>
    public static string Render(CompareGraph graph)
    {
        var positions = Positions(graph);
        var minX = positions.Count > 0 ? positions.Values.Min(p => p.X) - Margin : 0;
        var minY = positions.Count > 0 ? positions.Values.Min(p => p.Y) - Margin - TitleBand : 0;
        var maxX = positions.Count > 0 ? positions.Values.Max(p => p.X) + NodeWidth + Margin : 2 * Margin;
        var maxY = positions.Count > 0 ? positions.Values.Max(p => p.Y) + NodeHeight + Margin + LegendBand : 2 * Margin;
        double width = maxX - minX, height = maxY - minY;

        var root = El("svg",
            A("viewBox", $"{N(minX)} {N(minY)} {N(width)} {N(height)}"), A("width", width), A("height", height),
            A("font-family", "Helvetica, Arial, sans-serif"));
        root.Add(El("defs", new[] { ChangeStatus.Unchanged, ChangeStatus.Added, ChangeStatus.Removed, ChangeStatus.Modified }.Select(status =>
            El("marker", A("id", $"arrow-{Wire(status)}"), A("viewBox", "0 0 10 10"), A("refX", 10), A("refY", 5),
                A("markerWidth", 8), A("markerHeight", 8), A("orient", "auto-start-reverse"),
                El("path", A("d", "M0,0 L10,5 L0,10 z"), A("fill", Color(status)))))));
        root.Add(El("rect", A("x", minX), A("y", minY), A("width", width), A("height", height), A("fill", "#ffffff")));
        root.Add(El("text", A("x", minX + Margin), A("y", minY + 28), A("font-size", 18), A("font-weight", "bold"), A("fill", "#24292f"), graph.View.Title));

        foreach (var e in graph.Edges)
        {
            var (x1, y1, x2, y2) = Anchor(positions[e.SourceId], positions[e.TargetId]);
            root.Add(El("line", A("x1", x1), A("y1", y1), A("x2", x2), A("y2", y2), A("stroke", Color(e.Status)),
                A("stroke-width", e.Status == ChangeStatus.Unchanged ? "1.5" : "2.5"),
                e.Status == ChangeStatus.Removed || e.Implied ? A("stroke-dasharray", "6 4") : null,
                A("marker-end", $"url(#arrow-{Wire(e.Status)})")));
            if (e.Description.Length > 0)
                root.Add(El("text", A("x", (x1 + x2) / 2), A("y", (y1 + y2) / 2 - LabelLift), A("font-size", 11), A("text-anchor", "middle"),
                    A("fill", "#57606a"), A("paint-order", "stroke"), A("stroke", "#ffffff"), A("stroke-width", 4), Trim(e.Description, 40)));
        }

        foreach (var n in graph.Nodes.OrderBy(n => n.ElementId, StringComparer.Ordinal))
        {
            var p = positions[n.ElementId];
            var outside = n.Outside || n.External;
            var text = n.Kind == ElementKind.Component && !outside ? "#0b2540" : "#ffffff";
            var cx = p.X + NodeWidth / 2;
            var box = El("rect", A("x", p.X), A("y", p.Y), A("width", NodeWidth), A("height", NodeHeight),
                A("rx", n.Kind == ElementKind.Person ? PersonRadius : BoxRadius), A("fill", outside ? "#6b6b6b" : Fill[n.Kind]));
            switch (n.Status)
            {
                case ChangeStatus.Added: box.Add(A("stroke", Added), A("stroke-width", 5)); break;
                case ChangeStatus.Removed: box.Add(A("stroke", Removed), A("stroke-width", 4), A("stroke-dasharray", "8 5"), A("opacity", "0.55")); break;
                case ChangeStatus.Modified: box.Add(A("stroke", Modified), A("stroke-width", 5)); break;
            }
            var kind = char.ToUpperInvariant(ElementKinds.Label(n.Kind)[0]) + ElementKinds.Label(n.Kind)[1..];
            var group = El("g", box,
                El("text", A("x", cx), A("y", p.Y + NameY), A("font-size", 15), A("font-weight", "bold"), A("text-anchor", "middle"), A("fill", text), Trim(n.Name, 28)),
                El("text", A("x", cx), A("y", p.Y + TechY), A("font-size", 11), A("text-anchor", "middle"), A("fill", text), A("opacity", "0.85"),
                    Trim(n.Technology.Length > 0 ? $"[{kind}: {n.Technology}]" : $"[{kind}]", 38)));
            if (n.Status == ChangeStatus.Modified)
                group.Add(n.Changes.Take(MaxChangeLines).Select((c, i) =>
                    El("text", A("x", p.X + 10), A("y", p.Y + BodyY + i * LineHeight), A("font-size", 11), A("fill", "#ffe08a"),
                        Trim($"was: {FieldLabels.Of(c.Field)}: {(c.From.Length > 0 ? c.From : "—")}", 40))));
            else if (n.Description.Length > 0)
                group.Add(El("text", A("x", cx), A("y", p.Y + BodyY + 2), A("font-size", 11), A("text-anchor", "middle"), A("fill", text), A("opacity", "0.9"), Trim(n.Description, 40)));
            root.Add(group);
        }

        var lx = minX + Margin;
        foreach (var status in new[] { ChangeStatus.Added, ChangeStatus.Removed, ChangeStatus.Modified })
        {
            root.Add(El("rect", A("x", lx), A("y", maxY - 28), A("width", 12), A("height", 12), A("rx", 2), A("fill", Color(status))),
                El("text", A("x", lx + 18), A("y", maxY - 18), A("font-size", 12), A("fill", "#24292f"), Legend(status)));
            lx += LegendSpacing;
        }
        return root.ToString(SaveOptions.DisableFormatting) + "\n";
    }

    private static string Color(ChangeStatus s) => s switch
    {
        ChangeStatus.Added => Added,
        ChangeStatus.Removed => Removed,
        ChangeStatus.Modified => Modified,
        _ => Muted,
    };

    private static string Wire(ChangeStatus s) => s.ToString().ToLowerInvariant();

    private static string Legend(ChangeStatus s) => s switch
    {
        ChangeStatus.Added => "Added",
        ChangeStatus.Removed => "Removed",
        _ => "Modified",
    };

    private static (double, double, double, double) Anchor(Point s, Point t)
    {
        var dx = t.X - s.X;
        var dy = t.Y - s.Y;
        if (Math.Abs(dx) >= Math.Abs(dy))
            return dx >= 0 ? (s.X + NodeWidth, s.Y + NodeHeight / 2, t.X, t.Y + NodeHeight / 2) : (s.X, s.Y + NodeHeight / 2, t.X + NodeWidth, t.Y + NodeHeight / 2);
        return dy >= 0 ? (s.X + NodeWidth / 2, s.Y + NodeHeight, t.X + NodeWidth / 2, t.Y) : (s.X + NodeWidth / 2, s.Y, t.X + NodeWidth / 2, t.Y + NodeHeight);
    }

    private static string N(double v) => Math.Round(v, 1).ToString("0.#", CultureInfo.InvariantCulture);

    private static string Trim(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
