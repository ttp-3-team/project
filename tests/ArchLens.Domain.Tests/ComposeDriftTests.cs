using ArchLens.Domain.Compose;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class ComposeDriftTests
{
    private static DriftResult Check(string model, string compose) =>
        ComposeDrift.Check(Reference(model), ComposeFile.Parse(File.ReadAllText(ReferencePath("shopflow", compose))).Value, "shop");

    [Fact]
    public void Drift_lists_the_service_missing_from_the_model()
    {
        var drift = Check("head.json", "compose-drift.yml");
        Assert.False(drift.Ok);
        Assert.Equal(["worker"], drift.ServicesWithoutContainer);
        Assert.Empty(drift.ContainersWithoutService);
        Assert.Equal(["service 'worker' is missing from the model"], drift.Messages);
    }

    [Fact]
    public void Drift_lists_a_deployed_container_without_a_service()
    {
        var drift = Check("head.json", "compose-base.yml");
        Assert.False(drift.Ok);
        Assert.Equal(["cache"], drift.ContainersWithoutService);
        Assert.Empty(drift.ServicesWithoutContainer);
        Assert.Equal(["container 'Session Cache' is tagged deployed, but docker-compose has no service for it"], drift.Messages);
    }

    [Fact]
    public void No_drift_when_compose_and_model_are_aligned()
    {
        Assert.True(Check("head.json", "compose-fixed.yml").Ok);
        Assert.True(Check("base.json", "compose-base.yml").Ok);
    }

    [Fact]
    public void Analysis_returns_draft_diff_and_drift_together()
    {
        var analysis = ComposeDraftResult.Run(Reference("base.json"), ComposeFile.Parse(File.ReadAllText(ReferencePath("shopflow", "compose-drift.yml"))).Value, "shop", () => Guid.NewGuid().ToString()).Value;
        Assert.Equal(["Cache", "Worker"], analysis.Diff.Elements.Added.Select(e => e.Name).Order());
        Assert.All(analysis.Diff.Elements.Added, e => Assert.True(Guid.TryParse(e.Id, out _)));
        Assert.Equal(["cache", "worker"], analysis.Drift.ServicesWithoutContainer);
        Assert.True(analysis.Diff.Views.Single(v => v.Key == "container:shop").Affected);
    }
}
