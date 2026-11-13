using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ViewImpactTests
{
    private static ViewImpact View(DiffResult d, string key) => d.Views.Single(v => v.Key == key);

    [Fact]
    public Task ShopFlow_reference_pair_matches_expected_diff() =>
        Verify(SnapshotJson.Serialize(SemanticDiff.Compute(Reference("base.json"), Reference("head.json")), indented: true), "json");

    [Fact]
    public void Container_change_affects_its_container_view_and_the_context_through_an_implied_relationship()
    {
        var head = Small() with { Relationships = [.. Small().Relationships, Rel("api", "user", "Notifies")] };
        var d = SemanticDiff.Compute(Small(), head);

        Assert.True(View(d, "container:sys").Affected);
        Assert.Contains("added relationship 'API' → 'User'", View(d, "container:sys").Reasons);
        Assert.Equal(["added relationship 'Shop' → 'User' (implied)"], View(d, "context").Reasons);
        Assert.False(View(d, "component:api").Affected);
    }

    [Fact]
    public void Modified_container_is_reported_with_its_changed_fields()
    {
        var head = Small().With(e => e.Id == "web" ? e with { Name = "Storefront", Technology = "Vue" } : e);
        var d = SemanticDiff.Compute(Small(), head);
        Assert.Equal(["modified element 'Storefront': name, technology"], View(d, "container:sys").Reasons);
        Assert.False(View(d, "context").Affected);
    }

    [Fact]
    public void Change_inside_one_component_view_leaves_other_component_views_unaffected()
    {
        var b = Small() with { Elements = [.. Small().Elements, Component("page", "web", "Page")] };
        var h = b.With(e => e.Id == "repo" ? e with { Description = "Stores orders" } : e);
        var d = SemanticDiff.Compute(b, h);
        Assert.Equal(["modified element 'Repository': description"], View(d, "component:api").Reasons);
        Assert.Equal(ViewStatus.Unchanged, View(d, "component:web").Status);
        Assert.False(View(d, "container:sys").Affected);
    }

    [Fact]
    public void A_view_that_appears_is_marked_added()
    {
        var head = Small() with { Elements = [.. Small().Elements, Component("page", "web", "Page")] };
        var d = SemanticDiff.Compute(Small(), head);
        var v = View(d, "component:web");
        Assert.Equal(ViewStatus.Added, v.Status);
        Assert.True(v.Affected);
    }
}
