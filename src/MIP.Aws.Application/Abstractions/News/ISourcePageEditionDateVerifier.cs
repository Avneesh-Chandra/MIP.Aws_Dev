using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Abstractions.News;

public interface ISourcePageEditionDateVerifier
{
    bool IsSupported(NewsSource source);

    Task<SourcePageEditionDateCheck> VerifyByNavigationAsync(
        NewsSource source,
        Uri? pageUrl,
        DateOnly expectedEditionDate,
        CancellationToken cancellationToken);
}
