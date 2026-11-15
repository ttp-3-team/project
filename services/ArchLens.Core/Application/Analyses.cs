using ArchLens.Core.Auth;
using ArchLens.Core.Contracts;
using ArchLens.Domain;
using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using ErrorOr;

namespace ArchLens.Core.Application;

/// <summary>Analyses of stored versions and of snapshots in the request: diffs and compose drafts. Nothing is stored.</summary>
public sealed class Analyses(Versions versions)
{
    public async Task<ErrorOr<DiffResult>> Diff(CurrentUser user, Guid modelId, Guid from, Guid to, CancellationToken ct) =>
        (await versions.Pair(user, modelId, from, to, ct)).Then(p => SemanticDiff.Compute(p.Base, p.Head));

    public static ErrorOr<DiffResult> Diff(DiffRequest request) =>
        Checked(request.Base, request.Head, (a, b) => (ErrorOr<DiffResult>)SemanticDiff.Compute(a, b));

    public async Task<ErrorOr<ComposeDraftResult>> FromCompose(CurrentUser user, Guid modelId, ComposeDraftRequest request, CancellationToken ct)
    {
        var current = await versions.Snapshot(user, modelId, request.BaseVersionId, ct);
        if (current is null) return AppErrors.NotFound("version");
        var compose = ComposeFile.Parse(request.ComposeYaml);
        if (compose.IsError) return AppErrors.BadRequest("Invalid docker-compose", string.Join("; ", compose.Descriptions()));
        var target = ComposeDraft.CheckTarget(current, request.TargetSystemId);
        if (target is not null) return AppErrors.BadRequest("Invalid target system", target);
        return ComposeDraftResult.Run(current, compose.Value, request.TargetSystemId, () => Guid.NewGuid().ToString());
    }

    private static ErrorOr<T> Checked<T>(ModelSnapshot baseModel, ModelSnapshot headModel, Func<ModelSnapshot, ModelSnapshot, ErrorOr<T>> run)
    {
        var a = SnapshotValidator.Check(baseModel);
        var b = SnapshotValidator.Check(headModel);
        if (a.IsError || b.IsError)
            return Problems.Of([.. a.Descriptions().Select(p => $"base: {p}"), .. b.Descriptions().Select(p => $"head: {p}")]);
        return run(a.Value, b.Value);
    }
}
