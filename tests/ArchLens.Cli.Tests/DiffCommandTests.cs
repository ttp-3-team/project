
namespace ArchLens.Cli.Tests;

public sealed class DiffCommandTests : IDisposable
{
    private readonly string _out = Directory.CreateTempSubdirectory("archlens-cli-").FullName;
    private static string Ref(string name) => Path.Combine(AppContext.BaseDirectory, "reference-models", "shopflow", name);

    public void Dispose() => Directory.Delete(_out, recursive: true);

    private (int Code, string Out, string Err) Run(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var code = DiffCli.Run(args, stdout, stderr);
        return (code, stdout.ToString(), stderr.ToString());
    }

    private string[] Args(params string[] extra) =>
        ["diff", "--base", Ref("base.json"), "--head", Ref("head.json"), "--out", _out, .. extra];

    [Fact]
    public void Drift_exits_1_and_writes_comment_svg_and_delta()
    {
        var (code, _, _) = Run(Args("--compose", Ref("compose-drift.yml"), "--system", "shop"));
        Assert.Equal(DiffCommand.Drift, code);
        Assert.True(File.Exists(Path.Combine(_out, "comment.md")));
        Assert.True(File.Exists(Path.Combine(_out, "container-shop.svg")));
        Assert.True(File.Exists(Path.Combine(_out, "delta.json")));
    }

    [Fact]
    public void Aligned_compose_exits_0()
    {
        Assert.Equal(DiffCommand.Ok, Run(Args("--compose", Ref("compose-fixed.yml"), "--system", "shop")).Code);
    }

    [Theory]
    [InlineData("--compse", "x")]
    [InlineData("--compose")]
    [InlineData("--system", "shop")]
    public void Unknown_dangling_or_unpaired_options_exit_2(params string[] extra)
    {
        var (code, _, err) = Run(Args(extra));
        Assert.Equal(DiffCommand.BadInput, code);
        Assert.Contains("--help", err);
    }

    [Fact]
    public void Missing_compose_file_exits_2()
    {
        var (code, _, err) = Run(Args("--compose", "/nope/compose.yml", "--system", "shop"));
        Assert.Equal(DiffCommand.BadInput, code);
        Assert.Contains("compose.yml", err);
    }

    [Theory]
    [InlineData("nope")]
    [InlineData("apiGateway")]
    public void System_that_is_not_a_software_system_exits_2(string system)
    {
        Assert.Equal(DiffCommand.BadInput, Run(Args("--compose", Ref("compose-drift.yml"), "--system", system)).Code);
    }

    [Fact]
    public void Help_exits_0()
    {
        var (code, output, _) = Run("diff", "--help");
        Assert.Equal(DiffCommand.Ok, code);
        Assert.Contains("--compose", output);
    }

    [Fact]
    public void Missing_model_file_exits_2()
    {
        Assert.Equal(DiffCommand.BadInput, Run("diff", "--base", "/nope.json", "--head", Ref("head.json"), "--out", _out).Code);
    }
}
