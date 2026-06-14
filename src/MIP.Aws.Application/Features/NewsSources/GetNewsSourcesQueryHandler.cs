using MIP.Aws.Application.Abstractions;
using MIP.Aws.Shared.Paging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class GetNewsSourcesQueryHandler(IApplicationDbContext db)
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
                x.LastPublicHtmlExtractedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new PagedResult<NewsSourceListItemDto>
        {
            Items = items,
            Page = request.Page,
            PageSize = request.PageSize,
            TotalCount = total
        };
    }
}
