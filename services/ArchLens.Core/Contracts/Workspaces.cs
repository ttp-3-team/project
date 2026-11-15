using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using ArchLens.Domain.Model;

namespace ArchLens.Core.Contracts;

/// <summary>A workspace owned by one user.</summary>
public sealed record Workspace
{
    public required Guid Id { get; init; }
    public required string Name { get; init; }
    public required string OwnerId { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Body of <c>POST /workspaces</c>.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkspaceRequest
{
    [StringLength(Limits.NameLength, MinimumLength = 1)]
    public required string Name { get; init; }
}

/// <summary>A model with its latest version (null when it has none yet).</summary>
public sealed record Model
{
    public required Guid Id { get; init; }
    public required Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public required DateTimeOffset CreatedAt { get; init; }
    public required Guid? LatestVersionId { get; init; }
    public required int? LatestVersionNumber { get; init; }
}

/// <summary>Body of <c>POST /workspaces/{id}/models</c>. With a snapshot (import or template), version 1 is created from it.</summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateModelRequest
{
    [StringLength(Limits.NameLength, MinimumLength = 1)]
    public required string Name { get; init; }

    public ModelSnapshot? Snapshot { get; init; }

    [StringLength(Limits.MessageLength)]
    public string? Message { get; init; }
}

/// <summary>Size limits shared by the request records.</summary>
public static class Limits
{
    public const int NameLength = 200;
    public const int MessageLength = 2000;
    public const int IdLength = 128;
    public const int ComposeLength = 500_000;
}
