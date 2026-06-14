using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Features.NewsSources;
using MIP.Aws.Application.Portal;
using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Infrastructure.Portal;

public sealed class PortalDownloadStrategyResolver(
    PressReaderDownloadStrategy pressReader,
    GenericWebPortalDownloadStrategy generic) : IPortalDownloadStrategyResolver
{
    public IPortalDownloadStrategy Resolve(NewsSource source)
    {
        var key = PortalFieldMapper.NormalizeStrategyKey(source.PortalStrategyKey);
        if (string.Equals(key, PortalStrategyKeys.PressReader, StringComparison.Ordinal)
            || PressReaderPortalLogin.IsPressReaderSource(source))
        {
            return pressReader;
        }

        return generic;
    }
}
