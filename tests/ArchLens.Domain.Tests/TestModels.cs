using ArchLens.Domain.Model;

namespace ArchLens.Domain.Tests;

internal static class TestModels
{
    public static Element Person(string id, string? name = null) =>
        new() { Id = id, Kind = ElementKind.Person, Name = name ?? id };

    public static Element Sys(string id, string? name = null, bool external = false) =>
        new() { Id = id, Kind = ElementKind.SoftwareSystem, Name = name ?? id, External = external };

    public static Element Container(string id, string parentId, string? name = null, string technology = "", params string[] tags) =>
        new() { Id = id, Kind = ElementKind.Container, Name = name ?? id, ParentId = parentId, Technology = technology, Tags = tags };

    public static Element Component(string id, string parentId, string? name = null) =>
        new() { Id = id, Kind = ElementKind.Component, Name = name ?? id, ParentId = parentId };

    public static Relationship Rel(string source, string target, string description = "Uses", string technology = "", string? id = null) =>
        new() { Id = id ?? $"r-{source}-{target}", SourceId = source, TargetId = target, Description = description, Technology = technology };

    public static ModelSnapshot Snapshot(IEnumerable<Element> elements, IEnumerable<Relationship>? relationships = null) =>
        new() { Elements = elements.ToList(), Relationships = (relationships ?? []).ToList() };

    public static ModelSnapshot Small() => Snapshot(
        [
            Person("user", "User"),
            Sys("sys", "Shop"),
            Sys("ext", "Payments", external: true),
            Container("web", "sys", "Web", "React"),
            Container("api", "sys", "API", "Go"),
            Container("db", "sys", "Database", "Postgres"),
            Component("ctrl", "api", "Controller"),
            Component("repo", "api", "Repository"),
        ],
        [
            Rel("user", "web", "Uses"),
            Rel("web", "api", "Calls", "HTTPS"),
            Rel("api", "db", "Reads"),
            Rel("api", "ext", "Charges"),
            Rel("ctrl", "repo", "Uses"),
            Rel("repo", "db", "Reads", "SQL"),
        ]);

    public static string ReferencePath(params string[] parts) =>
        Path.Combine([AppContext.BaseDirectory, "reference-models", .. parts]);

    public static ModelSnapshot Reference(string name) =>
        SnapshotJson.Parse(File.ReadAllText(ReferencePath("shopflow", name))).Value;

    public static ModelSnapshot With(this ModelSnapshot s, Func<Element, Element> change) =>
        s with { Elements = s.Elements.Select(change).ToList() };
}
