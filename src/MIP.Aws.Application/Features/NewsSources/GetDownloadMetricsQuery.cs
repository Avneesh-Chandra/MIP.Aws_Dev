using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record GetDownloadMetricsQuery : IRequest<DownloadMetricsDto>;
