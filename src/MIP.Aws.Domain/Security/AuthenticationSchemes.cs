namespace MIP.Aws.Domain.Security;

/// <summary>
/// Canonical authentication scheme names used by the API host. These are referenced
/// by Program.cs, controllers, tests, and the SignalR hubs so we never typo a scheme.
/// </summary>
public static class AuthenticationSchemes
{
    /// <summary>
    /// Composite policy scheme that forwards to JwtBearer when a Bearer token is present
    /// (Authorization header or <c>?access_token=</c> query string for SignalR/WebSocket
    /// upgrades), and to the Identity application cookie otherwise.
    /// </summary>
    public const string SmartAuth = "SmartAuth";
}
