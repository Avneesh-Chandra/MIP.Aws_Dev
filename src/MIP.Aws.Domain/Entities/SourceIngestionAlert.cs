using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Operational alert when automated ingestion cannot proceed (CAPTCHA, MFA, broken selectors, etc.).
/// </summary>
public class SourceIngestionAlert : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public SourceIngestionAlertType AlertType { get; set; }

    public string Message { get; set; } = string.Empty;

    public bool IsResolved { get; set; }

    public DateTimeOffset? ResolvedAt { get; set; }
}
