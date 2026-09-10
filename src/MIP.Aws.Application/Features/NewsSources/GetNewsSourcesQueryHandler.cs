using MIP.Aws.Application.Abstractions;
using MIP.Aws.Application.Configuration;
using MIP.Aws.Domain.Enums;
using MIP.Aws.Shared.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetNewsSourcesQueryHandler(
    IApplicationDbContext db,
    IOptions<ApplicationDisplayOptions> displayOptions)
    : IRequestHandler<GetNewsSourcesQuery, PagedResult<NewsSourceListItemDto>>
{
    public async Task<PagedResult<NewsSourceListItemDto>> Handle(GetNewsSourcesQuery request, CancellationToken cancellationToken)
    {
        var query = db.NewsSources.AsNoTracking().Include(x => x.SourceCategory).AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(x => x.Name.Contains(term) || x.BaseUrl.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var items = await query
            .OrderBy(x => x.Name)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(x => new NewsSourceListItemDto(
                x.Id,
                x.Name,
                x.SourceType.ToString(),
                x.BaseUrl,
                x.IsEnabled,
                x.DefaultLanguage,
                x.Country,
                x.UseHeadlessBrowser,
                x.LastDownloadAt,
                x.SourceCategory != null ? x.SourceCategory.Name : null,
                x.ManualLoginRequired,
                x.RequiresOtp,
                x.RequiresMfa,
                x.IsDownloadAllowed,
                x.PdfDiscoveryEnabled,
                x.LastPdfDiscoveredAt,
                x.LastPdfDownloadedAt,
                x.AiSelectorSuggestionEnabled,
                x.PublicHtmlExtractionEnabled,
                x.GenerateInternalReportAllowed,
                x.LastPdfDiscoveryOutcome,
                x.LastPublicHtmlExtractedAt,
                null))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var enriched = await EnrichLastDownloadedThroughAsync(items, cancellationToken).ConfigureAwait(false);

        return new PagedResult<NewsSourceListItemDto>
        {
            Items = enriched,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }

    private async Task<IReadOnlyList<NewsSourceListItemDto>> EnrichLastDownloadedThroughAsync(
        IReadOnlyList<NewsSourceListItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var sourceIds = items.Select(x => x.Id).ToList();
        var displayTz = displayOptions.Value.DisplayTimeZoneId;

        var latestPdfRows = await db.PdfEditionDownloads.AsNoTracking()
            .Where(p => !p.IsDeleted
                        && sourceIds.Contains(p.NewsSourceId)
                        && p.Status == PdfEditionStatus.Downloaded)
            .Select(p => new
            {
                p.NewsSourceId,
                p.DownloadedAt,
                p.CreatedAt,
                p.DiscoveryMethod,
                p.FileName,
                Trigger = p.DownloadJob != null ? (DownloadJobTrigger?)p.DownloadJob.Trigger : null,
                JobStatus = p.DownloadJob != null ? (DownloadJobStatus?)p.DownloadJob.Status : null
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var bestBySource = latestPdfRows
            .GroupBy(x => x.NewsSourceId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.DownloadedAt ?? x.CreatedAt).First());

        return items
            .Select(item =>
            {
                if (!bestBySource.TryGetValue(item.Id, out var best))
                {
                    return item with
                    {
                        LastDownloadedThrough = item.LastPdfDownloadedAt is null
                            ? null
                            : "MIP Manual"
                    };
                }

                var through = PdfLastDownloadedThroughFormatter.Format(
                    best.Trigger,
                    best.JobStatus,
                    best.DiscoveryMethod,
                    best.FileName,
                    best.DownloadedAt ?? item.LastPdfDownloadedAt,
                    displayTz);

                return item with { LastDownloadedThrough = through };
            })
            .ToList();
    }
}
