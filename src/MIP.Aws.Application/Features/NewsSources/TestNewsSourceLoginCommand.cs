using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record TestNewsSourceLoginCommand(Guid NewsSourceId) : IRequest<NewsPortalLoginTestResultDto>;
