using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record EnqueueNewsSourceDownloadCommand(Guid NewsSourceId) : IRequest<Unit>;
