using MIP.Aws.Domain.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace MIP.Aws.API.Security;

public static class AuthorizationPolicyRegistry
{
    public static AuthorizationOptions Register(AuthorizationOptions options)
    {
        options.FallbackPolicy = new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(AuthenticationSchemes.SmartAuth)
            .RequireAssertion(ctx =>
            {
                if (ctx.Resource is HttpContext http
                    && BlazorPublicAccess.IsAnonymousPath(http.Request.Path.Value))
                {
                    return true;
                }

                return ctx.User?.Identity?.IsAuthenticated == true;
            })
            .Build();

        options.DefaultPolicy = NewBuilder().RequireAuthenticatedUser().Build();

        void Perm(AuthorizationPolicyBuilder p, string permission) =>
            Configure(p).RequireAssertion(ctx => PermissionEvaluator.HasPermission(ctx.User, permission));

        void PermAny(AuthorizationPolicyBuilder p, params string[] permissions) =>
            Configure(p).RequireAssertion(ctx => PermissionEvaluator.HasAnyPermission(ctx.User, permissions));

        void Roles(AuthorizationPolicyBuilder p, params string[] roles) =>
            Configure(p).RequireRole(roles);

        options.AddPolicy(AuthPolicies.SuperAdminOnly, p =>
            Roles(p, ApplicationRoles.SuperAdmin));

        options.AddPolicy(AuthPolicies.UserManagementPolicy, p =>
            PermAny(p, ApplicationPermissions.UsersView, ApplicationPermissions.UsersCreate,
                ApplicationPermissions.UsersUpdate, ApplicationPermissions.UsersDelete));

        options.AddPolicy(AuthPolicies.RoleManagementPolicy, p =>
            PermAny(p, ApplicationPermissions.RolesView, ApplicationPermissions.RolesManage));

        options.AddPolicy(AuthPolicies.SourceManagementPolicy, p =>
            Perm(p, ApplicationPermissions.SourcesManage));

        options.AddPolicy(AuthPolicies.ContentAdminPolicy, p =>
            Roles(p, ApplicationRoles.SuperAdmin, ApplicationRoles.ContentAdmin));

        options.AddPolicy(AuthPolicies.AnalystPolicy, p =>
            Roles(p, ApplicationRoles.SuperAdmin, ApplicationRoles.Analyst));

        options.AddPolicy(AuthPolicies.ApproverPolicy, p =>
            Roles(p, ApplicationRoles.SuperAdmin, ApplicationRoles.Approver));

        options.AddPolicy(AuthPolicies.ExecutiveViewerPolicy, p =>
            Roles(p, ApplicationRoles.SuperAdmin, ApplicationRoles.ExecutiveViewer));

        options.AddPolicy(AuthPolicies.ComplianceAuditorPolicy, p =>
            Roles(p, ApplicationRoles.SuperAdmin, ApplicationRoles.ComplianceAuditor));

        options.AddPolicy(AuthPolicies.AdminOrAnalyst, p =>
            PermAny(p, ApplicationPermissions.SourcesManage, ApplicationPermissions.ReviewEdit,
                ApplicationPermissions.ReviewView));

        options.AddPolicy(AuthPolicies.ExecutiveOnly, p =>
            Perm(p, ApplicationPermissions.DashboardExecutiveView));

        options.AddPolicy(AuthPolicies.ComplianceOnly, p =>
            PermAny(p, ApplicationPermissions.AuditView, ApplicationPermissions.AuditExport));

        options.AddPolicy(AuthPolicies.ExecutiveDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardExecutiveView));

