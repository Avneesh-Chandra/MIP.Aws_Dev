using System.Xml.Linq;
using MIP.Aws.Application.Connectors;

namespace MIP.Aws.Infrastructure.Download;

public sealed class RssDownloader(HttpClient httpClient) : IRssDownloader
{
    public async Task<RssFeedDocument> DownloadAsync(Uri feedUri, CancellationToken cancellationToken)
    {
        var xml = await httpClient.GetStringAsync(feedUri, cancellationToken).ConfigureAwait(false);
        var doc = XDocument.Parse(xml);
        XNamespace ns = doc.Root?.GetDefaultNamespace() ?? string.Empty;

        var items = new List<RssFeedItem>();

        foreach (var item in doc.Descendants(ns + "item"))
        {
            var title = (string?)item.Element("title") ?? string.Empty;
            var linkText = (string?)item.Element("link");
            if (string.IsNullOrWhiteSpace(linkText))
            {
                continue;
            }

            if (!Uri.TryCreate(linkText, UriKind.Absolute, out var link))
            {
                continue;
            }

            DateTimeOffset? published = null;
            var pubDate = (string?)item.Element("pubDate");
            if (pubDate != null && DateTimeOffset.TryParse(pubDate, out var parsed))
            {
                published = parsed;
            }

            var description = (string?)item.Element("description");
            items.Add(new RssFeedItem(title, link, published, description));
        }

        return new RssFeedDocument(feedUri, items);
    }
}
