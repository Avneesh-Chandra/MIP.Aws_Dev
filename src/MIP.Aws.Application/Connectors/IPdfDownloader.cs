namespace MIP.Aws.Application.Connectors;

public interface IPdfDownloader
{
    Task<DownloadedContent> DownloadPdfAsync(Uri resource, CancellationToken cancellationToken);
}
