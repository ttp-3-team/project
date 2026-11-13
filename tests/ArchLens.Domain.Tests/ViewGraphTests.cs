using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;
using ArchLens.Domain.Views;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ViewGraphTests
{
    [Fact]
    public void Editor_views_carry_nodes_edges_ancestors_and_layout_positions()
    {
        var s = Small() with
        {
            Layout = new Dictionary<string, IReadOnlyDictionary<string, Point>> { ["container:sys"] = new Dictionary<string, Point> { ["web"] = new(10, 20) } },
        };
        var view = ViewGraph.All(s).Single(v => v.View.Key == "container:sys");
        Assert.Equal([new ViewAncestor("context", "System context", "Context")], view.View.Ancestors);
        var web = view.Nodes.Single(n => n.ElementId == "web");
        Assert.Equal((new Point(10, 20), false, "component:web", false), (web.Position, web.Outside, web.ChildViewKey, web.HasChildren));
        var user = view.Nodes.Single(n => n.ElementId == "user");
        Assert.Equal((null, true, (string?)null), (user.Position, user.Outside, user.ChildViewKey));
        Assert.Contains(view.Edges, e => e.Id == "web|api" && e.Description == "Calls" && e.Technology == "HTTPS");
    }

    [Fact]
    public void External_system_cannot_be_opened()
    {
        var context = ViewGraph.All(Small()).Single(v => v.View.Key == "context");
        Assert.Null(context.Nodes.Single(n => n.ElementId == "ext").ChildViewKey);
        Assert.Equal("container:sys", context.Nodes.Single(n => n.ElementId == "sys").ChildViewKey);
    }
}
