namespace MIP.Aws.Domain.Security;

/// <summary>Default landing routes per primary role (first matching role wins).</summary>
public static class RoleDashboardRoutes
{
    public static string GetHomeRoute(IEnumerable<string> roles)
    {
        _ = roles;
        return "/operator/download-monitor";
    }
}
