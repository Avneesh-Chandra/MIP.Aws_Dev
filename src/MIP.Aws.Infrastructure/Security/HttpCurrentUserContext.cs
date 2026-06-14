using System.Security.Claims;
using MIP.Aws.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace MIP.Aws.Infrastructure.Security;

/// <summary>
/// HttpContext-backed implementation of <see cref="ICurrentUserContext"/>. Returns null/empty
/// values for background jobs (no ambient HttpContext). Always consults the SmartAuth-resolved
/// principal so both JWT and cookie identities work.
/// </summary>
public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    public Guid? UserId
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            var claim = user.FindFirst("sub")
                ?? user.FindFirst("nameid")
                ?? user.FindFirst(ClaimTypes.NameIdentifier);
            return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
        }
    }

    public string? Email
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return null;
            }

            return user.FindFirst(ClaimTypes.Email)?.Value
                ?? user.FindFirst("email")?.Value
                ?? user.Identity?.Name;
        }
    }

    public IReadOnlyList<string> Roles
    {
        get
        {
            var user = accessor.HttpContext?.User;
            if (user?.Identity?.IsAuthenticated != true)
            {
                return Array.Empty<string>();
            }

            return user.FindAll(ClaimTypes.Role).Select(c => c.Value).Distinct().ToList();
        }
    }

    public bool IsInRole(string role) => accessor.HttpContext?.User?.IsInRole(role) == true;
}
