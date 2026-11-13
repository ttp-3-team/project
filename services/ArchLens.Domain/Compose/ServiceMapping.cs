using ArchLens.Domain.Model;

namespace ArchLens.Domain.Compose;

/// <summary>
/// Maps compose services to containers of one software system. First match wins, in this order:
/// <c>properties.composeService == service</c>; container id equals the service name;
/// normalised match where id or name, lower-cased without '-', '_' and spaces, equals the normalised service name.
/// </summary>
public static class ServiceMapping
{
    public const string ComposeServiceProperty = "composeService";

    /// <summary>Service name → container id, for the services that map.</summary>
    public static IReadOnlyDictionary<string, string> Map(ModelSnapshot model, string systemId, IEnumerable<string> services)
    {
        var containers = model.Elements
            .Where(e => e.Kind == ElementKind.Container && e.ParentId == systemId)
            .OrderBy(e => e.Id, StringComparer.Ordinal)
            .ToList();
        var result = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var service in services)
        {
            var match =
                containers.FirstOrDefault(c => c.Property(ComposeServiceProperty) == service)
                ?? containers.FirstOrDefault(c => c.Id == service)
                ?? containers.FirstOrDefault(c => Normalize(c.Id) == Normalize(service) || Normalize(c.Name) == Normalize(service));
            if (match is not null) result[service] = match.Id;
        }
        return result;
    }

    /// <summary>Lower-cases and removes '-', '_' and whitespace: <c>api-gateway</c> ≡ <c>apiGateway</c> ≡ "API Gateway".</summary>
    public static string Normalize(string s) =>
        new(s.Where(ch => ch is not ('-' or '_') && !char.IsWhiteSpace(ch)).Select(char.ToLowerInvariant).ToArray());
}
