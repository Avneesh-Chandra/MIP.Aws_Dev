using MIP.Aws.Shared.Paging;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record GetNewsSourcesQuery(int Page, int PageSize, string? Search)
    : IRequest<PagedResult<NewsSourceListItemDto>>;
