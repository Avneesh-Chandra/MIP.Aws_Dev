using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Infrastructure.News.PdfEdition;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

/// <summary>
/// Latest print edition viewer page from aawsat.com/files/pdf/issue#####/.
/// </summary>
public sealed class AawsatEditionDiscovery(
    EditionDiscoveryHtmlClient html,
    ILogger<AawsatEditionDiscovery> logger) : IEditionUrlDiscovery
{
    public const string Key = "news.aawsat";

    public string ConnectorKey => Key;

    public async Task<EditionDiscoveryResult> DiscoverLatestEditionAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var pageUri = new Uri(source.BaseUrl.TrimEnd('/') + "/");
        var content = await html.FetchHtmlAsync(pageUri, source.UseHeadlessBrowser, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Could not load Asharq Al Awsat homepage: {pageUri}");
        }

        var issueId = EditionDiscoveryHtmlClient.MaxCapturedGroup(content, @"files/pdf/issue(\d+)/");
        if (issueId is null)
        {
            throw new InvalidOperationException("No Asharq Al Awsat print-edition issue link found on homepage.");
        }

        var editionUri = AawsatFullPublicationPdf.ResolveIssueViewerUri(
            new Uri($"https://aawsat.com/files/pdf/issue{issueId.Value}/"));
        logger.LogInformation("Asharq Al Awsat latest issue {IssueId} -> {Url}", issueId, editionUri);
        return new EditionDiscoveryResult(editionUri, "text/html", $"{source.Name} issue {issueId}");
    }
}
