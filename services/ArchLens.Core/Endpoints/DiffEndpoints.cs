using ArchLens.Core.Application;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Graph;

namespace ArchLens.Core.Endpoints;

/// <summary>Semantic diff, by version ids or stateless.</summary>
public static class DiffEndpoints
{
    public static RouteGroupBuilder MapDiffEndpoints(this RouteGroupBuilder api)
    {
        var diff = api.MapGroup("").WithTags("Diff");
        diff.MapGet("/models/{modelId:guid}/diff", async (Guid modelId, Guid from, Guid to, HttpContext http, Analyses svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.Diff(http.Caller(), modelId, from, to, ct)))
            .WithName("diffVersions").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        diff.MapPost("/diff", (DiffRequest body) => ApiResults.Ok(Analyses.Diff(body)))
            .WithName("diffSnapshots").ProducesValidationProblem().ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        return api;
    }
}
