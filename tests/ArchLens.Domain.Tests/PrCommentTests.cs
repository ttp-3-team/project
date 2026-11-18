using ArchLens.Domain.Compose;
using ArchLens.Domain.Diff;
using ArchLens.Domain.Render;
using static ArchLens.Domain.Tests.TestModels;

namespace ArchLens.Domain.Tests;

public class PrCommentTests
{
    private static string Comment(DriftResult? drift = null, IReadOnlyDictionary<string, string>? images = null)
    {
        var b = Reference("base.json");
        var h = Reference("head.json");
        return PrComment.Render(b, h, SemanticDiff.Compute(b, h), drift, images ?? new Dictionary<string, string>());
    }

    [Fact]
    public Task Comment_uses_names()
    {
        var md = Comment(new DriftResult(false, ["worker"], [], ["service 'worker' is missing from the model"]),
            new Dictionary<string, string> { ["container:shop"] = "https://img/container-shop.svg" });
        Assert.DoesNotContain("softwaresystem", md, StringComparison.OrdinalIgnoreCase);
        return Verify(md, "md");
    }

    [Fact]
    public void Images_are_matched_by_view_key()
    {
        var md = Comment(images: new Dictionary<string, string> { ["container:shop"] = "https://img/container-shop.svg" });
        Assert.Contains("![Containers: ShopFlow](https://img/container-shop.svg)", md);
    }

    [Fact]
    public void Names_cannot_mention_people_or_break_markdown()
    {
        Assert.Equal("&#64;team \\*bold\\* &lt;b&gt;", PrComment.Escape("@team *bold* <b>"));
    }

    [Fact]
    public void Drift_section_lists_findings()
    {
        var md = Comment(new DriftResult(false, ["worker"], [], ["service 'worker' is missing from the model"]));
        Assert.Contains("### ❌ Deployment check (R1): differences: 1", md);
        Assert.Contains("- service 'worker' is missing from the model", md);
    }
}
