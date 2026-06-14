using MIP.Aws.Domain.Enums;
using MediatR;

namespace MIP.Aws.Application.Features.NewsSources;

public sealed record TestNewsSourceConnectionCommand(string BaseUrl, ContentAcquisitionMode AcquisitionMode) : IRequest<NewsSourceConnectionTestResult>;

public sealed record NewsSourceConnectionTestResult(bool Success, string Message, int? HttpStatus);
