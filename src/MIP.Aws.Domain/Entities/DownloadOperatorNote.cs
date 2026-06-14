using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

/// <summary>Operator observation recorded against a source download attempt.</summary>
public class DownloadOperatorNote : AuditableEntity
{
    public Guid NewsSourceId { get; set; }

    public NewsSource NewsSource { get; set; } = null!;

    public Guid? DownloadJobId { get; set; }

    public DownloadJob? DownloadJob { get; set; }

    public string Note { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }
}
