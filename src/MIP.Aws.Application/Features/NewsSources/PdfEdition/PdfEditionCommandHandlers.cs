using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Features.NewsSources.PdfEdition;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public sealed class DiscoverPdfEditionCommandHandler(IPdfEditionDownloadService service)
    : IRequestHandler<DiscoverPdfEditionCommand, PdfEditionDownloadOutcome>
{
    public Task<PdfEditionDownloadOutcome> Handle(DiscoverPdfEditionCommand request, CancellationToken cancellationToken) =>
        service.DiscoverOnlyAsync(request.NewsSourceId, cancellationToken);
}

public sealed class DownloadTodayPdfCommandHandler(IPdfEditionDownloadService service)
    : IRequestHandler<DownloadTodayPdfCommand, PdfEditionDownloadOutcome>
{
    public Task<PdfEditionDownloadOutcome> Handle(DownloadTodayPdfCommand request, CancellationToken cancellationToken) =>
        service.DownloadTodayAsync(request.NewsSourceId, request.EnqueueOcr, cancellationToken);
}

public sealed class UploadTodayPdfCommandHandler(IPdfEditionDownloadService service)
    : IRequestHandler<UploadTodayPdfCommand, PdfEditionDownloadOutcome>
{
    public Task<PdfEditionDownloadOutcome> Handle(UploadTodayPdfCommand request, CancellationToken cancellationToken) =>
        service.UploadTodayAsync(
            request.NewsSourceId,
            request.FileName,
            request.Content,
            request.EnqueueOcr,
            cancellationToken,
            request.FromExternalRpa);
}

public sealed class DownloadManualPdfCommandHandler(IPdfEditionDownloadService service)
    : IRequestHandler<DownloadManualPdfCommand, PdfEditionDownloadOutcome>
{
    public Task<PdfEditionDownloadOutcome> Handle(DownloadManualPdfCommand request, CancellationToken cancellationToken) =>
        service.DownloadManualAsync(
            request.NewsSourceId,
            request.ManualUrl,
            request.SaveAsDiscoveryPageUrl,
            request.EnqueueOcr,
            cancellationToken);
}

public sealed class GetLatestPdfEditionQueryHandler(IPdfEditionDownloadService service)
    : IRequestHandler<GetLatestPdfEditionQuery, PdfEditionDownloadOutcome?>
{
    public Task<PdfEditionDownloadOutcome?> Handle(GetLatestPdfEditionQuery request, CancellationToken cancellationToken) =>
        service.GetLatestAsync(request.NewsSourceId, cancellationToken);
}
