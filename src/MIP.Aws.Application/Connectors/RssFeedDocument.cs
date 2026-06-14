namespace MIP.Aws.Application.Connectors;

public sealed record RssFeedItem(string Title, Uri Link, DateTimeOffset? PublishedAt, string? Summary);

public sealed record RssFeedDocument(Uri FeedUri, IReadOnlyList<RssFeedItem> Items);
