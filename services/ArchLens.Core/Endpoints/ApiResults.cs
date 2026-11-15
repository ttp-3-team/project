using ArchLens.Core.Application;
using ArchLens.Core.Auth;
using ArchLens.Domain;
using ErrorOr;
using Microsoft.AspNetCore.Http.HttpResults;

namespace ArchLens.Core.Endpoints;

internal static class ApiResults
{
    /// <summary>200 with the value, or the problem for the errors.</summary>
    public static Results<Ok<T>, ProblemHttpResult> Ok<T>(ErrorOr<T> result) =>
        result.IsError ? Problem(result.Errors) : TypedResults.Ok(result.Value);

    /// <summary>201 with the value and its location, or the problem for the errors.</summary>
    public static Results<Created<T>, ProblemHttpResult> Created<T>(ErrorOr<T> result, Func<T, string> location) =>
        result.IsError ? Problem(result.Errors) : TypedResults.Created(location(result.Value), result.Value);

    /// <summary>The one mapping from errors to problem details (titles and the <c>problems</c> extension the web reads).</summary>
    public static ProblemHttpResult Problem(List<Error> errors)
    {
        var first = errors[0];
        if (errors.All(e => e.Code == Problems.Code))
            return TypedResults.Problem(
                statusCode: StatusCodes.Status422UnprocessableEntity,
                title: "Invalid model",
                detail: "The model violates the model rules.",
                extensions: new Dictionary<string, object?> { ["problems"] = errors.Select(e => e.Description).ToList() });
        return first.Type switch
        {
            ErrorType.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found", detail: first.Description),
            ErrorType.Conflict => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: "Conflict", detail: first.Description),
            ErrorType.Validation => TypedResults.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: first.Metadata?.GetValueOrDefault(AppErrors.TitleKey) as string ?? "Bad request",
                detail: first.Description),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Internal error"),
        };
    }

    /// <summary>The caller parsed from the token. The fallback policy requires <c>sub</c>, so a missing one is a defect.</summary>
    public static CurrentUser Caller(this HttpContext http) =>
        CurrentUser.From(http.User) ?? throw new InvalidOperationException("authenticated principal without sub claim");
}
