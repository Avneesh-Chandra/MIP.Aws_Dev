using MIP.Aws.Domain.Common;
using MIP.Aws.Domain.Enums;

namespace MIP.Aws.Domain.Entities;

public class DownloadJob : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public DownloadJobStatus Status { get; set; }

    public DateTimeOffset? StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public string? CorrelationId { get; set; }

    public string? ErrorMessage { get; set; }

    public int RetryCount { get; set; }

    public int? HttpStatusCode { get; set; }

    public long? DurationMs { get; set; }

    public bool RobotsTxtAllowed { get; set; } = true;

    public DownloadJobTrigger Trigger { get; set; } = DownloadJobTrigger.Manual;

    public Guid? AutoAiRecoveryRunId { get; set; }

    public ICollection<DownloadedFile> Files { get; set; } = new List<DownloadedFile>();
}
