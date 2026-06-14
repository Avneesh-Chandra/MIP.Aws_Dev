namespace MIP.Aws.Domain.Security;

/// <summary>Maps system roles to fine-grained permissions (source of truth for RBAC).</summary>
public static class RolePermissionMatrix
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        BuildMatrix();

    public static IReadOnlySet<string> GetPermissionsForRole(string role) =>
        RolePermissions.TryGetValue(role, out var set) ? set : Empty;

    public static IReadOnlySet<string> GetPermissionsForRoles(IEnumerable<string> roles)
    {
        var merged = new HashSet<string>(StringComparer.Ordinal);
        foreach (var role in roles)
        {
            foreach (var permission in GetPermissionsForRole(role))
            {
                merged.Add(permission);
            }
        }

        return merged;
    }

    public static bool RoleHasPermission(string role, string permission) =>
        GetPermissionsForRole(role).Contains(permission);

    private static readonly IReadOnlySet<string> Empty = new HashSet<string>();

    private static IReadOnlyDictionary<string, IReadOnlySet<string>> BuildMatrix()
    {
        var map = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal);

        map[ApplicationRoles.SuperAdmin] = new HashSet<string>(ApplicationPermissions.All, StringComparer.Ordinal);

        map[ApplicationRoles.ContentAdmin] = Set(
            ApplicationPermissions.SourcesView,
            ApplicationPermissions.SourcesManage,
            ApplicationPermissions.SourcesTest,
            ApplicationPermissions.SourcesDownload,
            ApplicationPermissions.DashboardOperationsView,
            ApplicationPermissions.DailyBriefView,
            ApplicationPermissions.DailyBriefSettings,
            ApplicationPermissions.ReportsView,
            ApplicationPermissions.JobsView,
            ApplicationPermissions.ReportsDownload,
            ApplicationPermissions.OperatorDashboardView,
            ApplicationPermissions.DownloadStatusView,
            ApplicationPermissions.LatestPdfView,
            ApplicationPermissions.DownloadFailureView,
            ApplicationPermissions.ManualInterventionView,
            ApplicationPermissions.OperatorNotesView,
            ApplicationPermissions.AdminInterventionView,
            ApplicationPermissions.AdminInterventionManage,
            ApplicationPermissions.SourceRecoveryView,
            ApplicationPermissions.SourceRecoveryApply,
            ApplicationPermissions.SourceRecoveryAdmin);

        map[ApplicationRoles.MIPOperator] = Set(
            ApplicationPermissions.OperatorDashboardView,
            ApplicationPermissions.DownloadStatusView,
            ApplicationPermissions.LatestPdfView,
            ApplicationPermissions.DownloadFailureView,
            ApplicationPermissions.ManualInterventionView,
            ApplicationPermissions.AdminNotificationCreate,
            ApplicationPermissions.OperatorNotesCreate,
            ApplicationPermissions.OperatorNotesView,
            ApplicationPermissions.SourceRecoveryView,
            ApplicationPermissions.SourceRecoveryApply);

        map[ApplicationRoles.Analyst] = Set(
            ApplicationPermissions.ReviewView,
            ApplicationPermissions.ReviewEdit,
            ApplicationPermissions.BriefsBuild,
            ApplicationPermissions.DailyBriefView,
            ApplicationPermissions.DailyBriefGenerate,
            ApplicationPermissions.DailyBriefEdit,
            ApplicationPermissions.DashboardAnalystView,
            ApplicationPermissions.ReportsView,
            ApplicationPermissions.ReportsGenerate,
            ApplicationPermissions.MarketView,
            ApplicationPermissions.SocialView,
            ApplicationPermissions.SocialDraft);

        map[ApplicationRoles.Approver] = Set(
            ApplicationPermissions.ReviewView,
            ApplicationPermissions.ReviewEdit,
            ApplicationPermissions.ReviewApprove,
            ApplicationPermissions.BriefsBuild,
            ApplicationPermissions.BriefsApprove,
            ApplicationPermissions.BriefsPublish,
            ApplicationPermissions.DailyBriefView,
            ApplicationPermissions.DailyBriefGenerate,
            ApplicationPermissions.DailyBriefEdit,
            ApplicationPermissions.DailyBriefApprove,
            ApplicationPermissions.DailyBriefSend,
            ApplicationPermissions.DashboardApproverView,
            ApplicationPermissions.ReportsView,
            ApplicationPermissions.ReportsApprove,
            ApplicationPermissions.MarketView,
            ApplicationPermissions.SocialView,
            ApplicationPermissions.SocialApprove,
            ApplicationPermissions.SocialPublish);

        map[ApplicationRoles.ExecutiveViewer] = Set(
            ApplicationPermissions.DashboardExecutiveView,
            ApplicationPermissions.DailyBriefView,
            ApplicationPermissions.ReportsView,
            ApplicationPermissions.ReportsDownload,
            ApplicationPermissions.MarketView,
            ApplicationPermissions.SocialViewPublished,
            ApplicationPermissions.OperatorDashboardView,
            ApplicationPermissions.DownloadStatusView,
            ApplicationPermissions.LatestPdfView);

        map[ApplicationRoles.ComplianceAuditor] = Set(
            ApplicationPermissions.DashboardComplianceView,
            ApplicationPermissions.AuditView,
            ApplicationPermissions.AuditExport,
            ApplicationPermissions.DailyBriefView,
            ApplicationPermissions.DailyBriefAudit,
            ApplicationPermissions.SourcesView,
            ApplicationPermissions.JobsView,
            ApplicationPermissions.SocialView);

        return map;
    }

    private static IReadOnlySet<string> Set(params string[] permissions) =>
        new HashSet<string>(permissions, StringComparer.Ordinal);
}
