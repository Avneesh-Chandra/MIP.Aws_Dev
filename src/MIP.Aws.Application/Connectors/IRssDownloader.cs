namespace MIP.Aws.Application.Connectors;

public interface IRssDownloader
{
    Task<RssFeedDocument> DownloadAsync(Uri feedUri, CancellationToken cancellationToken);
}
