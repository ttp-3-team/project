using ArchLens.Core.Application;
using ArchLens.Core.Contracts;
using ArchLens.Domain.Graph;

namespace ArchLens.Core.Endpoints;

/// <summary>Drafts proposed by analyses (docker-compose today).</summary>
public static class DraftEndpoints
{
    public static RouteGroupBuilder MapDraftEndpoints(this RouteGroupBuilder api)
    {
        api.MapPost("/models/{modelId:guid}/drafts/compose", async (Guid modelId, ComposeDraftRequest body, HttpContext http, Analyses svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.FromCompose(http.Caller(), modelId, body, ct)))
            .WithTags("Drafts").WithName("createComposeDraft").ProducesValidationProblem().ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }
}
