using MIP.Aws.Application.Abstractions.Portal;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourceLogoutCommandHandler(IWebPortalAutomationService portal)
    : IRequestHandler<TestNewsSourceLogoutCommand, NewsPortalLogoutTestResultDto>
{
    public Task<NewsPortalLogoutTestResultDto> Handle(TestNewsSourceLogoutCommand request, CancellationToken cancellationToken) =>
        portal.TestLogoutAsync(request.NewsSourceId, cancellationToken);
}
