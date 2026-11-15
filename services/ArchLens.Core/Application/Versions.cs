using System.Linq.Expressions;
using ErrorOr;
using ArchLens.Domain;
using ArchLens.Core.Auth;
using ArchLens.Core.Contracts;
using ArchLens.Core.Data;
using ArchLens.Domain.Model;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace ArchLens.Core.Application;

/// <summary>
/// Immutable versions of a model. A new version must name the latest version as its parent (optimistic
/// concurrency); the number is max + 1 inside a transaction and the unique (model_id, number) index is the backstop.
/// </summary>
public sealed class Versions(ArchLensDbContext db, Workspaces workspaces, TimeProvider clock)
{
    public async Task<ErrorOr<IReadOnlyList<VersionSummary>>> List(CurrentUser user, Guid modelId, CancellationToken ct)
    {
        if (!await workspaces.OwnedModels(user).AnyAsync(m => m.Id == modelId, ct)) return AppErrors.NotFound("model");
        var rows = await db.Versions.Where(v => v.ModelId == modelId).OrderByDescending(v => v.Number).Select(ToSummary).ToListAsync(ct);
        return rows;
    }

    public async Task<ErrorOr<VersionSummary>> Create(CurrentUser user, Guid modelId, CreateVersionRequest request, CancellationToken ct)
    {
        if (!await workspaces.OwnedModels(user).AnyAsync(m => m.Id == modelId, ct)) return AppErrors.NotFound("model");
        var snapshot = SnapshotValidator.Check(request.Snapshot);
        if (snapshot.IsError) return Problems.Of(snapshot.Descriptions());

        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var latest = await db.Versions.Where(v => v.ModelId == modelId).OrderByDescending(v => v.Number)
            .Select(v => new { v.Id, v.Number }).FirstOrDefaultAsync(ct);
        if (latest?.Id != request.ParentVersionId)
            return AppErrors.Conflict(latest is null
                ? "the model has no versions yet; parentVersionId must be null"
                : $"parentVersionId is not the latest version (latest is v{latest.Number}, {latest.Id})");

        var record = new VersionRecord
        {
            Id = Guid.CreateVersion7(),
            ModelId = modelId,
            Number = (latest?.Number ?? 0) + 1,
            ParentVersionId = latest?.Id,
            Snapshot = snapshot.Value,
            Message = request.Message.Trim(),
            CommitSha = request.CommitSha,
            CreatedBy = user.Id,
            CreatedAt = clock.GetUtcNow(),
        };
        db.Versions.Add(record);
        try
        {
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return AppErrors.Conflict("another version was saved concurrently; reload and retry");
        }
        return ToSummaryCompiled(record);
    }

    public async Task<ErrorOr<ModelVersion>> Get(CurrentUser user, Guid versionId, CancellationToken ct)
    {
        var record = await OwnedVersions(user).FirstOrDefaultAsync(v => v.Id == versionId, ct);
        return record is null ? AppErrors.NotFound("version") : Full(record);
    }

    internal async Task<ModelSnapshot?> Snapshot(CurrentUser user, Guid modelId, Guid versionId, CancellationToken ct) =>
        (await OwnedVersions(user).Where(v => v.Id == versionId && v.ModelId == modelId).Select(v => v.Snapshot).ToListAsync(ct)).FirstOrDefault();

    private IQueryable<VersionRecord> OwnedVersions(CurrentUser user) =>
        db.Versions.AsNoTracking().Where(v => v.Model!.Workspace!.OwnerId == user.Id);

    private static readonly Expression<Func<VersionRecord, VersionSummary>> ToSummary = v => new VersionSummary
    {
        Id = v.Id,
        ModelId = v.ModelId,
        Number = v.Number,
        ParentVersionId = v.ParentVersionId,
        Message = v.Message,
        CommitSha = v.CommitSha,
        CreatedBy = v.CreatedBy,
        CreatedAt = v.CreatedAt,
    };

    private static readonly Func<VersionRecord, VersionSummary> ToSummaryCompiled = ToSummary.Compile();

    private static ModelVersion Full(VersionRecord v) => new()
    {
        Id = v.Id,
        ModelId = v.ModelId,
        Number = v.Number,
        ParentVersionId = v.ParentVersionId,
        Message = v.Message,
        CommitSha = v.CommitSha,
        CreatedBy = v.CreatedBy,
        CreatedAt = v.CreatedAt,
        Snapshot = v.Snapshot,
    };
}
