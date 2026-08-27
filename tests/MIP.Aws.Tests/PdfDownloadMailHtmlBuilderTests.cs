using MIP.Aws.Infrastructure.Operator;

namespace MIP.Aws.Tests;

public sealed class PdfDownloadMailHtmlBuilderTests
{
    [Fact]
    public void Build_renders_browser_and_download_links_with_escaped_values()
    {
        var html = PdfDownloadMailHtmlBuilder.Build(
            [
                new PdfDownloadMailItem(
                    "Alayam <source>",
                    "edition & today.pdf",
                    new DateOnly(2026, 8, 27),
                    5_471_234,
                    "https://files.example/view",
                    "https://files.example/download")
            ],
            "https://files.example/all",
            "https://files.example/logo.svg");

        Assert.Contains("href=\"https://files.example/view\"", html);
        Assert.Contains("href=\"https://files.example/download\"", html);
        Assert.Contains("href=\"https://files.example/all\"", html);
        Assert.Contains("edition &amp; today.pdf", html);
        Assert.DoesNotContain("Alayam <source>", html);
        Assert.Contains("5.22 MB", html);
    }
}
