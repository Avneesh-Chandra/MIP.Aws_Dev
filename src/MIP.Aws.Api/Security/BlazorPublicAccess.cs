namespace MIP.Aws.API.Security;

/// <summary>
/// Paths reachable without authentication (static assets, swagger, health, login).
/// </summary>
public static class BlazorPublicAccess
{
    public static bool IsAnonymousPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var p = path.TrimStart('/');

        if (p.StartsWith("_framework", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("_content/", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("_blazor", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.StartsWith("health", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("swagger", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (p.Equals("Account/Login", StringComparison.OrdinalIgnoreCase)
            || p.StartsWith("Account/Login/", StringComparison.OrdinalIgnoreCase)
            || p.Equals("Account/AccessDenied", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return p.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
               || p.EndsWith(".css", StringComparison.OrdinalIgnoreCase)
               || p.EndsWith(".map", StringComparison.OrdinalIgnoreCase);
    }
}
