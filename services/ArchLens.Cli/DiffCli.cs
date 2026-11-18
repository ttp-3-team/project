using System.CommandLine;
using System.CommandLine.Help;

namespace ArchLens.Cli;

/// <summary>Parsed options of <c>archlens diff</c>.</summary>
public sealed record DiffOptions(FileInfo Base, FileInfo Head, DirectoryInfo Out, FileInfo? Compose, string? System, string? ImageBaseUrl);

/// <summary>The command line: System.CommandLine parses and validates; <see cref="DiffCommand"/> does the work.</summary>
public static class DiffCli
{
    /// <summary>Parses <paramref name="args"/> and runs the command. Every parse error exits with 2 (bad input), never 0.</summary>
    public static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        var root = Build(stdout, stderr);
        var parsed = root.Parse(args);
        if (parsed.Errors.Count > 0 && parsed.Action is not HelpAction)
        {
            foreach (var error in parsed.Errors) stderr.WriteLine($"archlens: {error.Message}");
            stderr.WriteLine("Run 'archlens diff --help' for usage.");
            return DiffCommand.BadInput;
        }
        return parsed.Invoke(new InvocationConfiguration { Output = stdout, Error = stderr });
    }

    private static RootCommand Build(TextWriter stdout, TextWriter stderr)
    {
        var baseOption = new Option<FileInfo>("--base") { Description = "Base model (snapshot or exported version JSON)", Required = true }.AcceptExistingOnly();
        var headOption = new Option<FileInfo>("--head") { Description = "Head model (snapshot or exported version JSON)", Required = true }.AcceptExistingOnly();
        var outOption = new Option<DirectoryInfo>("--out") { Description = "Directory for comment.md, SVGs and delta.json", Required = true };
        var composeOption = new Option<FileInfo>("--compose") { Description = "docker-compose file to check against the model (rule R1)" }.AcceptExistingOnly();
        var systemOption = new Option<string>("--system") { Description = "Id of the software system the compose file deploys" };
        var imageBaseOption = new Option<string>("--image-base-url") { Description = "URL prefix for the SVGs in comment.md" };

        var diff = new Command("diff", "Semantic diff of two ArchLens models, with SVGs of the affected views and drift rule R1")
        {
            baseOption, headOption, outOption, composeOption, systemOption, imageBaseOption,
        };
        diff.Validators.Add(result =>
        {
            if ((result.GetResult(composeOption) is null) != (result.GetResult(systemOption) is null))
                result.AddError("--compose and --system go together");
        });
        diff.SetAction(parsed => DiffCommand.Run(
            new DiffOptions(
                parsed.GetRequiredValue(baseOption),
                parsed.GetRequiredValue(headOption),
                parsed.GetRequiredValue(outOption),
                parsed.GetValue(composeOption),
                parsed.GetValue(systemOption),
                parsed.GetValue(imageBaseOption)),
            stdout,
            stderr));

        return new RootCommand("ArchLens command line") { diff };
    }
}
