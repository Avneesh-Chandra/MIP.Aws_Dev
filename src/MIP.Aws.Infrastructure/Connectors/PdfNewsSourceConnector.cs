using MIP.Aws.Application.Connectors;
using MIP.Aws.Domain.Entities;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Infrastructure.Connectors;

public sealed class PdfNewsSourceConnector : INewsSourceConnector, IPdfNewsConnector
{
    public string ConnectorKey => "default.pdf";

    public NewsSourceType SupportedType => NewsSourceType.PublicPdf;

    public Task<IReadOnlyList<DownloadCandidate>> BuildDownloadPlanAsync(NewsSource source, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(source.BaseUrl, UriKind.Absolute, out var uri))
        {
            return Task.FromResult<IReadOnlyList<DownloadCandidate>>(Array.Empty<DownloadCandidate>());
        }

        IReadOnlyList<DownloadCandidate> plan = [new DownloadCandidate(uri, "application/pdf", source.Name)];
        return Task.FromResult(plan);
    }
}
