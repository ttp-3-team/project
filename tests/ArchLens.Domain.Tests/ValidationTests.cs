using ArchLens.Domain.Model;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ValidationTests
{
    [Fact]
    public void ShopFlow_reference_models_are_valid()
    {
        Assert.Empty(SnapshotValidator.Validate(Reference("base.json")));
        Assert.Empty(SnapshotValidator.Validate(Reference("head.json")));
    }

    [Fact]
    public void Duplicate_element_id_is_reported()
    {
        var s = Snapshot([Sys("sys"), Person("sys")]);
        Assert.Contains("duplicate element id 'sys'", SnapshotValidator.Validate(s));
    }

    [Fact]
    public void Container_inside_a_container_is_a_bad_parent_kind()
    {
        var s = Snapshot([Sys("sys"), Container("a", "sys"), Container("b", "a")]);
        var problem = Assert.Single(SnapshotValidator.Validate(s));
        Assert.Contains("'b' (container) has parent 'a' of kind container, expected softwareSystem", problem);
    }

    [Fact]
    public void Uuid_ids_are_valid()
    {
        var s = Snapshot([Person("6f1c2a64-3b0e-4f8e-9a51-0c2d7f6b9e11")]);
        Assert.Empty(SnapshotValidator.Validate(s));
    }

    private static readonly Dictionary<string, (ModelSnapshot Snapshot, string[] Problems)> Invalid = new()
    {
        ["person with a parent"] = (Snapshot([Sys("sys"), Person("p") with { ParentId = "sys" }]), ["element 'p' (person) cannot have a parent"]),
        ["relationship to an ancestor"] = (
            Snapshot([Sys("sys"), Container("api", "sys"), Component("ctrl", "api")], [Rel("ctrl", "sys"), Rel("api", "ctrl")]),
            ["relationship 'r-api-ctrl' cannot connect an element to its parent", "relationship 'r-ctrl-sys' cannot connect an element to its parent"]),
        ["dangling relationship"] = (Snapshot([Person("p")], [Rel("p", "ghost")]), ["relationship 'r-p-ghost' has a missing target 'ghost'"]),
        ["self relationship"] = (Snapshot([Person("p")], [Rel("p", "p")]), ["relationship 'r-p-p' cannot connect an element to itself"]),
    };

    public static TheoryData<string> InvalidCases => new(Invalid.Keys);

    [Theory]
    [MemberData(nameof(InvalidCases))]
    public void Invalid_snapshot_reports_exactly_its_problems(string name)
    {
        var (snapshot, problems) = Invalid[name];
        Assert.Equal(problems, SnapshotValidator.Validate(snapshot));
    }

    [Fact]
    public void Json_round_trip_keeps_the_snapshot()
    {
        var original = Reference("head.json");
        var json = SnapshotJson.Serialize(original);
        var again = SnapshotJson.Parse(json).Value;
        Assert.Equal(json, SnapshotJson.Serialize(again));
    }

    [Fact]
    public void Malformed_json_is_a_value_not_an_exception()
    {
        var result = SnapshotJson.Parse("{ not json");
        Assert.True(result.IsError);
    }
}