        options.AddPolicy(AuthPolicies.AnalystDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardAnalystView));

        options.AddPolicy(AuthPolicies.ApproverDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardApproverView));

        options.AddPolicy(AuthPolicies.SuperAdminDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardSuperAdminView));

        options.AddPolicy(AuthPolicies.ContentAdminDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardOperationsView));

        options.AddPolicy(AuthPolicies.ComplianceDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardComplianceView));

        options.AddPolicy(AuthPolicies.SystemHealthDashboard, p =>
            Perm(p, ApplicationPermissions.DashboardOperationsView));

        options.AddPolicy(AuthPolicies.ReviewStudioRead, p =>
            Perm(p, ApplicationPermissions.ReviewView));

        options.AddPolicy(AuthPolicies.ReviewEditPolicy, p =>
            Perm(p, ApplicationPermissions.ReviewEdit));

        options.AddPolicy(AuthPolicies.ReviewStudioWrite, p =>
            Perm(p, ApplicationPermissions.ReviewEdit));

        options.AddPolicy(AuthPolicies.ReviewApprovePolicy, p =>
            Perm(p, ApplicationPermissions.ReviewApprove));

        options.AddPolicy(AuthPolicies.ReviewStudioApprove, p =>
            Perm(p, ApplicationPermissions.ReviewApprove));

        options.AddPolicy(AuthPolicies.ExecutiveQueueRead, p =>
            PermAny(p, ApplicationPermissions.ReviewView, ApplicationPermissions.DashboardExecutiveView,
                ApplicationPermissions.DashboardApproverView));

        options.AddPolicy(AuthPolicies.ExecutiveBriefPublish, p =>
            Perm(p, ApplicationPermissions.BriefsPublish));

        options.AddPolicy(AuthPolicies.ReportApprovalPolicy, p =>
            Perm(p, ApplicationPermissions.ReportsApprove));

        options.AddPolicy(AuthPolicies.ReportsAccess, p =>
            Perm(p, ApplicationPermissions.ReportsView));

        options.AddPolicy(AuthPolicies.AuditReadPolicy, p =>
            Perm(p, ApplicationPermissions.AuditView));

        options.AddPolicy(AuthPolicies.AuditExportPolicy, p =>
            Perm(p, ApplicationPermissions.AuditExport));

        options.AddPolicy(AuthPolicies.JobsManagePolicy, p =>
            Perm(p, ApplicationPermissions.JobsManage));

        options.AddPolicy(AuthPolicies.MarketDataRead, p =>
            Perm(p, ApplicationPermissions.MarketView));

        options.AddPolicy(AuthPolicies.MarketDataWrite, p =>
            Perm(p, ApplicationPermissions.MarketManage));

        options.AddPolicy(AuthPolicies.MarketDataAdmin, p =>
            Roles(p, ApplicationRoles.SuperAdmin));

        options.AddPolicy(AuthPolicies.MarketDataAudit, p =>
            PermAny(p, ApplicationPermissions.AuditView, ApplicationPermissions.AuditExport));

        options.AddPolicy(AuthPolicies.NewsSourcePdfWrite, p =>
            PermAny(p, ApplicationPermissions.SourcesManage, ApplicationPermissions.SourcesTest));

        options.AddPolicy(AuthPolicies.NewsSourcePdfRead, p =>
            PermAny(p, ApplicationPermissions.SourcesView, ApplicationPermissions.SourcesManage,
                ApplicationPermissions.AuditView, ApplicationPermissions.LatestPdfView));

        options.AddPolicy(AuthPolicies.NewsSourcePdfAudit, p =>
            Perm(p, ApplicationPermissions.AuditView));

        options.AddPolicy(AuthPolicies.SocialRead, p =>
            PermAny(p, ApplicationPermissions.SocialView, ApplicationPermissions.SocialViewPublished));

        options.AddPolicy(AuthPolicies.SocialDraft, p =>
            Perm(p, ApplicationPermissions.SocialDraft));

        options.AddPolicy(AuthPolicies.SocialApprove, p =>
            Perm(p, ApplicationPermissions.SocialApprove));

        options.AddPolicy(AuthPolicies.SocialPublish, p =>
            PermAny(p, ApplicationPermissions.SocialPublish, ApplicationPermissions.SocialPublishDirect));

        options.AddPolicy(AuthPolicies.SocialAccountsManage, p =>
            PermAny(p, ApplicationPermissions.SocialAccountsManage, ApplicationPermissions.SocialPublishDirect));

        options.AddPolicy(AuthPolicies.SocialViewPublished, p =>
            Perm(p, ApplicationPermissions.SocialViewPublished));

        options.AddPolicy(AuthPolicies.DailyBriefView, p =>
            Perm(p, ApplicationPermissions.DailyBriefView));

        options.AddPolicy(AuthPolicies.DailyBriefEdit, p =>
            PermAny(p, ApplicationPermissions.DailyBriefGenerate, ApplicationPermissions.DailyBriefEdit));

        options.AddPolicy(AuthPolicies.DailyBriefApprove, p =>
            Perm(p, ApplicationPermissions.DailyBriefApprove));

        options.AddPolicy(AuthPolicies.DailyBriefSend, p =>
            Perm(p, ApplicationPermissions.DailyBriefSend));

        options.AddPolicy(AuthPolicies.DailyBriefSettings, p =>
            Perm(p, ApplicationPermissions.DailyBriefSettings));

        options.AddPolicy(AuthPolicies.OperatorDashboard, p =>
            Perm(p, ApplicationPermissions.OperatorDashboardView));

        options.AddPolicy(AuthPolicies.OperatorDownloadMonitor, p =>
            PermAny(p, ApplicationPermissions.OperatorDashboardView, ApplicationPermissions.DownloadStatusView));

        options.AddPolicy(AuthPolicies.OperatorLatestPdf, p =>
            Perm(p, ApplicationPermissions.LatestPdfView));

        options.AddPolicy(AuthPolicies.OperatorFailureDetails, p =>
            Perm(p, ApplicationPermissions.DownloadFailureView));

        options.AddPolicy(AuthPolicies.OperatorNotes, p =>
            PermAny(p, ApplicationPermissions.OperatorNotesCreate, ApplicationPermissions.OperatorNotesView));

        options.AddPolicy(AuthPolicies.OperatorInformAdmin, p =>
            Perm(p, ApplicationPermissions.AdminNotificationCreate));

        options.AddPolicy(AuthPolicies.AdminInterventionQueue, p =>
            PermAny(p, ApplicationPermissions.AdminInterventionView, ApplicationPermissions.AdminInterventionManage));

        options.AddPolicy(AuthPolicies.AdminInterventionManage, p =>
            Perm(p, ApplicationPermissions.AdminInterventionManage));

        options.AddPolicy(AuthPolicies.SourceRecoveryView, p =>
            Perm(p, ApplicationPermissions.SourceRecoveryView));

        options.AddPolicy(AuthPolicies.SourceRecoveryApply, p =>
            Perm(p, ApplicationPermissions.SourceRecoveryApply));

        options.AddPolicy(AuthPolicies.SourceRecoveryAdmin, p =>
            Perm(p, ApplicationPermissions.SourceRecoveryAdmin));

        return options;
    }

    private static AuthorizationPolicyBuilder NewBuilder() =>
        new AuthorizationPolicyBuilder()
            .AddAuthenticationSchemes(AuthenticationSchemes.SmartAuth);

    private static AuthorizationPolicyBuilder Configure(AuthorizationPolicyBuilder builder) =>
        builder
            .AddAuthenticationSchemes(AuthenticationSchemes.SmartAuth)
            .RequireAuthenticatedUser();
}
