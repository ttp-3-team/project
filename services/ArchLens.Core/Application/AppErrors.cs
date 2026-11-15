using ErrorOr;

namespace ArchLens.Core.Application;

/// <summary>The expected failures of Core's application operations, as ErrorOr errors.</summary>
public static class AppErrors
{
    public const string TitleKey = "title";

    /// <summary>The resource does not exist or belongs to another user (indistinguishable on purpose): 404.</summary>
    public static Error NotFound(string what) => Error.NotFound("archlens.not_found", $"{what} not found");

    /// <summary>The request conflicts with current state, e.g. a stale parent version: 409.</summary>
    public static Error Conflict(string detail) => Error.Conflict("archlens.conflict", detail);

    /// <summary>The request names something unusable (bad compose file, wrong target): 400 with a specific title.</summary>
    public static Error BadRequest(string title, string detail) =>
        Error.Validation("archlens.bad_request", detail, new Dictionary<string, object> { [TitleKey] = title });
}
