using System.Security.Claims;
using MIP.Aws.Application.Features.Auth;
using MIP.Aws.Domain.Security;
using MIP.Aws.Shared.Responses;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MIP.Aws.API.Controllers;

/// <summary>
/// Authentication and current-user profile endpoints.
/// </summary>
[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(IMediator mediator) : ControllerBase
{
    /// <summary>
    /// Authenticates a user and returns JWT access and refresh tokens.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> LoginAsync(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await mediator.Send(new LoginCommand(request.Email, request.Password, ip), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Login successful"));
    }

    /// <summary>
    /// Exchanges a valid refresh token for a new token pair (rotation).
    /// </summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<AuthResponseDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RefreshAsync(
        [FromBody] RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await mediator.Send(new RefreshTokenCommand(request.RefreshToken, ip), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<AuthResponseDto>.Ok(result, "Token refreshed"));
    }

    /// <summary>
    /// Revokes refresh token(s) for the signed-in user.
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(typeof(ApiResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse>> LogoutAsync(
        [FromBody] LogoutRequest? request,
        CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        await mediator.Send(new LogoutCommand(userId, request?.RefreshToken, ip), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse.Ok("Logged out"));
    }

    /// <summary>
    /// Registers a new user with a system role (SuperAdmin only).
    /// </summary>
    [HttpPost("register")]
    [Authorize(Policy = AuthPolicies.SuperAdminOnly)]
    [ProducesResponseType(typeof(ApiResponse<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<Guid>>> RegisterAsync(
        [FromBody] RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var id = await mediator.Send(
            new RegisterUserCommand(request.Email, request.Password, request.DisplayName, request.RoleName),
            cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<Guid>.Ok(id, "User registered"));
    }

    /// <summary>
    /// Returns the authenticated user's profile and roles.
    /// </summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiResponse<UserProfileDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> MeAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var profile = await mediator.Send(new GetMeQuery(userId), cancellationToken).ConfigureAwait(false);
        return Ok(ApiResponse<UserProfileDto>.Ok(profile, "Profile loaded"));
    }

    private Guid RequireUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub);
        if (value is null || !Guid.TryParse(value, out var id))
        {
            throw new UnauthorizedAccessException("Invalid authentication context.");
        }

        return id;
    }
}

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string? RefreshToken);

public sealed record RegisterRequest(string Email, string Password, string DisplayName, string RoleName);
