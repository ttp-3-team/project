using ArchLens.Core.Application;
using ArchLens.Core.Contracts;

namespace ArchLens.Core.Endpoints;

/// <summary>Immutable versions. There are deliberately no update or delete endpoints.</summary>
public static class VersionEndpoints
{
    public static RouteGroupBuilder MapVersionEndpoints(this RouteGroupBuilder api)
    {
        var versions = api.MapGroup("").WithTags("Versions");
        versions.MapGet("/models/{modelId:guid}/versions", async (Guid modelId, HttpContext http, Versions svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.List(http.Caller(), modelId, ct)))
            .WithName("listVersions").ProducesProblem(StatusCodes.Status404NotFound);
        versions.MapPost("/models/{modelId:guid}/versions", async (Guid modelId, CreateVersionRequest body, HttpContext http, Versions svc, CancellationToken ct) =>
                ApiResults.Created(await svc.Create(http.Caller(), modelId, body, ct), v => $"/api/v1/versions/{v.Id}"))
            .WithName("createVersion").ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status404NotFound).ProducesProblem(StatusCodes.Status409Conflict).ProducesProblem(StatusCodes.Status422UnprocessableEntity);
        versions.MapGet("/versions/{versionId:guid}", async (Guid versionId, HttpContext http, Versions svc, CancellationToken ct) =>
                ApiResults.Ok(await svc.Get(http.Caller(), versionId, ct)))
            .WithName("getVersion").ProducesProblem(StatusCodes.Status404NotFound);
        return api;
    }
}
