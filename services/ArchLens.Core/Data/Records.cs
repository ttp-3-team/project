using ArchLens.Domain.Model;

namespace ArchLens.Core.Data;

/// <summary>Persistence record of a workspace. <see cref="OwnerId"/> is the web user id (JWT <c>sub</c>).</summary>
public sealed class WorkspaceRecord
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public required string OwnerId { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
}

/// <summary>Persistence record of a model.</summary>
public sealed class ModelRecord
{
    public Guid Id { get; init; }
    public Guid WorkspaceId { get; init; }
    public required string Name { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public WorkspaceRecord? Workspace { get; init; }
}

/// <summary>Persistence record of an immutable version. The database rejects UPDATE and DELETE on this table.</summary>
public sealed class VersionRecord
{
    public Guid Id { get; init; }
    public Guid ModelId { get; init; }
    public int Number { get; init; }
    public Guid? ParentVersionId { get; init; }
    public required ModelSnapshot Snapshot { get; init; }
    public required string Message { get; init; }
    public string? CommitSha { get; init; }
    public required string CreatedBy { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public ModelRecord? Model { get; init; }
}
