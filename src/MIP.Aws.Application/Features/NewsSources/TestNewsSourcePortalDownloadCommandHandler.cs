using MIP.Aws.Application.Abstractions.Portal;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourcePortalDownloadCommandHandler(IWebPortalAutomationService portal)
    : IRequestHandler<TestNewsSourcePortalDownloadCommand, NewsPortalDownloadTestResultDto>
{
    public Task<NewsPortalDownloadTestResultDto> Handle(TestNewsSourcePortalDownloadCommand request, CancellationToken cancellationToken) =>
        portal.TestDownloadAsync(request.NewsSourceId, cancellationToken);
}
