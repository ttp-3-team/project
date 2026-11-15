using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ArchLens.Domain.Model;

namespace ArchLens.Core.Contracts;

/// <summary>A version without its snapshot.</summary>
public record VersionSummary
{
    public required Guid Id { get; init; }
    public required Guid ModelId { get; init; }
    public required int Number { get; init; }
    public required Guid? ParentVersionId { get; init; }
    public required string Message { get; init; }
    public required string? CommitSha { get; init; }
    public required string CreatedBy { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>An immutable version with its snapshot (export uses this).</summary>
public sealed record ModelVersion : VersionSummary
{
    public required ModelSnapshot Snapshot { get; init; }
}

/// <summary>Body of <c>POST /models/{id}/versions</c>. <c>parentVersionId</c> must be the latest version (null for the first).</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateVersionRequest
{
    public required ModelSnapshot Snapshot { get; init; }

    [StringLength(Limits.MessageLength)]
    public required string Message { get; init; }

    public required Guid? ParentVersionId { get; init; }

    [RegularExpression("^[0-9a-f]{7,40}$")]
    public string? CommitSha { get; init; }
}
