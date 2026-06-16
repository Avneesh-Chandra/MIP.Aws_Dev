using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News.EditionDiscovery;

namespace MIP.Aws.Infrastructure.News.PdfEdition;

/// <summary>
/// Chooses the page URL operators should see when diagnosing a failed public PDF download.
/// For Asharq, prefers the FlipBuilder issue viewer over the homepage so AI recovery sees Download controls.
/// </summary>
public static class PdfFailureCaptureUrlResolver
{
    public static string? Resolve(NewsSource source, string? hintedPageUrl = null)
    {
        if (AawsatFullPublicationPdf.UsesClickPath(source)
            && !string.IsNullOrWhiteSpace(hintedPageUrl)
            && Uri.TryCreate(hintedPageUrl.Trim(), UriKind.Absolute, out var hinted)
            && AawsatFullPublicationPdf.IsIssueViewerUrl(hinted))
        {
            return AawsatFullPublicationPdf.ResolveIssueViewerUri(hinted).ToString();
        }

        if (AawsatFullPublicationPdf.UsesClickPath(source)
            && !string.IsNullOrWhiteSpace(source.EditionUrl)
            && Uri.TryCreate(source.EditionUrl.Trim(), UriKind.Absolute, out var editionUri)
            && AawsatFullPublicationPdf.IsIssueViewerUrl(editionUri))
        {
            return AawsatFullPublicationPdf.ResolveIssueViewerUri(editionUri).ToString();
        }

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
