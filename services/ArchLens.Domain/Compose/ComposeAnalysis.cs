using ArchLens.Domain.Diff;
using ArchLens.Domain.Model;
using ErrorOr;

namespace ArchLens.Domain.Compose;

/// <summary>Draft, its diff against the current model, and the drift of the current model.</summary>
public sealed record ComposeDraftResult(ModelSnapshot Draft, DiffResult Diff, DriftResult Drift)
{
    /// <summary>Builds the draft from a parsed compose file and checks drift in one call.</summary>
    public static ErrorOr<ComposeDraftResult> Run(ModelSnapshot current, ComposeFile compose, string targetSystemId, Func<string> newId) =>
        ComposeDraft.Build(current, compose, targetSystemId, newId)
            .Then(draft => new ComposeDraftResult(draft, SemanticDiff.Compute(current, draft), ComposeDrift.Check(current, compose, targetSystemId)));
}
