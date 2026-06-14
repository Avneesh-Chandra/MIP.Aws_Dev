namespace MIP.Aws.Application.Connectors;

public sealed record DownloadedContent(
    Uri SourceUri,
    byte[] Payload,
    string ContentType,
    IReadOnlyDictionary<string, string>? Headers,
    int HttpStatusCode);
