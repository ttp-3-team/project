using ArchLens.Domain.Model;

namespace ArchLens.Domain.Compose;

/// <summary>Result of drift rule R1. Lists carry service names and container ids; messages show names only.</summary>
public sealed record DriftResult(bool Ok, IReadOnlyList<string> ServicesWithoutContainer, IReadOnlyList<string> ContainersWithoutService, IReadOnlyList<string> Messages);

/// <summary>
/// Rule R1 (port of the PoC's rules.ts): every compose service maps to a container of the target system in the
/// current model, and every container of that system tagged <c>deployed</c> has a compose service.
/// </summary>
public static class ComposeDrift
{
    public static DriftResult Check(ModelSnapshot current, ComposeFile compose, string targetSystemId)
    {
        var mapping = ServiceMapping.Map(current, targetSystemId, compose.Services.Select(s => s.Name));
        var servicesWithoutContainer = compose.Services.Select(s => s.Name).Where(s => !mapping.ContainsKey(s)).ToList();
        var mapped = mapping.Values.ToHashSet(StringComparer.Ordinal);
        var deployedWithoutService = current.Elements
            .Where(e => e.Kind == ElementKind.Container && e.ParentId == targetSystemId && e.Tags.Contains(ComposeDraft.DeployedTag) && !mapped.Contains(e.Id))
            .OrderBy(e => e.Name, StringComparer.Ordinal).ThenBy(e => e.Id, StringComparer.Ordinal)
            .ToList();

        var messages = servicesWithoutContainer.Select(s => $"service '{s}' is missing from the model")
            .Concat(deployedWithoutService.Select(c => $"container '{c.Name}' is tagged deployed, but docker-compose has no service for it"))
            .ToList();
        return new DriftResult(messages.Count == 0, servicesWithoutContainer, deployedWithoutService.Select(c => c.Id).ToList(), messages);
    }
}
