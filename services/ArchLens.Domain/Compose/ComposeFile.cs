using ErrorOr;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace ArchLens.Domain.Compose;

/// <summary>A compose service: the parts ArchLens reads.</summary>
public sealed record ComposeService(string Name, string? Image, bool HasBuild, IReadOnlyList<string> DependsOn);

/// <summary>The services of a docker-compose file, sorted by name.</summary>
public sealed record ComposeFile(IReadOnlyList<ComposeService> Services)
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .WithTypeConverter(new DependsOnConverter())
        .IgnoreUnmatchedProperties()
        .Build();

    /// <summary>Parses compose YAML. Supports <c>depends_on</c> as a list or as the long (map) syntax.</summary>
    public static ErrorOr<ComposeFile> Parse(string yaml)
    {
        ComposeDocument? document;
        try
        {
            document = Yaml.Deserialize<ComposeDocument?>(yaml);
        }
        catch (YamlException e)
        {
            return Problems.Of($"invalid YAML (line {e.Start.Line}): {(e.InnerException ?? e).Message}");
        }
        if (document?.Services is not { } services) return Problems.Of("docker-compose has no services section");
        if (services.Keys.FirstOrDefault(k => k is not string { Length: > 0 }) is { } badKey)
            return Problems.Of($"a service name must be a string, not '{badKey}'");

        return new ComposeFile(services
            .Select(kv => (Name: (string)kv.Key, Doc: kv.Value ?? new ComposeServiceDocument()))
            .Select(s => new ComposeService(
                s.Name,
                s.Doc.Image,
                s.Doc.Build is not null,
                (s.Doc.DependsOn?.Services ?? []).Where(d => d.Length > 0).Distinct().Order(StringComparer.Ordinal).ToList()))
            .OrderBy(s => s.Name, StringComparer.Ordinal)
            .ToList());
    }

    private sealed class ComposeDocument
    {
        public Dictionary<object, ComposeServiceDocument?>? Services { get; set; }
    }

    private sealed class ComposeServiceDocument
    {
        public string? Image { get; set; }
        public object? Build { get; set; }
        public DependsOn? DependsOn { get; set; }
    }

    private sealed record DependsOn(IReadOnlyList<string> Services);

    private sealed class DependsOnConverter : IYamlTypeConverter
    {
        public bool Accepts(Type type) => type == typeof(DependsOn);

        public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
        {
            var names = new List<string>();
            if (parser.TryConsume<SequenceStart>(out _))
            {
                while (!parser.TryConsume<SequenceEnd>(out _))
                {
                    if (parser.TryConsume<Scalar>(out var item)) names.Add(item.Value);
                    else rootDeserializer(typeof(object));
                }
            }
            else if (parser.TryConsume<MappingStart>(out _))
            {
                while (!parser.TryConsume<MappingEnd>(out _))
                {
                    if (parser.TryConsume<Scalar>(out var key)) names.Add(key.Value);
                    else rootDeserializer(typeof(object));
                    rootDeserializer(typeof(object));
                }
            }
            else
            {
                rootDeserializer(typeof(object));
            }
            return new DependsOn(names);
        }

        public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer) =>
            throw new NotSupportedException("compose files are only read");
    }
}
