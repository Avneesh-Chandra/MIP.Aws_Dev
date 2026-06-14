using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record DownloadNewsSourceEditionCommand(Guid NewsSourceId) : IRequest<Unit>;
