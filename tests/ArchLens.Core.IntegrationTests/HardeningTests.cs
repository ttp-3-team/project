using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;

namespace ArchLens.Core.IntegrationTests;

[Collection(nameof(CoreCollection))]
public class HardeningTests(CoreFixture core)
{
    public static TheoryData<string, string> BadTokens => new()
    {
        { "missing sub", CoreFixture.Token(null) },
        { "expired", CoreFixture.Token("u", notBefore: DateTime.UtcNow.AddMinutes(-20), lifetime: TimeSpan.FromMinutes(5)) },
        { "wrong audience", CoreFixture.Token("u", audience: "someone-else") },
        { "wrong issuer", CoreFixture.Token("u", issuer: "evil") },
        { "lifetime over five minutes", CoreFixture.Token("u", lifetime: TimeSpan.FromDays(365)) },
        { "signed with another secret", CoreFixture.Token("mallory", "some-other-secret-that-is-long-enough-0123456789") },
    };

    [Theory]
    [MemberData(nameof(BadTokens))]
    public async Task Bad_tokens_are_rejected(string why, string token)
    {
        var response = await core.WithToken(token).GetAsync("/api/v1/workspaces");
        Assert.True(response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden, $"{why}: {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Every_owned_resource_is_hidden_from_other_users()
    {
        var alice = core.As();
        var workspace = await alice.CreateWorkspace();
        var model = await alice.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var v = model.LatestVersionId!.Value;

        var bob = core.As();
        var compose = new { composeYaml = CoreFixture.Reference("compose-drift.yml"), targetSystemId = "shop", baseVersionId = v };
        var responses = new[]
        {
            await bob.GetAsync($"/api/v1/workspaces/{workspace.Id}"),
            await bob.GetAsync($"/api/v1/workspaces/{workspace.Id}/models"),
            await bob.PostAsJsonAsync($"/api/v1/workspaces/{workspace.Id}/models", new { name = "intruder" }, SnapshotJson.Options),
            await bob.GetAsync($"/api/v1/models/{model.Id}"),
            await bob.GetAsync($"/api/v1/models/{model.Id}/versions"),
            await bob.GetAsync($"/api/v1/versions/{v}"),
            await bob.SaveVersion(model.Id, CoreFixture.ReferenceSnapshot("head.json"), v),
            await bob.GetAsync($"/api/v1/models/{model.Id}/diff?from={v}&to={v}"),
            await bob.GetAsync($"/api/v1/models/{model.Id}/compare?from={v}&to={v}&view=context"),
            await bob.PostAsJsonAsync($"/api/v1/models/{model.Id}/drafts/compose", compose, SnapshotJson.Options),
        };
        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Empty(await (await bob.GetAsync("/api/v1/workspaces")).Read<List<Workspace>>());
    }

    public static TheoryData<string, string, string> MalformedBodies => new()
    {
        { "null body", "/api/v1/diff", "null" },
        { "empty diff", "/api/v1/diff", "{}" },
        { "unknown member", "/api/v1/workspaces", """{"name":"x","owner":"me"}""" },
        { "name too long", "/api/v1/workspaces", $$"""{"name":"{{new string('x', 201)}}"}""" },
        { "unknown element kind", "/api/v1/validate", """{"snapshot":{"schemaVersion":1,"elements":[{"id":"a","kind":"robot","name":"A"}],"relationships":[],"layout":{}}}""" },
        { "snapshot without layout", "/api/v1/validate", """{"snapshot":{"schemaVersion":1,"elements":[],"relationships":[]}}""" },
        { "not json", "/api/v1/views", "{ nope" },
    };

    [Theory]
    [MemberData(nameof(MalformedBodies))]
    public async Task Malformed_bodies_are_400_problems(string why, string path, string body)
    {
        var response = await core.As().PostAsync(path, new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, $"{why}: {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Version_without_snapshot_is_rejected()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var response = await client.PostAsJsonAsync($"/api/v1/models/{model.Id}/versions", new { message = "empty", parentVersionId = model.LatestVersionId }, SnapshotJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Non_uuid_query_parameter_is_400()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync($"/api/v1/models/{model.Id}/diff?from=x&to=y")).StatusCode);
    }

    [Theory]
    [InlineData("services: [1, 2", "shop", "Invalid docker-compose")]
    [InlineData("services: {}", "apiGateway", "Invalid target system")]
    [InlineData("services: {}", "nope", "Invalid target system")]
    public async Task Compose_draft_errors_are_400_with_specific_titles(string yaml, string target, string title)
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var response = await client.PostAsJsonAsync($"/api/v1/models/{model.Id}/drafts/compose",
            new { composeYaml = yaml, targetSystemId = target, baseVersionId = model.LatestVersionId }, SnapshotJson.Options);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(title, JsonNode.Parse(await response.Content.ReadAsStringAsync())!["title"]!.GetValue<string>());
    }
}

[Collection(nameof(CoreCollection))]
public class ViewsApiTests(CoreFixture core)
{
    [Fact]
    public async Task Validate_reports_problems()
    {
        var broken = CoreFixture.ReferenceSnapshot("base.json") with { Relationships = [new Relationship { Id = "r", SourceId = "customer", TargetId = "customer" }] };
        var result = await (await core.As().PostAsJsonAsync("/api/v1/validate", new { snapshot = broken }, SnapshotJson.Options)).Read<ValidationResult>();
        Assert.False(result.Ok);
        Assert.Equal(["relationship 'r' cannot connect an element to itself"], result.Problems);
    }

    [Fact]
    public async Task Views_return_the_ShopFlow_graphs()
    {
        var views = await (await core.As().PostAsJsonAsync("/api/v1/views", new { snapshot = CoreFixture.ReferenceSnapshot("base.json") }, SnapshotJson.Options)).Read<List<ViewGraph>>();
        var container = views.Single(v => v.View.Key == "container:shop");
        Assert.Equal(14, container.Nodes.Count);
        Assert.Equal("System context", Assert.Single(container.View.Ancestors).Title);
        Assert.Equal("component:orderService", container.Nodes.Single(n => n.ElementId == "orderService").ChildViewKey);
        Assert.Contains(views, v => v.View.Key == "component:apiGateway");
    }

    [Fact]
    public async Task Compare_by_versions_and_by_snapshots_agree()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var v2 = await (await client.SaveVersion(model.Id, CoreFixture.ReferenceSnapshot("head.json"), model.LatestVersionId)).Read<VersionSummary>();

        var byIds = await (await client.GetAsync($"/api/v1/models/{model.Id}/compare?from={model.LatestVersionId}&to={v2.Id}&view=container%3Ashop")).Read<CompareGraph>();
        var bySnapshots = await (await client.PostAsJsonAsync("/api/v1/compare",
            new { @base = CoreFixture.ReferenceSnapshot("base.json"), head = CoreFixture.ReferenceSnapshot("head.json"), view = "container:shop" }, SnapshotJson.Options)).Read<CompareGraph>();
        Assert.Equal(SnapshotJson.Serialize(byIds), SnapshotJson.Serialize(bySnapshots));

        var mobile = byIds.Nodes.Single(n => n.ElementId == "mobileApp");
        Assert.Equal(ChangeStatus.Modified, mobile.Status);
        Assert.Equal(new FieldChange("name", "Mobile App", "Mobile Storefront"), mobile.Changes[0]);
        Assert.Equal(ChangeStatus.Added, byIds.Nodes.Single(n => n.ElementId == "cache").Status);
        Assert.Equal(ChangeStatus.Removed, byIds.Edges.Single(e => e.Id == "notificationService|mobileApp").Status);
        Assert.NotNull(mobile.Position);
    }

    [Fact]
    public async Task Compare_of_an_unknown_view_is_404()
    {
        var s = CoreFixture.ReferenceSnapshot("base.json");
        var response = await core.As().PostAsJsonAsync("/api/v1/compare", new { @base = s, head = s, view = "component:nope" }, SnapshotJson.Options);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
