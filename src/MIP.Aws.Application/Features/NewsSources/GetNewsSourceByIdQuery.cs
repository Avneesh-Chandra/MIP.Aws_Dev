using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record GetNewsSourceByIdQuery(Guid Id) : IRequest<NewsSourceDetailDto>;
