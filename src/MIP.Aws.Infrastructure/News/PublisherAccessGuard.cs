namespace MIP.Aws.Infrastructure.News;

/// <summary>Detects bot-protection / access-denied HTML that must not be treated as publisher content.</summary>
public static class PublisherAccessGuard
{
    public static bool IsAccessBlocked(string? html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return false;
        }

        var sample = html.Length > 4096 ? html[..4096] : html;
        return sample.Contains("you have been blocked", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("sorry, you have been blocked", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("access denied", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("cf-browser-verification", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("cloudflare", StringComparison.OrdinalIgnoreCase)
                   && (sample.Contains("challenge", StringComparison.OrdinalIgnoreCase)
                       || sample.Contains("ray id", StringComparison.OrdinalIgnoreCase)
                       || sample.Contains("checking your browser", StringComparison.OrdinalIgnoreCase))
               || sample.Contains("attention required", StringComparison.OrdinalIgnoreCase)
               || sample.Contains("captcha-delivery", StringComparison.OrdinalIgnoreCase);
    }

    public static bool LooksLikePublisherEpaper(string? html) =>
        !string.IsNullOrWhiteSpace(html)
        && (html.Contains("aPDFdownloadAllPages", StringComparison.OrdinalIgnoreCase)
            || html.Contains("i.alayam.com", StringComparison.OrdinalIgnoreCase)
            || html.Contains("/files/pdf/issue", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Full Publication", StringComparison.OrdinalIgnoreCase));
}
