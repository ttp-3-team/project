using ArchLens.Domain.Diff;
using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;
using ArchLens.Domain.Views;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class CompareGraphTests
{
    private static ModelSnapshot WithLayout(ModelSnapshot s, string view, params (string Id, double X, double Y)[] points) => s with
    {
        Layout = new Dictionary<string, IReadOnlyDictionary<string, Point>>(s.Layout)
        {
            [view] = points.ToDictionary(p => p.Id, p => new Point(p.X, p.Y)),
        },
    };

    private static readonly ModelSnapshot Base = WithLayout(
        Snapshot([Sys("sys"), Container("web", "sys", "Web"), Container("api", "sys", "API"), Container("old", "sys", "Old")], [Rel("web", "api", "Calls")]),
        "container:sys", ("web", 0, 0), ("api", 300, 0), ("old", 600, 0));

    private static readonly ModelSnapshot Head = WithLayout(
        Snapshot([Sys("sys"), Container("web", "sys", "Storefront"), Container("api", "sys", "API"), Container("cache", "sys", "Cache", "Redis")],
            [Rel("web", "api", "Calls"), Rel("api", "cache")]),
        "container:sys", ("web", 999, 999), ("api", 300, 0), ("cache", 300, 200));

    private static CompareGraph Graph() => CompareGraph.Build(Base, Head, SemanticDiff.Compute(Base, Head), "container:sys")!;

    private static CompareNode Node(string id) => Graph().Nodes.Single(n => n.ElementId == id);

    [Fact]
    public void Draws_the_union_of_base_and_head_with_the_removed_element_as_a_ghost()
    {
        Assert.Equal(["api", "cache", "old", "web"], Graph().Nodes.Select(n => n.ElementId));
        Assert.Equal((ChangeStatus.Removed, "Old"), (Node("old").Status, Node("old").Name));
        Assert.Equal(ChangeStatus.Added, Node("cache").Status);
    }

    [Fact]
    public void Keeps_base_positions_for_unchanged_modified_and_removed_elements()
    {
        Assert.Equal(new Point(0, 0), Node("web").Position);
        Assert.Equal(new Point(300, 0), Node("api").Position);
        Assert.Equal(new Point(600, 0), Node("old").Position);
        Assert.Equal(new Point(300, 200), Node("cache").Position);
    }

    [Fact]
    public void Position_is_null_when_no_layout_has_the_element()
    {
        var head = Head with { Layout = new Dictionary<string, IReadOnlyDictionary<string, Point>>() };
        var graph = CompareGraph.Build(Base, head, SemanticDiff.Compute(Base, head), "container:sys")!;
        Assert.Null(graph.Nodes.Single(n => n.ElementId == "cache").Position);
    }

    [Fact]
    public void Added_element_whose_head_position_covers_a_base_box_is_left_unplaced()
    {
        var head = WithLayout(Head, "container:sys", ("web", 999, 999), ("api", 300, 0), ("cache", 310, 20));
        var graph = CompareGraph.Build(Base, head, SemanticDiff.Compute(Base, head), "container:sys")!;
        Assert.Null(graph.Nodes.Single(n => n.ElementId == "cache").Position);
    }

    [Fact]
    public void Reports_a_rename_as_one_modified_element_with_its_previous_value()
    {
        var web = Node("web");
        Assert.Equal(ChangeStatus.Modified, web.Status);
        Assert.Equal([new FieldChange("name", "Web", "Storefront")], web.Changes);
        Assert.Equal(ChangeStatus.Unchanged, Node("api").Status);
    }

    [Fact]
    public void Marks_added_edges_and_keeps_unchanged_ones()
    {
        var edges = Graph().Edges.ToDictionary(e => e.Id);
        Assert.Equal(ChangeStatus.Added, edges["api|cache"].Status);
        Assert.Equal(ChangeStatus.Unchanged, edges["web|api"].Status);
        Assert.Equal("Calls", edges["web|api"].Description);
    }

    [Fact]
    public void Modified_relationship_carries_its_field_changes()
    {
        var head = Base with { Relationships = [Rel("web", "api", "Calls", "gRPC")] };
        var graph = CompareGraph.Build(Base, head, SemanticDiff.Compute(Base, head), "container:sys")!;
        var edge = graph.Edges.Single();
        Assert.Equal(ChangeStatus.Modified, edge.Status);
        Assert.Equal([new FieldChange("technology", "", "gRPC")], edge.Changes);
    }

    [Fact]
    public void View_carries_ancestors_and_nodes_carry_their_child_view()
    {
        var b = Small();
        var graph = CompareGraph.Build(b, b, SemanticDiff.Compute(b, b), "component:api")!;
        Assert.Equal([new ViewAncestor("context", "System context", "Context"), new ViewAncestor("container:sys", "Containers: Shop", "Shop")], graph.View.Ancestors);
        var db = graph.Nodes.Single(n => n.ElementId == "db");
        Assert.True(db.Outside);
        Assert.Equal("component:db", db.ChildViewKey);
        Assert.False(db.HasChildren);
    }

    [Fact]
    public Task ShopFlow_container_view_compare_graph()
    {
        var b = Reference("base.json");
        var h = Reference("head.json");
        return Verify(SnapshotJson.Serialize(CompareGraph.Build(b, h, SemanticDiff.Compute(b, h), "container:shop"), indented: true), "json");
    }

    [Fact]
    public void Unknown_view_gives_no_graph()
    {
        Assert.Null(CompareGraph.Build(Small(), Small(), SemanticDiff.Compute(Small(), Small()), "component:nope"));
    }
}
