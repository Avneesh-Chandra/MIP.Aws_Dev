using MediatR;

namespace MIP.Aws.Application.Features.Auth;

public sealed record RefreshTokenCommand(string RefreshToken, string? IpAddress) : IRequest<AuthResponseDto>;
