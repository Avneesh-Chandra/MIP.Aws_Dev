using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

public interface IPortalDownloadStrategyResolver
{
    IPortalDownloadStrategy Resolve(NewsSource source);
}
