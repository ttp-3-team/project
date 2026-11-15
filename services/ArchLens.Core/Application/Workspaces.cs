using ErrorOr;
using ArchLens.Domain;
using ArchLens.Core.Auth;
using ArchLens.Core.Contracts;
using ArchLens.Core.Data;
using ArchLens.Domain.Model;
using Microsoft.EntityFrameworkCore;

namespace ArchLens.Core.Application;

/// <summary>Workspaces and models owned by the caller. Every query is filtered by owner; foreign resources are "not found".</summary>
public sealed class Workspaces(ArchLensDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<Workspace>> List(CurrentUser user, CancellationToken ct) =>
        (await db.Workspaces.Where(w => w.OwnerId == user.Id).OrderBy(w => w.CreatedAt).ThenBy(w => w.Id).ToListAsync(ct))
            .Select(ToContract).ToList();

    public async Task<ErrorOr<Workspace>> Create(CurrentUser user, CreateWorkspaceRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return AppErrors.BadRequest("Invalid name", "the name cannot be empty");
        var record = new WorkspaceRecord { Id = Guid.CreateVersion7(), Name = name, OwnerId = user.Id, CreatedAt = clock.GetUtcNow() };
        db.Workspaces.Add(record);
        await db.SaveChangesAsync(ct);
        return ToContract(record);
    }

    public async Task<ErrorOr<Workspace>> Get(CurrentUser user, Guid id, CancellationToken ct)
    {
        var record = await Owned(user).FirstOrDefaultAsync(w => w.Id == id, ct);
        return record is null ? AppErrors.NotFound("workspace") : ToContract(record);
    }

    public async Task<ErrorOr<IReadOnlyList<Model>>> ListModels(CurrentUser user, Guid workspaceId, CancellationToken ct)
    {
        if (!await Owned(user).AnyAsync(w => w.Id == workspaceId, ct)) return AppErrors.NotFound("workspace");
        var models = await ModelsWithLatest(db.Models.Where(m => m.WorkspaceId == workspaceId)).ToListAsync(ct);
        return models.OrderBy(m => m.CreatedAt).ThenBy(m => m.Id).ToList();
    }

    /// <summary>Creates a model; with a snapshot (import or template) version 1 is created in the same transaction.</summary>
    public async Task<ErrorOr<Model>> CreateModel(CurrentUser user, Guid workspaceId, CreateModelRequest request, CancellationToken ct)
    {
        var name = request.Name.Trim();
        if (name.Length == 0) return AppErrors.BadRequest("Invalid name", "the name cannot be empty");
        if (!await Owned(user).AnyAsync(w => w.Id == workspaceId, ct)) return AppErrors.NotFound("workspace");

        ModelSnapshot? snapshot = null;
        if (request.Snapshot is not null)
        {
            var checkedSnapshot = SnapshotValidator.Check(request.Snapshot);
            if (checkedSnapshot.IsError) return Problems.Of(checkedSnapshot.Descriptions());
            snapshot = checkedSnapshot.Value;
        }

        var now = clock.GetUtcNow();
        var model = new ModelRecord { Id = Guid.CreateVersion7(), WorkspaceId = workspaceId, Name = name, CreatedAt = now };
        db.Models.Add(model);
        VersionRecord? first = null;
        if (snapshot is not null)
        {
            first = new VersionRecord
            {
                Id = Guid.CreateVersion7(),
                ModelId = model.Id,
                Number = 1,
                Snapshot = snapshot,
                Message = string.IsNullOrWhiteSpace(request.Message) ? "Initial version" : request.Message.Trim(),
                CreatedBy = user.Id,
                CreatedAt = now,
            };
            db.Versions.Add(first);
        }
        await db.SaveChangesAsync(ct);
        return ToContract(model, first?.Id, first?.Number);
    }

    public async Task<ErrorOr<Model>> GetModel(CurrentUser user, Guid modelId, CancellationToken ct)
    {
        var model = await ModelsWithLatest(OwnedModels(user).Where(m => m.Id == modelId)).FirstOrDefaultAsync(ct);
        return model is null ? AppErrors.NotFound("model") : model;
    }

    internal IQueryable<WorkspaceRecord> Owned(CurrentUser user) => db.Workspaces.Where(w => w.OwnerId == user.Id);

    internal IQueryable<ModelRecord> OwnedModels(CurrentUser user) => db.Models.Where(m => m.Workspace!.OwnerId == user.Id);

    private IQueryable<Model> ModelsWithLatest(IQueryable<ModelRecord> models) =>
        from m in models
        let latest = db.Versions.Where(v => v.ModelId == m.Id).OrderByDescending(v => v.Number).FirstOrDefault()
        select new Model
        {
            Id = m.Id,
            WorkspaceId = m.WorkspaceId,
            Name = m.Name,
            CreatedAt = m.CreatedAt,
            LatestVersionId = latest == null ? null : latest.Id,
            LatestVersionNumber = latest == null ? null : latest.Number,
        };

    private static Workspace ToContract(WorkspaceRecord w) => new() { Id = w.Id, Name = w.Name, OwnerId = w.OwnerId, CreatedAt = w.CreatedAt };

    private static Model ToContract(ModelRecord m, Guid? latestId, int? latestNumber) => new()
    {
        Id = m.Id,
        WorkspaceId = m.WorkspaceId,
        Name = m.Name,
        CreatedAt = m.CreatedAt,
        LatestVersionId = latestId,
        LatestVersionNumber = latestNumber,
    };
}
