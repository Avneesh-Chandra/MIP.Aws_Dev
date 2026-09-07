using MIP.Aws.Application.Abstractions.Reporting;

namespace MIP.Aws.Infrastructure.Operator;

internal static class PdfDownloadMailAssets
{
    public const string LogoContentId = "gfh-logo";
    public const string HeroContentId = "newspaper-hero";

    public static (string? LogoCidUrl, string? HeroCidUrl, IReadOnlyList<EmailAttachment> Attachments)
        BuildModernInlineImages(byte[]? compositeHeroJpeg = null)
    {
        var attachments = new List<EmailAttachment>(2);
        string? logoUrl = null;
        string? heroUrl = null;

        if (TryReadBytes("gfh-logo-mail.png", out var logoBytes)
            || TryReadBytes("gfh-logo.png", out logoBytes)
            || TryReadBytes("gfh-logo-dark.png", out logoBytes))
        {
            attachments.Add(new EmailAttachment(
                "gfh-logo.png",
                "image/png",
                logoBytes,
                LogoContentId,
                IsInline: true));
            logoUrl = $"cid:{LogoContentId}";
        }

        if (compositeHeroJpeg is { Length: > 0 })
        {
            attachments.Add(new EmailAttachment(
                "newspaper-hero.jpg",
                "image/jpeg",
                compositeHeroJpeg,
                HeroContentId,
                IsInline: true));
            heroUrl = $"cid:{HeroContentId}";
        }
        else if (TryReadBytes(Path.Combine("mail", "daily-newspaper-hero.jpg"), out var heroBytes))
        {
            attachments.Add(new EmailAttachment(
                "daily-newspaper-hero.jpg",
                "image/jpeg",
                heroBytes,
                HeroContentId,
                IsInline: true));
            heroUrl = $"cid:{HeroContentId}";
        }

        return (logoUrl, heroUrl, attachments);
    }

    private static bool TryReadBytes(string relativeUnderImages, out byte[] bytes)
    {
        foreach (var path in CandidatePaths(relativeUnderImages))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            bytes = File.ReadAllBytes(path);
            if (bytes.Length > 0)
            {
                return true;
            }
        }

        bytes = [];
        return false;
    }

    private static IEnumerable<string> CandidatePaths(string relativeUnderImages)
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "wwwroot", "_content", "MIP.Aws.Blazor", "images", relativeUnderImages);
        yield return Path.Combine(baseDir, "wwwroot", "images", relativeUnderImages);
        yield return Path.Combine(baseDir, "_content", "MIP.Aws.Blazor", "images", relativeUnderImages);
    }
}
