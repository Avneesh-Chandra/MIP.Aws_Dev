using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

/// <summary>
/// Latest issue PDF from media.akhbar-alkhaleej.com (issue id embedded in homepage HTML).
/// </summary>
public sealed class AkhbarAlKhaleejEditionDiscovery(
    EditionDiscoveryHtmlClient html,
    ILogger<AkhbarAlKhaleejEditionDiscovery> logger) : IEditionUrlDiscovery
{
    public const string Key = "news.akhbar-alkhaleej";

    public string ConnectorKey => Key;

    public async Task<EditionDiscoveryResult> DiscoverLatestEditionAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var pageUri = new Uri(source.BaseUrl.TrimEnd('/') + "/");
        var content = await html.FetchHtmlAsync(pageUri, source.UseHeadlessBrowser, cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException($"Could not load Akhbar Al Khaleej homepage: {pageUri}");
        }

        var issueCandidates = new[]
        {
            EditionDiscoveryHtmlClient.MaxCapturedGroup(content, @"source/(\d+)/"),
            EditionDiscoveryHtmlClient.MaxCapturedGroup(content, @"pdf\.php\?i=(\d+)")
        }.Where(x => x.HasValue).Select(x => x!.Value).ToArray();

        var issueId = issueCandidates.Length == 0 ? (int?)null : issueCandidates.Max();
        if (issueId is null)
        {
            throw new InvalidOperationException("No Akhbar Al Khaleej issue id found on homepage.");
        }

        var pdfUri = new Uri($"https://media.akhbar-alkhaleej.com/pdf.php?i={issueId.Value}");
        logger.LogInformation("Akhbar Al Khaleej latest issue {IssueId} -> {Pdf}", issueId, pdfUri);
        return new EditionDiscoveryResult(pdfUri, "application/pdf", $"{source.Name} #{issueId}");
    }
}
