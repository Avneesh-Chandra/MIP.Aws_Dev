namespace MIP.Aws.Domain.Security;

/// <summary>
/// Canonical role names synchronized with Microsoft Entra ID app roles and local RBAC.
/// </summary>
public static class ApplicationRoles
{
    public const string SuperAdmin = "SuperAdmin";

    public const string ContentAdmin = "ContentAdmin";

    public const string Analyst = "Analyst";

    public const string Approver = "Approver";

    public const string ExecutiveViewer = "ExecutiveViewer";

    public const string ComplianceAuditor = "ComplianceAuditor";

    public const string MIPOperator = "MIPOperator";

    public static IReadOnlyList<string> All { get; } =
    [
        SuperAdmin,
        ContentAdmin,
        MIPOperator,
        Analyst,
        Approver,
        ExecutiveViewer,
        ComplianceAuditor
    ];
}
