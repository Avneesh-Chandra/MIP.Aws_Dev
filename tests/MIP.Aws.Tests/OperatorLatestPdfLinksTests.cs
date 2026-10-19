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

    [Fact]
    public void BuildPublicPdfDownloadUrl_escapes_token_and_supports_inline_mode()
    {
        var url = OperatorLatestPdfLinks.BuildPublicPdfDownloadUrl(
            "https://files.example.com",
            "token+/with?characters",
            inline: true);

        Assert.Equal(
            "https://files.example.com/api/v1/operator/public-pdf-download?token=token%2B%2Fwith%3Fcharacters&inline=true",
            url);
    }

    [Theory]
    [InlineData(
        "http://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com",
        "https://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com")]
    [InlineData(
        "https://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com",
        "https://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com")]
    [InlineData("http://localhost:5196", "http://localhost:5196")]
    [InlineData(
        "https://gfnnewsintelligence.gfh.com",
        "https://gfnnewsintelligence.gfh.com")]
    [InlineData(
        "https://gfnnewsintelligence.gfh.com/",
        "https://gfnnewsintelligence.gfh.com")]
    public void NormalizePortalBaseUrl_upgrades_http_alb_to_https(string input, string expected)
    {
        Assert.Equal(expected, OperatorLatestPdfLinks.NormalizePortalBaseUrl(input));
    }

    [Fact]
    public void BuildPortalUrl_pins_source_file_and_edition()
    {
        var sourceId = Guid.Parse("eae8cd3b-3dda-4be4-969d-c1da82aaccf8");
        var fileId = Guid.Parse("94717d3e-e716-4ff4-b8cb-6cffb8b69307");
        var url = OperatorLatestPdfLinks.BuildPortalUrl(
            "http://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com",
            sourceId,
            inline: true,
            fileId,
            new DateOnly(2026, 8, 12));

        Assert.Equal(
            "https://internal-ir-gfh-dev-mip-ecs-int-lb-01-1702425070.eu-west-1.elb.amazonaws.com/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/pdf/94717d3e-e716-4ff4-b8cb-6cffb8b69307?inline=true&edition=2026-08-12",
            url);
        Assert.DoesNotContain("/latest-pdf", url, StringComparison.OrdinalIgnoreCase);
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
    [InlineData(
        "/api/v1/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/pdf/94717d3e-e716-4ff4-b8cb-6cffb8b69307",
        "?inline=true&edition=2026-08-12",
        "/operator/sources/eae8cd3b-3dda-4be4-969d-c1da82aaccf8/pdf/94717d3e-e716-4ff4-b8cb-6cffb8b69307?inline=true&edition=2026-08-12")]
    public void TryMapApiPathToPortal_maps_latest_and_file_pdf_paths(string path, string query, string expected)
    {
        Assert.True(OperatorLatestPdfLinks.TryMapApiPathToPortal(path, query, out var portalPath));
        Assert.Equal(expected, portalPath);
    }
}

public sealed class LocalReturnUrlValidatorTests
{
    [Theory]
    [InlineData("/operator/sources/00000000-0000-0000-0000-000000000001/latest-pdf?inline=true", true)]
    [InlineData("/operator/sources/00000000-0000-0000-0000-000000000001/pdf/11111111-1111-1111-1111-111111111111?inline=true", true)]
    [InlineData("https://evil.example/phish", false)]
    [InlineData("//evil.example/phish", false)]
    [InlineData("", false)]
    public void IsValid_rejects_open_redirects(string returnUrl, bool expected)
    {
        Assert.Equal(expected, LocalReturnUrlValidator.IsValid(returnUrl));
    }
}
