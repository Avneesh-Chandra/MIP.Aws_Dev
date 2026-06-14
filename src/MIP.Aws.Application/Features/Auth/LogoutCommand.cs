using MediatR;

namespace MIP.Aws.Application.Features.Auth;

public sealed record LogoutCommand(Guid UserId, string? RefreshToken, string? IpAddress) : IRequest<Unit>;
