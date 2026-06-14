using MIP.Aws.Application.Abstractions.Portal;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed class TestNewsSourceLoginCommandHandler(IWebPortalAutomationService portal)
    : IRequestHandler<TestNewsSourceLoginCommand, NewsPortalLoginTestResultDto>
{
    public Task<NewsPortalLoginTestResultDto> Handle(TestNewsSourceLoginCommand request, CancellationToken cancellationToken) =>
        portal.TestLoginAsync(request.NewsSourceId, cancellationToken);
}
