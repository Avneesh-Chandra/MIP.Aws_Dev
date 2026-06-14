namespace MIP.Aws.Application.Connectors;

public sealed record DownloadCandidate(Uri ResourceUri, string? ContentTypeHint, string? Title);
