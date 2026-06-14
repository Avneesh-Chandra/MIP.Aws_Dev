using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>
/// Static email recipient for a <see cref="ReportSchedule"/>.
/// </summary>
public sealed class ReportScheduleRecipient : AuditableEntity
{
    public Guid ReportScheduleId { get; set; }

    public ReportSchedule ReportSchedule { get; set; } = null!;

    public string Email { get; set; } = string.Empty;

    public string? DisplayName { get; set; }
}
