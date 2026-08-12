using MIP.Aws.Application.Features.Operator;
using MIP.Aws.Application.Security;

namespace MIP.Aws.Tests;

public sealed class OperatorLatestPdfLinksTests
{
    [Fact]
    public void BuildPortalUrl_uses_portal_route_not_api()
    {
        var sourceId = Guid.Parse("eae8cd3b-3dda-4be4-969d-c1da82aaccf8");
        var url = OperatorLatestPdfLinks.BuildPortalUrl(
            "https://internal.example.com",
            sourceId,
            inline: true);

        Assert.Contains("/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/latest-pdf?inline=true", url);
        Assert.DoesNotContain("/api/", url);
    }

    [Theory]
    [InlineData(
        "/api/v1/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/latest-pdf",
        "?inline=true",
        "/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/latest-pdf?inline=true")]
    [InlineData(
        "/api/v1/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/latest-pdf/",
        "",
        "/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/latest-pdf?inline=true")]
    public void TryMapApiPathToPortal_maps_latest_pdf_api_paths(string path, string query, string expected)
    {
        Assert.True(OperatorLatestPdfLinks.TryMapApiPathToPortal(path, query, out var portalPath));
        Assert.Equal(expected, portalPath);
    }
}

public sealed class LocalReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/operator/sources/00000000-0000-0000-0000-000000000001/latest-pdf?inline=true", true)]
    [InlineData("https://evil.example/phish", false)]
    [InlineData("//evil.example/phish", false)]
    [InlineData("", false)]
    public void IsValid_rejects_open_redirects(string returnUrl, bool expected)
    {
        Assert.Equal(expected, LocalReturnUrlValidator.IsValid(returnUrl));
    }
}
