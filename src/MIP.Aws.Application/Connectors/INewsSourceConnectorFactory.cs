using MIP.Aws.Domain.Entities;

namespace MIP.Aws.Application.Connectors;

public interface INewsSourceConnectorFactory
{
    INewsSourceConnector Resolve(NewsSource source);
}
