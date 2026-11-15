using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ArchLens.Domain.Model;

namespace ArchLens.Core.Contracts;

/// <summary>Body of <c>POST /diff</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record DiffRequest
{
    public required ModelSnapshot Base { get; init; }
    public required ModelSnapshot Head { get; init; }
}

/// <summary>Body of <c>POST /models/{id}/drafts/compose</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ComposeDraftRequest
{
    [StringLength(Limits.ComposeLength, MinimumLength = 1)]
    public required string ComposeYaml { get; init; }

    [StringLength(Limits.IdLength, MinimumLength = 1)]
    public required string TargetSystemId { get; init; }

    public required Guid BaseVersionId { get; init; }
}
