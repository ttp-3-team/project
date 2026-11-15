using ArchLens.Core.Application;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Model;

namespace ArchLens.Core.Endpoints;

/// <summary>Workspaces, models and templates.</summary>
public static class WorkspaceEndpoints
{
    public static RouteGroupBuilder MapWorkspaceEndpoints(this RouteGroupBuilder api)
    {
        var workspaces = api.MapGroup("/workspaces").WithTags("Workspaces");
        workspaces.MapGet("/", async (HttpContext http, Workspaces svc, CancellationToken ct) =>
                TypedResults.Ok(await svc.List(http.Caller(), ct)))
            .WithName("listWorkspaces");
        workspaces.MapPost("/", async (CreateWorkspaceRequest body, HttpContext http, Workspaces svc, CancellationToken ct) =>
                ApiResults.Created(await svc.Create(http.Caller(), body, ct), w => $"/api/v1/workspaces/{w.Id}"))
            .WithName("createWorkspace").ProducesValidationProblem();
        workspaces.MapGet("/{workspaceId:guid}", async (Guid workspaceId, HttpContext http, Workspaces svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.Get(http.Caller(), workspaceId, ct)))
            .WithName("getWorkspace").ProducesProblem(StatusCodes.Status404NotFound);

        var models = api.MapGroup("").WithTags("Models");
        models.MapGet("/workspaces/{workspaceId:guid}/models", async (Guid workspaceId, HttpContext http, Workspaces svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.ListModels(http.Caller(), workspaceId, ct)))
            .WithName("listModels").ProducesProblem(StatusCodes.Status404NotFound);
        models.MapPost("/workspaces/{workspaceId:guid}/models", async (Guid workspaceId, CreateModelRequest body, HttpContext http, Workspaces svc, CancellationToken ct) =>
                ApiResults.Created(await svc.CreateModel(http.Caller(), workspaceId, body, ct), m => $"/api/v1/models/{m.Id}"))
            .WithName("createModel").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        models.MapGet("/models/{modelId:guid}", async (Guid modelId, HttpContext http, Workspaces svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.GetModel(http.Caller(), modelId, ct)))
            .WithName("getModel").ProducesProblem(StatusCodes.Status404NotFound);

        api.MapGet("/templates/shopflow", () => TypedResults.Text(Templates.ShopFlowJson, "application/json"))
            .WithTags("Templates").WithName("getShopFlowTemplate").Produces<ModelSnapshot>();
        return api;
    }
}

/// <summary>Reference templates embedded from <c>reference-models/</c>.</summary>
public static class Templates
{
    public static readonly string ShopFlowJson = Read("templates/shopflow.json");

    private static string Read(string name)
    {
        using var stream = typeof(Templates).Assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"embedded template {name} is missing");
        return new StreamReader(stream).ReadToEnd();
    }
}
