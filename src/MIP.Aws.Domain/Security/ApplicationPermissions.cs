namespace MIP.Aws.Domain.Security;

/// <summary>Fine-grained permission keys used in policies, JWT/cookie claims, and role matrices.</summary>
public static class ApplicationPermissions
{
    public const string UsersView = "Users.View";
    public const string UsersCreate = "Users.Create";
    public const string UsersUpdate = "Users.Update";
    public const string UsersDelete = "Users.Delete";

    public const string RolesView = "Roles.View";
    public const string RolesManage = "Roles.Manage";

    public const string SourcesView = "Sources.View";
    public const string SourcesManage = "Sources.Manage";
    public const string SourcesTest = "Sources.Test";
    public const string SourcesDownload = "Sources.Download";

    public const string ReviewView = "Review.View";
    public const string ReviewEdit = "Review.Edit";
    public const string ReviewApprove = "Review.Approve";

    public const string BriefsBuild = "Briefs.Build";
    public const string BriefsApprove = "Briefs.Approve";
    public const string BriefsPublish = "Briefs.Publish";

    public const string DailyBriefView = "DailyBrief.View";
    public const string DailyBriefGenerate = "DailyBrief.Generate";
    public const string DailyBriefEdit = "DailyBrief.Edit";
    public const string DailyBriefApprove = "DailyBrief.Approve";
    public const string DailyBriefSend = "DailyBrief.Send";
    public const string DailyBriefSettings = "DailyBrief.Settings";
    public const string DailyBriefAudit = "DailyBrief.Audit";

    public const string DashboardExecutiveView = "Dashboard.Executive.View";
    public const string DashboardAnalystView = "Dashboard.Analyst.View";
    public const string DashboardApproverView = "Dashboard.Approver.View";
    public const string DashboardComplianceView = "Dashboard.Compliance.View";
    public const string DashboardOperationsView = "Dashboard.Operations.View";
    public const string DashboardSuperAdminView = "Dashboard.SuperAdmin.View";

    public const string ReportsView = "Reports.View";
    public const string ReportsGenerate = "Reports.Generate";
    public const string ReportsApprove = "Reports.Approve";
    public const string ReportsDownload = "Reports.Download";

    public const string MarketView = "Market.View";
    public const string MarketManage = "Market.Manage";

    public const string AuditView = "Audit.View";
    public const string AuditExport = "Audit.Export";

    public const string JobsView = "Jobs.View";
    public const string JobsManage = "Jobs.Manage";

    public const string SystemSettingsManage = "SystemSettings.Manage";

    public const string SocialView = "Social.View";
    public const string SocialViewPublished = "Social.ViewPublished";
    public const string SocialDraft = "Social.Draft";
    public const string SocialApprove = "Social.Approve";
    public const string SocialPublish = "Social.Publish";
    public const string SocialAccountsManage = "Social.Accounts.Manage";
    public const string SocialPublishDirect = "Social.Publish.Direct";

    public const string OperatorDashboardView = "OperatorDashboard.View";
    public const string DownloadStatusView = "DownloadStatus.View";
    public const string LatestPdfView = "LatestPdf.View";
    public const string DownloadFailureView = "DownloadFailure.View";
    public const string ManualInterventionView = "ManualIntervention.View";
    public const string AdminNotificationCreate = "AdminNotification.Create";
    public const string OperatorNotesCreate = "OperatorNotes.Create";
    public const string OperatorNotesView = "OperatorNotes.View";
    public const string AdminInterventionView = "AdminIntervention.View";
    public const string AdminInterventionManage = "AdminIntervention.Manage";

    public const string SourceRecoveryView = "SourceRecovery.View";
    public const string SourceRecoveryApply = "SourceRecovery.Apply";
    public const string SourceRecoveryAdmin = "SourceRecovery.Admin";

    public static IReadOnlyList<string> All { get; } =
    [
        UsersView, UsersCreate, UsersUpdate, UsersDelete,
        RolesView, RolesManage,
        SourcesView, SourcesManage, SourcesTest, SourcesDownload,
        ReviewView, ReviewEdit, ReviewApprove,
        BriefsBuild, BriefsApprove, BriefsPublish,
        DailyBriefView, DailyBriefGenerate, DailyBriefEdit, DailyBriefApprove, DailyBriefSend, DailyBriefSettings, DailyBriefAudit,
        DashboardExecutiveView, DashboardAnalystView, DashboardApproverView,
        DashboardComplianceView, DashboardOperationsView, DashboardSuperAdminView,
        ReportsView, ReportsGenerate, ReportsApprove, ReportsDownload,
        MarketView, MarketManage,
        AuditView, AuditExport,
        JobsView, JobsManage,
        SystemSettingsManage,
        SocialView, SocialViewPublished, SocialDraft, SocialApprove, SocialPublish,
        SocialAccountsManage, SocialPublishDirect,
        OperatorDashboardView, DownloadStatusView, LatestPdfView, DownloadFailureView,
        ManualInterventionView, AdminNotificationCreate, OperatorNotesCreate, OperatorNotesView,
        AdminInterventionView, AdminInterventionManage,
        SourceRecoveryView, SourceRecoveryApply, SourceRecoveryAdmin
    ];
}
