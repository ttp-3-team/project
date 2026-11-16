using ArchLens.Core.Application;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Diff;

namespace ArchLens.Core.Endpoints;

/// <summary>Validation, derived views and compare graphs for the web canvas.</summary>
public static class ViewEndpoints
{
    public static RouteGroupBuilder MapViewEndpoints(this RouteGroupBuilder api)
    {
        var views = api.MapGroup("").WithTags("Views");
        views.MapPost("/validate", (SnapshotRequest body) => TypedResults.Ok(Analyses.Validate(body.Snapshot)))
            .WithName("validateSnapshot").ProducesValidationProblem();
        views.MapPost("/views", (SnapshotRequest body) => TypedResults.Ok(Analyses.Views(body.Snapshot)))
            .WithName("deriveViews").ProducesValidationProblem();
        views.MapGet("/models/{modelId:guid}/compare", async (Guid modelId, Guid from, Guid to, string view, HttpContext http, Analyses svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.Compare(http.Caller(), modelId, from, to, view, ct)))
            .WithName("compareVersions").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        views.MapPost("/compare", (CompareRequest body) => ApiResults.Ok(Analyses.Compare(body)))
            .WithName("compareSnapshots").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return api;
    }
}
