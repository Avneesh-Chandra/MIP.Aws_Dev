using MIP.Aws.Application.Abstractions.Operator;
using MIP.Aws.Infrastructure.Operator;
using Microsoft.AspNetCore.DataProtection;

namespace MIP.Aws.Tests;

public sealed class PdfDownloadMailLinkServiceTests
{
    [Fact]
    public void CreateToken_round_trips_file_payload()
    {
        var service = CreateService();
        var file = new PdfDownloadMailLinkFile(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Bahrain - Alayam.pdf");

        var token = service.CreateToken([file], DateTimeOffset.UtcNow.AddHours(24));

        Assert.True(service.TryReadToken(token, out var payload));
        Assert.NotNull(payload);
        Assert.Equal([file], payload!.Files);
    }

    [Fact]
    public void TryReadToken_rejects_tampered_token()
    {
        var service = CreateService();
        var file = new PdfDownloadMailLinkFile(Guid.NewGuid(), Guid.NewGuid(), "edition.pdf");
        var token = service.CreateToken([file], DateTimeOffset.UtcNow.AddHours(24));
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');

        Assert.False(service.TryReadToken(tampered, out _));
    }

    [Fact]
    public void CreateToken_rejects_expired_links()
    {
        var service = CreateService();
        var file = new PdfDownloadMailLinkFile(Guid.NewGuid(), Guid.NewGuid(), "edition.pdf");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.CreateToken([file], DateTimeOffset.UtcNow.AddHours(-1)));
    }

    private static PdfDownloadMailLinkService CreateService() =>
        new(DataProtectionProvider.Create("MIP.Aws.Tests.PdfDownloadMail"));
}
