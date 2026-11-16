using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using Npgsql;

namespace ArchLens.Core.IntegrationTests;

[Collection(nameof(CoreCollection))]
public class CoreApiTests(CoreFixture core)
{
    [Fact]
    public async Task Requests_without_a_token_are_rejected_but_health_is_public()
    {
        var anonymous = core.Anonymous();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/v1/workspaces")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync("/health")).StatusCode);
    }

    [Fact]
    public async Task Workspace_then_model_from_template_creates_version_1()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var template = await (await client.GetAsync("/api/v1/templates/shopflow")).Read<ModelSnapshot>();
        var model = await client.CreateModel(workspace.Id, template);

        Assert.Equal(1, model.LatestVersionNumber);
        var versions = await (await client.GetAsync($"/api/v1/models/{model.Id}/versions")).Read<List<VersionSummary>>();
        var v1 = Assert.Single(versions);
        Assert.Null(v1.ParentVersionId);
        Assert.Single(await (await client.GetAsync("/api/v1/workspaces")).Read<List<Workspace>>());
    }

    [Fact]
    public async Task Served_OpenAPI_document_is_the_committed_contract()
    {
        var served = await core.Anonymous().GetStringAsync("/openapi/v1.json");
        await Verify(JsonNode.Parse(served)!.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }), "json");
    }

    [Fact]
    public async Task Diff_between_saved_versions_reports_rename_as_one_modification()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var head = CoreFixture.ReferenceSnapshot("head.json");
        var v2 = await (await client.SaveVersion(model.Id, head, model.LatestVersionId)).Read<VersionSummary>();
        Assert.Equal(2, v2.Number);

        var diff = await (await client.GetAsync($"/api/v1/models/{model.Id}/diff?from={model.LatestVersionId}&to={v2.Id}")).Read<DiffResult>();
        var modified = Assert.Single(diff.Elements.Modified);
        Assert.Equal("mobileApp", modified.Id);
        Assert.Empty(diff.Elements.Removed);

        await Verify(SnapshotJson.Serialize(diff, indented: true), "json");
    }

    [Fact]
    public async Task Stale_parent_version_is_a_conflict()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var v1 = model.LatestVersionId;
        Assert.Equal(HttpStatusCode.Created, (await client.SaveVersion(model.Id, CoreFixture.ReferenceSnapshot("head.json"), v1)).StatusCode);

        var stale = await client.SaveVersion(model.Id, CoreFixture.ReferenceSnapshot("head.json"), v1, "based on v1 again");
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task Invalid_snapshot_is_rejected_with_the_list_of_problems()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));
        var broken = CoreFixture.ReferenceSnapshot("base.json") with
        {
            Relationships = [new Relationship { Id = "r-x", SourceId = "customer", TargetId = "nowhere" }],
        };
        var response = await client.SaveVersion(model.Id, broken, model.LatestVersionId);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        Assert.Contains("missing target 'nowhere'", problem["problems"]![0]!.GetValue<string>());
    }

    [Fact]
    public async Task Database_trigger_rejects_update_and_delete_of_versions()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("base.json"));

        await using var connection = new NpgsqlConnection(core.ConnectionString);
        await connection.OpenAsync();
        foreach (var sql in new[] { "UPDATE versions SET message = 'tampered' WHERE id = @id", "DELETE FROM versions WHERE id = @id" })
        {
            await using var command = new NpgsqlCommand(sql, connection);
            command.Parameters.AddWithValue("id", model.LatestVersionId!.Value);
            var error = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Contains("versions are immutable", error.MessageText);
        }
    }

    [Fact]
    public async Task Compose_draft_returns_draft_diff_and_drift()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var model = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("head.json"));

        var response = await client.PostAsJsonAsync($"/api/v1/models/{model.Id}/drafts/compose",
            new { composeYaml = CoreFixture.Reference("compose-drift.yml"), targetSystemId = "shop", baseVersionId = model.LatestVersionId },
            SnapshotJson.Options);
        var result = await response.Read<ComposeDraftResult>();

        Assert.False(result.Drift.Ok);
        Assert.Equal(["worker"], result.Drift.ServicesWithoutContainer);
        var worker = Assert.Single(result.Draft.Elements, e => e.Property("composeService") == "worker");
        Assert.True(Guid.TryParse(worker.Id, out _), "new ids are opaque UUIDs");
        Assert.Equal([worker.Id], result.Diff.Elements.Added.Select(e => e.Id));
    }

    [Fact]
    public async Task Exported_version_reimports_without_loss()
    {
        var client = core.As();
        var workspace = await client.CreateWorkspace();
        var original = await client.CreateModel(workspace.Id, CoreFixture.ReferenceSnapshot("head.json"));
        var exported = await (await client.GetAsync($"/api/v1/versions/{original.LatestVersionId}")).Read<ModelVersion>();

        var imported = await client.CreateModel(workspace.Id, exported.Snapshot, "ShopFlow (import)");
        var reimported = await (await client.GetAsync($"/api/v1/versions/{imported.LatestVersionId}")).Read<ModelVersion>();
        Assert.Equal(SnapshotJson.Serialize(exported.Snapshot), SnapshotJson.Serialize(reimported.Snapshot));

        var diff = await (await client.PostAsJsonAsync("/api/v1/diff", new { @base = exported.Snapshot, head = reimported.Snapshot }, SnapshotJson.Options)).Read<DiffResult>();
        Assert.True(diff.IsEmpty);
    }
}
