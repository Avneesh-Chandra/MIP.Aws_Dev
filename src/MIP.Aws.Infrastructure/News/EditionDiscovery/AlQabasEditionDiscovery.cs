using System.Text.RegularExpressions;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.News.EditionDiscovery;

/// <summary>
/// Latest archive PDF from d.alqabas.com (linked on alqabas.com homepage JSON/HTML).
/// </summary>
public sealed class AlQabasEditionDiscovery(
    EditionDiscoveryHtmlClient html,
    ILogger<AlQabasEditionDiscovery> logger) : IEditionUrlDiscovery
{
    public const string Key = "news.alqabas";

    private static readonly Regex ArchivePdfRegex = new(
        @"https://d\.alqabas\.com/archive/(\d+)_[^""']+\.pdf",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public string ConnectorKey => Key;

    public async Task<EditionDiscoveryResult> DiscoverLatestEditionAsync(NewsSource source, CancellationToken cancellationToken)
    {
        var pageUris = new[]
        {
            new Uri(source.BaseUrl.TrimEnd('/') + "/"),
            new Uri("https://d.alqabas.com/archive/")
        }.Distinct().ToList();

        string? bestUrl = null;
        long bestTs = 0;
        foreach (var pageUri in pageUris)
        {
            var content = await html.FetchHtmlAsync(pageUri, source.UseHeadlessBrowser, cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(content))
            {
                logger.LogDebug("Al Qabas discovery: no HTML from {Url}", pageUri);
                continue;
            }

            foreach (Match match in ArchivePdfRegex.Matches(content))
            {
                if (!match.Success || !long.TryParse(match.Groups[1].Value, out var ts))
                {
                    continue;
                }

                if (ts >= bestTs)
                {
                    bestTs = ts;
                    bestUrl = match.Value;
                }
            }

            if (bestUrl is not null)
            {
                break;
            }
        }

        if (string.IsNullOrWhiteSpace(bestUrl) || !Uri.TryCreate(bestUrl, UriKind.Absolute, out var pdfUri))
        {
            throw new InvalidOperationException("No Al Qabas archive PDF link found on homepage or archive index.");
        }

        logger.LogInformation("Al Qabas latest archive PDF -> {Url}", pdfUri);
        return new EditionDiscoveryResult(pdfUri, "application/pdf", source.Name);
    }
}
