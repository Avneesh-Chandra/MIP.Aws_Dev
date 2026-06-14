namespace MIP.Aws.Domain.Security;

/// <summary>
/// Authorization policy names mapped to permission requirements in the API host.
/// </summary>
public static class AuthPolicies
{
    public const string SuperAdminOnly = "SuperAdminOnly";

    public const string AdminOrAnalyst = "AdminOrAnalyst";

    public const string ContentAdminPolicy = "ContentAdminPolicy";

    public const string AnalystPolicy = "AnalystPolicy";

    public const string ApproverPolicy = "ApproverPolicy";

    public const string ExecutiveViewerPolicy = "ExecutiveViewerPolicy";

    public const string ComplianceAuditorPolicy = "ComplianceAuditorPolicy";

    public const string UserManagementPolicy = "UserManagementPolicy";

    public const string RoleManagementPolicy = "RoleManagementPolicy";

    public const string SourceManagementPolicy = "SourceManagementPolicy";

    public const string ReviewEditPolicy = "ReviewEditPolicy";

    public const string ReviewApprovePolicy = "ReviewApprovePolicy";

    public const string ReportApprovalPolicy = "ReportApprovalPolicy";

    public const string AuditReadPolicy = "AuditReadPolicy";

    public const string AuditExportPolicy = "AuditExportPolicy";

    public const string JobsManagePolicy = "JobsManagePolicy";

    public const string ExecutiveOnly = "ExecutiveOnly";

    public const string ComplianceOnly = "ComplianceOnly";

    public const string ExecutiveDashboard = "ExecutiveDashboard";

    public const string AnalystDashboard = "AnalystDashboard";

    public const string ApproverDashboard = "ApproverDashboard";

    public const string SuperAdminDashboard = "SuperAdminDashboard";

    public const string ContentAdminDashboard = "ContentAdminDashboard";

    public const string ComplianceDashboard = "ComplianceDashboard";

    public const string SystemHealthDashboard = "SystemHealthDashboard";

    public const string ReportsAccess = "ReportsAccess";

    public const string ReviewStudioRead = "ReviewStudioRead";

    public const string ReviewStudioWrite = "ReviewStudioWrite";

    public const string ReviewStudioApprove = "ReviewStudioApprove";

    public const string ExecutiveQueueRead = "ExecutiveQueueRead";

    public const string ExecutiveBriefPublish = "ExecutiveBriefPublish";

    public const string MarketDataRead = "MarketDataRead";

    public const string MarketDataWrite = "MarketDataWrite";

    public const string MarketDataAdmin = "MarketDataAdmin";

    public const string MarketDataAudit = "MarketDataAudit";

    public const string NewsSourcePdfWrite = "NewsSourcePdfWrite";

    public const string NewsSourcePdfRead = "NewsSourcePdfRead";

    public const string NewsSourcePdfAudit = "NewsSourcePdfAudit";

    public const string SocialRead = "SocialRead";

    public const string SocialDraft = "SocialDraft";

    public const string SocialApprove = "SocialApprove";

    public const string SocialPublish = "SocialPublish";

    public const string SocialAccountsManage = "SocialAccountsManage";

    public const string SocialViewPublished = "SocialViewPublished";

    public const string DailyBriefView = "DailyBriefView";

    public const string DailyBriefEdit = "DailyBriefEdit";

    public const string DailyBriefApprove = "DailyBriefApprove";

    public const string DailyBriefSend = "DailyBriefSend";

    public const string DailyBriefSettings = "DailyBriefSettings";

    public const string OperatorDashboard = "OperatorDashboard";

    public const string OperatorDownloadMonitor = "OperatorDownloadMonitor";

    public const string OperatorLatestPdf = "OperatorLatestPdf";

    public const string OperatorFailureDetails = "OperatorFailureDetails";

    public const string OperatorNotes = "OperatorNotes";

    public const string OperatorInformAdmin = "OperatorInformAdmin";

    public const string AdminInterventionQueue = "AdminInterventionQueue";

    public const string AdminInterventionManage = "AdminInterventionManage";

    public const string SourceRecoveryView = "SourceRecoveryView";

    public const string SourceRecoveryApply = "SourceRecoveryApply";

    public const string SourceRecoveryAdmin = "SourceRecoveryAdmin";
}
