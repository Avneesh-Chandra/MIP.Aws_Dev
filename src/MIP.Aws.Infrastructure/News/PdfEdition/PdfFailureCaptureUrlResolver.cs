using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Chooses the page URL operators should see when diagnosing a failed public PDF download.
/// Prefers the configured discovery landing page over auto-scanned candidate links.
/// </summary>
public static class PdfFailureCaptureUrlResolver
{
    public static string? Resolve(NewsSource source, string? hintedPageUrl = null)
    {
        var discovery = FirstNonEmpty(source.PdfDiscoveryPageUrl, source.EditionUrl, source.BaseUrl);
        if (!string.IsNullOrWhiteSpace(discovery))
        {
            return discovery.Trim();
        }

        return string.IsNullOrWhiteSpace(hintedPageUrl) ? null : hintedPageUrl.Trim();
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return null;
    }
}
