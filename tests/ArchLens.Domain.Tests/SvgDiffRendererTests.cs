using ArchLens.Domain.Diff;
using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;
using ArchLens.Domain.Render;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class SvgDiffRendererTests
{
    private static CompareGraph ShopFlow(ModelSnapshot? head = null)
    {
        var b = Reference("base.json");
        var h = head ?? Reference("head.json");
        return CompareGraph.Build(b, h, SemanticDiff.Compute(b, h), "container:shop")!;
    }

    [Fact]
    public Task Rendering_is_deterministic_byte_for_byte()
    {
        var h = Reference("head.json");
        var first = SvgDiffRenderer.Render(ShopFlow());
        var second = SvgDiffRenderer.Render(ShopFlow(h with { Elements = h.Elements.Reverse().ToList() }));
        Assert.Equal(first, second);
        return Verify(first, "svg");
    }

    [Fact]
    public void Graph_positions_are_used_and_only_unplaced_nodes_go_to_the_grid()
    {
        var graph = ShopFlow();
        var positions = SvgDiffRenderer.Positions(graph);
        foreach (var n in graph.Nodes.Where(n => n.Position is not null)) Assert.Equal(n.Position, positions[n.ElementId]);
        var cache = graph.Nodes.Single(n => n.ElementId == "cache");
        Assert.Null(cache.Position);
        Assert.True(positions["cache"].X > graph.Nodes.Where(n => n.Position is not null).Max(n => n.Position!.X));
    }

    [Fact]
    public void Added_nodes_are_placed_after_existing_ones()
    {
        var b = Small();
        var h = Small() with { Elements = [.. Small().Elements, Container("aaa", "sys", "First by id")] };
        var graph = CompareGraph.Build(b, h, SemanticDiff.Compute(b, h), "container:sys")!;
        var positions = SvgDiffRenderer.Positions(graph);
        var last = positions.MaxBy(p => (p.Value.X, p.Value.Y)).Key;
        Assert.Equal("aaa", last);
    }

    [Fact]
    public void Lifted_edges_use_their_own_label()
    {
        var b = Small();
        var h = Small() with { Relationships = [.. Small().Relationships.Where(r => r.Id != "r-api-ext"), Rel("ctrl", "ext", "Charges cards via", id: "r-2")] };
        var svg = SvgDiffRenderer.Render(CompareGraph.Build(b, h, SemanticDiff.Compute(b, h), "context")!);
        Assert.Contains("Charges cards via", svg);
    }
}
