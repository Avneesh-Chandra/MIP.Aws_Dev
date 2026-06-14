using MediatR;

namespace MIP.Aws.Application.Features.Auth;

public sealed record LoginCommand(string Email, string Password, string? IpAddress) : IRequest<AuthResponseDto>;
