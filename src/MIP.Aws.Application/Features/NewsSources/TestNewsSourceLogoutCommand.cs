using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record TestNewsSourceLogoutCommand(Guid NewsSourceId) : IRequest<NewsPortalLogoutTestResultDto>;
