using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Abstractions.Downloading;
using MIP.Aws.Application.Abstractions.News;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Enums;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MIP.Aws.Infrastructure.Jobs;

[Queue(HangfireQueueOptions.Names.Downloads)]
public sealed class PdfEditionJobs(IServiceScopeFactory scopeFactory, ILogger<PdfEditionJobs> logger)
{
    private sealed record EligibleSource(
        Guid Id,
        string Name,
        string? PdfDiscoveryPageUrl,
        string BaseUrl,
        string? ConnectorKey);
    [AutomaticRetry(Attempts = 2, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task DiscoverAndDownloadTodayPdfAsync(Guid newsSourceId)
    {
        using var scope = scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IPdfEditionDownloadService>();
        using (DownloadExecutionContext.UseTrigger(DownloadJobTrigger.Scheduled))
        {
            var result = await service.DownloadTodayAsync(newsSourceId, enqueueOcr: true, CancellationToken.None).ConfigureAwait(false);
            logger.LogInformation(
                "PDF edition job for {SourceId}: {Status} ({Path})",
                newsSourceId,
                result.Status,
                result.SavedPath);
        }
    }

    [AutomaticRetry(Attempts = 1, OnAttemptsExceeded = AttemptsExceededAction.Delete)]
    public async Task DiscoverAndDownloadAllEligibleAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<IPdfEditionDownloadService>();
        var notifications = scope.ServiceProvider.GetRequiredService<IPdfEditionAdminNotificationService>();

        var sources = await db.NewsSources.AsNoTracking()
            .Where(s => !s.IsDeleted && s.IsEnabled && s.PdfDiscoveryEnabled && s.IsDownloadAllowed)
            .Where(s => s.SourceType == NewsSourceType.PublicHtml || s.SourceType == NewsSourceType.PublicPdf)
            .Select(s => new EligibleSource(
                s.Id,
                s.Name,
                s.PdfDiscoveryPageUrl,
                s.BaseUrl,
                s.ConnectorKey))
            .ToListAsync(CancellationToken.None)
            .ConfigureAwait(false);

        var needsManualAction = new List<PdfEditionJobResult>();
        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);

        foreach (var source in sources)
        {
            try
            {
                var result = await service.DownloadTodayAsync(source.Id, enqueueOcr: true, CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "PDF edition daily job for {Source}: {Status}",
                    source.Name,
                    result.Status);

                if (RequiresManualAction(result.Status))
                {
                    needsManualAction.Add(ToJobResult(source, result));
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "PDF edition daily job failed for source {SourceId}", source.Id);
                needsManualAction.Add(new PdfEditionJobResult(
                    source.Id,
                    source.Name,
                    source.PdfDiscoveryPageUrl ?? source.BaseUrl,
                    source.ConnectorKey,
                    PdfEditionStatus.Failed,
                    ex.Message,
                    null));
            }
        }

        if (needsManualAction.Count > 0)
        {
            await notifications.SendManualActionRequiredAsync(needsManualAction, editionDate, CancellationToken.None)
                .ConfigureAwait(false);
        }
    }

    private static bool RequiresManualAction(PdfEditionStatus status) =>
        status is PdfEditionStatus.NoPublicPdfAvailable
            or PdfEditionStatus.Failed
            or PdfEditionStatus.BlockedByCompliance;

    private static PdfEditionJobResult ToJobResult(EligibleSource source, PdfEditionDownloadOutcome outcome) =>
        new(
            source.Id,
            source.Name,
            source.PdfDiscoveryPageUrl ?? source.BaseUrl,
            source.ConnectorKey,
            outcome.Status,
            outcome.FailureReason,
            outcome.SourceUrl);
}
