using MIP.Aws.Application.Abstractions;
using MIP.Aws.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources.PdfEdition;

public sealed class TodayPdfUploadStatusQueryHandler(IApplicationDbContext db)
    : IRequestHandler<GetTodayPdfUploadStatusQuery, IReadOnlyList<TodayPdfUploadSourceDto>>
{
    public async Task<IReadOnlyList<TodayPdfUploadSourceDto>> Handle(
        GetTodayPdfUploadStatusQuery request,
        CancellationToken cancellationToken)
    {
        var editionDate = DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = new DateTimeOffset(editionDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var dayEnd = dayStart.AddDays(1);

        var sources = (await db.NewsSources.AsNoTracking()
                .Where(source => !source.IsDeleted && source.IsEnabled)
                .OrderBy(source => source.Name)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
            .Where(PdfManagementSourceRules.IsPdfDownloadMonitoredSource)
            .ToList();

        if (sources.Count == 0)
        {
            return [];
        }

        var sourceIds = sources.Select(source => source.Id).ToList();
        var publicRows = await db.PdfEditionDownloads.AsNoTracking()
            .Where(row => !row.IsDeleted
                          && sourceIds.Contains(row.NewsSourceId)
                          && row.DownloadedFileId != null
                          && (row.Status == PdfEditionStatus.Downloaded
                              || row.Status == PdfEditionStatus.SkippedDuplicate)
                          && (row.EditionDate == editionDate
                              || (row.DownloadedAt >= dayStart && row.DownloadedAt < dayEnd)))
            .Select(row => new AvailableFile(
                row.NewsSourceId,
                row.DownloadedFileId,
                row.FileName,
                row.FileSizeBytes,
                row.DownloadedAt ?? row.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var successfulJobs = await db.DownloadJobs.AsNoTracking()
            .Where(job => !job.IsDeleted
                          && sourceIds.Contains(job.NewsSourceId)
                          && (job.Status == DownloadJobStatus.Succeeded
                              || job.Status == DownloadJobStatus.SuccessWithAutoAiRecovery)
                          && job.CompletedAt >= dayStart
                          && job.CompletedAt < dayEnd)
            .Select(job => new { job.Id, job.NewsSourceId, job.CompletedAt, job.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var jobIds = successfulJobs.Select(job => job.Id).ToList();
        var portalFiles = jobIds.Count == 0
            ? []
            : await db.DownloadedFiles.AsNoTracking()
                .Where(file => !file.IsDeleted && jobIds.Contains(file.DownloadJobId))
                .Select(file => new { file.Id, file.DownloadJobId, file.SizeBytes })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

        var jobById = successfulJobs.ToDictionary(job => job.Id);
        var portalAvailable = portalFiles
            .Where(file => jobById.ContainsKey(file.DownloadJobId))
            .Select(file =>
            {
                var job = jobById[file.DownloadJobId];
                return new AvailableFile(
                    job.NewsSourceId,
                    file.Id,
                    null,
                    file.SizeBytes,
                    job.CompletedAt ?? job.CreatedAt);
            });

        var availableBySource = publicRows
            .Concat(portalAvailable)
            .GroupBy(file => file.SourceId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderByDescending(file => file.OccurredAt).First());

        return sources
            .Select(source =>
            {
                availableBySource.TryGetValue(source.Id, out var file);
                var fileName = file?.FileName;
                if (string.IsNullOrWhiteSpace(fileName) && file is not null)
                {
                    fileName = BuildFallbackFileName(source.Name, editionDate);
                }

                return new TodayPdfUploadSourceDto(
                    source.Id,
                    source.Name,
                    source.SourceType.ToString(),
                    file is not null,
                    file?.FileId,
                    fileName,
                    file?.SizeBytes,
                    file?.OccurredAt);
            })
            .ToList();
    }

    private static string BuildFallbackFileName(string sourceName, DateOnly editionDate)
    {
        var safeName = string.Concat(sourceName.Select(character =>
            char.IsLetterOrDigit(character) || character is ' ' or '-' or '_' ? character : '-'));
        return $"{safeName.Trim().Replace(' ', '-')}-{editionDate:yyyy-MM-dd}.pdf";
    }

    private sealed record AvailableFile(
        Guid SourceId,
        Guid? FileId,
        string? FileName,
        long? SizeBytes,
        DateTimeOffset OccurredAt);
}
