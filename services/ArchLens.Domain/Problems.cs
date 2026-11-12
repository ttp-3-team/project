using ErrorOr;

namespace ArchLens.Domain;

/// <summary>
/// Expected failures are <see cref="ErrorOr{TValue}"/> values. A model problem is a validation error whose description
/// is the human-readable text shown to people.
/// </summary>
public static class Problems
{
    /// <summary>Code of every model problem.</summary>
    public const string Code = "archlens.problem";

    /// <summary>One problem.</summary>
    public static Error Of(string description) => Error.Validation(Code, description);

    /// <summary>Several problems.</summary>
    public static List<Error> Of(IEnumerable<string> descriptions) => descriptions.Select(Of).ToList();

    /// <summary>The problem texts of a failed result; empty for a success.</summary>
    public static IReadOnlyList<string> Descriptions<T>(this ErrorOr<T> result) =>
        result.IsError ? result.Errors.Select(e => e.Description).ToList() : [];
}
