using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ArchLens.Domain.Model;

namespace ArchLens.Core.Contracts;

/// <summary>Body of <c>POST /validate</c> and <c>POST /views</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SnapshotRequest
{
    public required ModelSnapshot Snapshot { get; init; }
}

/// <summary>Result of <c>POST /validate</c>.</summary>
public sealed record ValidationResult
{
    public required bool Ok { get; init; }
    public required IReadOnlyList<string> Problems { get; init; }
}

/// <summary>Body of <c>POST /compare</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CompareRequest
{
    public required ModelSnapshot Base { get; init; }
    public required ModelSnapshot Head { get; init; }

    [StringLength(Limits.IdLength * 2, MinimumLength = 1)]
    public required string View { get; init; }
}
