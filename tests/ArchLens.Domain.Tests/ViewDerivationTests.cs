using ArchLens.Domain.Views;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ViewDerivationTests
{
    private static C4View View(string key) => ViewDerivation.Find(Small(), key) ?? throw new Xunit.Sdk.XunitException($"no view {key}");

    private static string[] Edges(C4View v) => v.Edges.Select(e => e.Key).ToArray();

    [Fact]
    public void Context_view_lifts_relationships_of_descendants_to_top_level_elements()
    {
        var context = View("context");
        Assert.Equal(["ext", "sys", "user"], context.ElementIds);
        Assert.Equal(["sys|ext", "user|sys"], Edges(context));
        Assert.All(context.Edges, e => Assert.True(e.Implied));
    }

    [Fact]
    public void Container_view_includes_external_neighbours_as_top_level_elements()
    {
        var view = View("container:sys");
        Assert.Equal(["api", "db", "ext", "user", "web"], view.ElementIds);
        Assert.Contains("api|ext", Edges(view));
        Assert.Contains("user|web", Edges(view));
        Assert.False(view.Edges.Single(e => e.Key == "web|api").Implied);
    }

    [Fact]
    public void Component_view_shows_components_and_lifted_neighbours()
    {
        var view = View("component:api");
        Assert.Equal(["ctrl", "db", "repo"], view.ElementIds);
        Assert.Equal(["ctrl|repo", "repo|db"], Edges(view));
    }

    [Fact]
    public void Implied_edges_are_not_duplicated()
    {
        var s = Small() with { Relationships = [.. Small().Relationships, Rel("ctrl", "ext", "Charges", id: "r-2")] };
        var context = ViewDerivation.Find(s, "context")!;
        var edge = Assert.Single(context.Edges, e => e.Key == "sys|ext");
        Assert.Equal(["r-2", "r-api-ext"], edge.RelationshipIds);
    }

    [Fact]
    public void Edge_backed_by_an_explicit_relationship_is_not_implied()
    {
        var s = Small() with { Relationships = [.. Small().Relationships, Rel("web", "ctrl", "Calls", id: "r-3")] };
        var edge = ViewDerivation.Find(s, "container:sys")!.Edges.Single(e => e.Key == "web|api");
        Assert.Equal(["r-3", "r-web-api"], edge.RelationshipIds);
        Assert.False(edge.Implied);
    }

    [Fact]
    public void Lifted_edge_is_labelled_by_its_own_relationship()
    {
        var s = Small() with { Relationships = [.. Small().Relationships.Where(r => r.Id != "r-api-ext"), Rel("ctrl", "ext", "Charges cards via", "REST", id: "r-2")] };
        var edge = ViewDerivation.Find(s, "context")!.Edges.Single(e => e.Key == "sys|ext");
        Assert.Equal(("Charges cards via", "REST", true), (edge.Description, edge.Technology, edge.Implied));
    }

    [Fact]
    public void Editable_views_include_empty_systems_and_containers()
    {
        var keys = ViewDerivation.Editable(Small()).Select(v => v.Key).ToArray();
        Assert.Equal(["context", "container:sys", "component:api", "component:db", "component:web"], keys);
    }

    [Fact]
    public void ShopFlow_has_one_context_one_container_and_three_component_views()
    {
        var keys = ViewDerivation.All(Reference("base.json")).Select(v => v.Key).ToArray();
        Assert.Equal(["context", "container:shop", "component:notificationService", "component:orderService", "component:paymentService"], keys);
    }
}
