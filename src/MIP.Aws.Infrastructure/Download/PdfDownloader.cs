using MIP.Aws.Application.Connectors;

namespace MIP.Aws.Infrastructure.Download;

public sealed class PdfDownloader(IContentDownloader contentDownloader) : IPdfDownloader
{
    public Task<DownloadedContent> DownloadPdfAsync(Uri resource, CancellationToken cancellationToken) =>
        contentDownloader.DownloadAsync(resource, null, cancellationToken);
}
