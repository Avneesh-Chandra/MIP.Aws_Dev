using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class Report : AuditableEntity
{
    public ReportType ReportType { get; set; }

    public ReportFormat Format { get; set; }

    public DateOnly ReportDate { get; set; }

    public string Title { get; set; } = string.Empty;

    /// <summary>Relative storage key (e.g. reports/pdf/...).</summary>
    public string BlobUri { get; set; } = string.Empty;

    public string? ApprovedBy { get; set; }

    public DateTimeOffset? ApprovedAt { get; set; }

    public ReportGenerationStatus GenerationStatus { get; set; } = ReportGenerationStatus.Completed;

    public string? FailureReason { get; set; }

    public long? SizeBytes { get; set; }

    public DateTimeOffset? GeneratedAt { get; set; }

    public string? ContentType { get; set; }

    public ICollection<EmailLog> EmailLogs { get; set; } = new List<EmailLog>();

    public ICollection<ReportDeliveryLog> DeliveryLogs { get; set; } = new List<ReportDeliveryLog>();
}
