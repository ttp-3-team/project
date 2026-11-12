using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class SemanticDiffTests
{
    private static (string Field, string From, string To)[] Changes(ElementModified m) =>
        m.Changes.Select(c => (c.Field, c.From, c.To)).ToArray();

    [Fact]
    public void Identical_models_give_an_empty_diff()
    {
        var d = SemanticDiff.Compute(Small(), Small());
        Assert.True(d.IsEmpty);
    }

    [Fact]
    public void Added_element_is_reported()
    {
        var head = Small() with { Elements = [.. Small().Elements, Container("cache", "sys", "Cache", "Redis")] };
        var d = SemanticDiff.Compute(Small(), head);
        Assert.Equal(new ElementRef("cache", ElementKind.Container, "Cache", "sys"), Assert.Single(d.Elements.Added));
        Assert.Empty(d.Elements.Removed);
    }

    [Fact]
    public void Removed_element_is_reported()
    {
        var head = Small() with { Elements = Small().Elements.Where(e => e.Id != "repo").ToList(), Relationships = Small().Relationships.Where(r => r.SourceId != "repo" && r.TargetId != "repo").ToList() };
        var d = SemanticDiff.Compute(Small(), head);
        Assert.Equal("repo", Assert.Single(d.Elements.Removed).Id);
        Assert.Equal(["r-ctrl-repo", "r-repo-db"], d.Relationships.Removed.Select(r => r.Id));
    }

    [Fact]
    public void Rename_is_one_modification_not_remove_and_add()
    {
        var head = Small().With(e => e.Id == "web" ? e with { Name = "Storefront" } : e);
        var d = SemanticDiff.Compute(Small(), head);
        Assert.Empty(d.Elements.Added);
        Assert.Empty(d.Elements.Removed);
        var m = Assert.Single(d.Elements.Modified);
        Assert.Equal("web", m.Id);
        Assert.Equal([("name", "Web", "Storefront")], Changes(m));
    }

    [Fact]
    public void Technology_change_is_a_field_change()
    {
        var head = Small().With(e => e.Id == "api" ? e with { Technology = "Rust" } : e);
        var m = Assert.Single(SemanticDiff.Compute(Small(), head).Elements.Modified);
        Assert.Equal([("technology", "Go", "Rust")], Changes(m));
    }

    [Fact]
    public void Tags_compare_as_sets()
    {
        var b = Small().With(e => e.Id == "db" ? e with { Tags = ["Database", "deployed"] } : e);
        var reordered = Small().With(e => e.Id == "db" ? e with { Tags = ["deployed", "Database", "deployed"] } : e);
        Assert.True(SemanticDiff.Compute(b, reordered).IsEmpty);

        var changed = Small().With(e => e.Id == "db" ? e with { Tags = ["Database"] } : e);
        var m = Assert.Single(SemanticDiff.Compute(b, changed).Elements.Modified);
        Assert.Equal([("tags", "Database, deployed", "Database")], Changes(m));
    }

    [Fact]
    public void Marking_a_system_external_is_a_field_change()
    {
        var head = Small().With(e => e.Id == "ext" ? e with { External = false } : e);
        var b = Small().With(e => e.Id == "ext" ? e with { External = true } : e);
        var m = Assert.Single(SemanticDiff.Compute(b, head).Elements.Modified);
        Assert.Equal([("external", "true", "false")], Changes(m));
    }

    [Fact]
    public void Moving_a_component_to_another_container_is_a_parent_change()
    {
        var head = Small().With(e => e.Id == "repo" ? e with { ParentId = "web" } : e);
        var m = Assert.Single(SemanticDiff.Compute(Small(), head).Elements.Modified);
        Assert.Equal([("parentId", "api", "web")], Changes(m));
    }

    [Fact]
    public void Relationships_are_added_removed_and_modified_by_id()
    {
        var head = Small() with
        {
            Relationships = Small().Relationships
                .Where(r => r.Id != "r-api-ext")
                .Select(r => r.Id == "r-web-api" ? r with { Technology = "gRPC", Description = "Invokes" } : r)
                .Append(Rel("user", "ext", "Pays"))
                .ToList(),
        };
        var d = SemanticDiff.Compute(Small(), head);
        Assert.Equal(["r-user-ext"], d.Relationships.Added.Select(r => r.Id));
        Assert.Equal(["r-api-ext"], d.Relationships.Removed.Select(r => r.Id));
        var m = Assert.Single(d.Relationships.Modified);
        Assert.Equal([("description", "Calls", "Invokes"), ("technology", "HTTPS", "gRPC")], m.Changes.Select(c => (c.Field, c.From, c.To)));
    }

    [Fact]
    public void Layout_only_change_gives_an_empty_diff()
    {
        var head = Small() with
        {
            Layout = new Dictionary<string, IReadOnlyDictionary<string, Point>>
            {
                ["context"] = new Dictionary<string, Point> { ["sys"] = new(100, 200) },
            },
        };
        Assert.True(SemanticDiff.Compute(Small(), head).IsEmpty);
    }

    [Fact]
    public void Diff_is_deterministic_byte_for_byte()
    {
        var b = Reference("base.json");
        var h = Reference("head.json");
        var shuffled = h with { Elements = h.Elements.Reverse().ToList(), Relationships = h.Relationships.Reverse().ToList() };
        var first = SnapshotJson.Serialize(SemanticDiff.Compute(b, h));
        var second = SnapshotJson.Serialize(SemanticDiff.Compute(b, shuffled));
        Assert.Equal(first, second);
    }
}
