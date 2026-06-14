using MIP.Aws.Domain.Common;

namespace MIP.Aws.Domain.Entities;

public class DownloadedFile : AuditableEntity
{
    public Guid DownloadJobId { get; set; }

    public DownloadJob DownloadJob { get; set; } = null!;

    public string ContentType { get; set; } = "application/octet-stream";

    public string OriginalUrl { get; set; } = string.Empty;

    public string BlobUri { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? Sha256 { get; set; }

    public ICollection<ExtractedArticle> Articles { get; set; } = new List<ExtractedArticle>();

    public ICollection<OcrProcessingJob> OcrJobs { get; set; } = new List<OcrProcessingJob>();
}
