using MIP.Aws.Domain.Security;
using Hangfire.Dashboard;

namespace MIP.Aws.API.Security;

public sealed class HangfireDashboardAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();
        return http.User.Identity?.IsAuthenticated == true
               && http.User.IsInRole(ApplicationRoles.SuperAdmin);
    }
}
