using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ComposeDraftTests
{
    private static Func<string> Ids()
    {
        var n = 0;
        return () => $"id-{++n}";
    }

    private static ModelSnapshot Draft(ModelSnapshot current, string yaml, string system = "sys") =>
        ComposeDraft.Build(current, ComposeFile.Parse(yaml).Value, system, Ids()).Value;

    private static Element ByService(ModelSnapshot s, string service) => s.Elements.Single(e => e.Property("composeService") == service);

    [Fact]
    public void New_service_becomes_a_container_with_technology_from_the_image()
    {
        var draft = Draft(Small(), """
            services:
              session-cache:
                image: docker.io/library/redis:7.2
            """);
        var cache = ByService(draft, "session-cache");
        Assert.Equal(("id-1", "Session Cache", "docker.io/library/redis", "sys"), (cache.Id, cache.Name, cache.Technology, cache.ParentId));
        Assert.Contains("deployed", cache.Tags);
    }

    [Fact]
    public void Built_service_gets_Docker_as_technology()
    {
        var draft = Draft(Small(), "services:\n  worker:\n    build: ./worker\n");
        Assert.Equal("Docker", ByService(draft, "worker").Technology);
    }

    [Fact]
    public void Depends_on_becomes_a_Uses_relationship()
    {
        var draft = Draft(Small(), """
            services:
              worker:
                build: .
                depends_on: [db]
              db:
                image: postgres:17
            """);
        var worker = ByService(draft, "worker");
        var rel = Assert.Single(draft.Relationships, r => r.SourceId == worker.Id);
        Assert.Equal(("db", "Uses"), (rel.TargetId, rel.Description));
        Assert.DoesNotContain(draft.Relationships, r => r.Id.StartsWith("r-worker", StringComparison.Ordinal));
    }

    [Fact]
    public void Existing_relationship_is_not_duplicated()
    {
        var draft = Draft(Small(), "services:\n  web:\n    depends_on: [api]\n  api:\n    depends_on: [web]\n");
        Assert.Equal(Small().Relationships.Select(r => r.Id), draft.Relationships.Select(r => r.Id));
    }

    [Fact]
    public void Draft_never_deletes_anything()
    {
        var draft = Draft(Small(), "services:\n  cache:\n    image: redis\n  web: {}\n");
        var diff = SemanticDiff.Compute(Small(), draft);
        Assert.Empty(diff.Elements.Removed);
        Assert.Empty(diff.Elements.Modified);
        Assert.Empty(diff.Relationships.Removed);
        Assert.Empty(diff.Relationships.Modified);
        Assert.Equal(["Cache"], diff.Elements.Added.Select(e => e.Name));
        Assert.All(Small().Elements, e => Assert.Equal(e, draft.Elements.Single(d => d.Id == e.Id)));
    }

    [Fact]
    public void ShopFlow_drift_compose_adds_only_the_worker_to_head()
    {
        var head = Reference("head.json");
        var draft = Draft(head, File.ReadAllText(ReferencePath("shopflow", "compose-drift.yml")), "shop");
        var diff = SemanticDiff.Compute(head, draft);
        Assert.Equal(["Worker"], diff.Elements.Added.Select(e => e.Name));
        Assert.Equal("worker", ByService(draft, "worker").Property("composeService"));
        Assert.Empty(diff.Relationships.Added);
    }

    [Fact]
    public void Unknown_target_system_is_an_error_value()
    {
        var result = ComposeDraft.Build(Small(), ComposeFile.Parse("services: {}").Value, "nope", Ids());
        Assert.Equal(["the model has no element 'nope'"], result.Descriptions());
    }

    [Fact]
    public void Container_as_target_is_reported_as_the_wrong_kind()
    {
        var result = ComposeDraft.Build(Small(), ComposeFile.Parse("services: {}").Value, "api", Ids());
        Assert.Equal(["'API' — container, and only a software system can be the target"], result.Descriptions());
    }

    [Theory]
    [InlineData("api-gateway", "Api Gateway")]
    [InlineData("messageBus", "Message Bus")]
    [InlineData("worker", "Worker")]
    public void Service_name_becomes_a_title(string service, string title) => Assert.Equal(title, ComposeDraft.TitleCase(service));
}
