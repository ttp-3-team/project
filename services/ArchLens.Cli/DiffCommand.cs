using ErrorOr;
using System.Text;
using System.Text.Json;
using ArchLens.Domain;
using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Graph;
using ArchLens.Domain.Model;
using ArchLens.Domain.Render;

namespace ArchLens.Cli;

/// <summary>
/// <c>archlens diff</c>: writes comment.md, one SVG per affected view and delta.json.
/// Exit codes: 0 no drift, 1 drift found (rule R1), 2 unusable input (bad options, missing or invalid files, unknown system).
/// </summary>
public static class DiffCommand
{
    public const int Ok = 0, Drift = 1, BadInput = 2;

    public static int Run(DiffOptions options, TextWriter stdout, TextWriter stderr)
    {
        var b = LoadSnapshot(options.Base.FullName);
        var h = LoadSnapshot(options.Head.FullName);
        if (b.IsError || h.IsError)
        {
            foreach (var p in b.Descriptions().Concat(h.Descriptions())) stderr.WriteLine($"archlens: {p}");
            return BadInput;
        }
        var baseModel = b.Value;
        var headModel = h.Value;

        DriftResult? drift = null;
        if (options is { Compose: { } composeFile, System: { } systemId })
        {
            var target = ComposeDraft.CheckTarget(headModel, systemId);
            if (target is not null) { stderr.WriteLine($"archlens: --system {systemId}: {target}"); return BadInput; }
            var compose = ComposeFile.Parse(File.ReadAllText(composeFile.FullName));
            if (compose.IsError) { foreach (var p in compose.Descriptions()) stderr.WriteLine($"archlens: {composeFile.Name}: {p}"); return BadInput; }
            drift = ComposeDrift.Check(headModel, compose.Value, systemId);
        }

        var diff = SemanticDiff.Compute(baseModel, headModel);
        var outDir = options.Out.FullName;
        Directory.CreateDirectory(outDir);
        var imageBase = options.ImageBaseUrl?.TrimEnd('/');
        var images = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var view in diff.Views.Where(v => v.Affected))
        {
            var graph = CompareGraph.Build(baseModel, headModel, diff, view.Key);
            if (graph is null) continue;
            var file = view.Key.Replace(':', '-') + ".svg";
            File.WriteAllText(Path.Combine(outDir, file), SvgDiffRenderer.Render(graph), Utf8);
            images[view.Key] = imageBase is null ? file : $"{imageBase}/{file}";
        }
        File.WriteAllText(Path.Combine(outDir, "delta.json"), SnapshotJson.Serialize(new { diff, drift }, indented: true) + "\n", Utf8);
        File.WriteAllText(Path.Combine(outDir, "comment.md"), PrComment.Render(baseModel, headModel, diff, drift, images), Utf8);

        stdout.WriteLine($"archlens: {images.Count} affected view(s), drift {(drift is null ? "not checked" : drift.Ok ? "ok" : "FOUND")} → {outDir}");
        return drift is { Ok: false } ? Drift : Ok;
    }

    private static readonly UTF8Encoding Utf8 = new(false);

    internal static ErrorOr<ModelSnapshot> LoadSnapshot(string path)
    {
        var text = File.ReadAllText(path);
        try
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("snapshot", out var inner))
                text = inner.GetRawText();
        }
        catch (JsonException e)
        {
            return Problems.Of($"{path}: invalid JSON: {e.Message}");
        }
        var parsed = SnapshotJson.Parse(text);
        if (parsed.IsError) return Problems.Of(parsed.Descriptions().Select(p => $"{path}: {p}"));
        var problems = SnapshotValidator.Validate(parsed.Value);
        return problems.Count == 0 ? parsed : Problems.Of(problems.Select(p => $"{path}: {p}"));
    }
}
