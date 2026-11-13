using ArchLens.Domain.Model;

namespace ArchLens.Domain.Graph;

/// <summary>
/// Size of a drawn C4 node. The web canvas (<c>apps/web/src/model/node-size.ts</c>) and the SVG renderer draw boxes of
/// this size, so positions computed here line up with what people see.
/// </summary>
public static class NodeBox
{
    public const double Width = 240;
    public const double Height = 136;

    /// <summary>Whether two boxes placed at these top-left corners overlap.</summary>
    public static bool Overlap(Point a, Point b) => Math.Abs(a.X - b.X) < Width && Math.Abs(a.Y - b.Y) < Height;
}
