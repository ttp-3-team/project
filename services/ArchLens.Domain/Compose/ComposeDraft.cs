using ErrorOr;
using System.Text;
using ArchLens.Domain.Model;

namespace ArchLens.Domain.Compose;

/// <summary>
/// Builds a model draft from docker-compose: the current model plus a container for every unmapped service
/// and a "Uses" relationship for every <c>depends_on</c> pair with no relationship between the two yet.
/// The draft never deletes or changes existing elements. New ids come from <c>newId</c> (opaque, e.g. UUIDs).
/// New containers are tagged <c>deployed</c>: they come from a deployment manifest.
/// </summary>
public static class ComposeDraft
{
    public const string DeployedTag = "deployed";
    public const string UsesDescription = "Uses";

    public static ErrorOr<ModelSnapshot> Build(ModelSnapshot current, ComposeFile compose, string targetSystemId, Func<string> newId)
    {
        var targetCheck = CheckTarget(current, targetSystemId);
        if (targetCheck is not null) return Problems.Of(targetCheck);

        var mapping = new Dictionary<string, string>(ServiceMapping.Map(current, targetSystemId, compose.Services.Select(s => s.Name)), StringComparer.Ordinal);
        var elements = current.Elements.ToList();
        foreach (var service in compose.Services.Where(s => !mapping.ContainsKey(s.Name)))
        {
            var id = newId();
            mapping[service.Name] = id;
            elements.Add(new Element
            {
                Id = id,
                Kind = ElementKind.Container,
                Name = TitleCase(service.Name),
                Technology = Technology(service),
                Tags = [DeployedTag],
                ParentId = targetSystemId,
                Properties = new Dictionary<string, string> { [ServiceMapping.ComposeServiceProperty] = service.Name },
            });
        }

        var relationships = current.Relationships.ToList();
        bool Connected(string a, string b) => relationships.Any(r => (r.SourceId == a && r.TargetId == b) || (r.SourceId == b && r.TargetId == a));
        foreach (var service in compose.Services)
        {
            foreach (var dependency in service.DependsOn)
            {
                if (!mapping.TryGetValue(dependency, out var targetId)) continue;
                var sourceId = mapping[service.Name];
                if (sourceId == targetId || Connected(sourceId, targetId)) continue;
                relationships.Add(new Relationship { Id = newId(), SourceId = sourceId, TargetId = targetId, Description = UsesDescription });
            }
        }

        return current with { Elements = elements, Relationships = relationships };
    }

    /// <summary>A problem with the target system id, or null when it names a software system of the model.</summary>
    public static string? CheckTarget(ModelSnapshot current, string targetSystemId)
    {
        var target = current.Elements.FirstOrDefault(e => e.Id == targetSystemId);
        if (target is null) return $"the model has no element '{targetSystemId}'";
        return target.Kind == ElementKind.SoftwareSystem
            ? null
            : $"'{target.Name}' — {ElementKinds.Label(target.Kind)}, and only a software system can be the target";
    }

    internal static string Technology(ComposeService service)
    {
        if (string.IsNullOrWhiteSpace(service.Image)) return service.HasBuild ? "Docker" : "";
        var image = service.Image.Split('@')[0];
        var lastSlash = image.LastIndexOf('/');
        var colon = image.LastIndexOf(':');
        return colon > lastSlash ? image[..colon] : image;
    }

    /// <summary>Display name of a service: "api-gateway" → "Api Gateway", "messageBus" → "Message Bus".</summary>
    public static string TitleCase(string name) =>
        string.Join(' ', Words(name).Select(w => char.ToUpperInvariant(w[0]) + w[1..]));

    private static IEnumerable<string> Words(string s)
    {
        var current = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var ch = s[i];
            if (!char.IsLetterOrDigit(ch))
            {
                if (current.Length > 0) { yield return current.ToString(); current.Clear(); }
                continue;
            }
            if (char.IsUpper(ch) && current.Length > 0 && char.IsLower(s[i - 1]))
            {
                yield return current.ToString();
                current.Clear();
            }
            current.Append(ch);
        }
        if (current.Length > 0) yield return current.ToString();
    }
}
