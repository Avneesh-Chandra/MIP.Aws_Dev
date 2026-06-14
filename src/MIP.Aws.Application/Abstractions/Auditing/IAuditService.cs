namespace MIP.Aws.Application.Abstractions.Auditing;

/// <summary>
/// Append-only audit emitter. Rows are persisted to <c>AuditLog</c> table and MUST NOT be modified
/// after insertion. Implementations are responsible for stamping correlation, user, and IP context.
/// </summary>
public interface IAuditService
{
    /// <summary>
    /// Records an administrative action (user crud, role change, configuration change, etc.).
    /// </summary>
    Task RecordAdminActionAsync(
        string action,
        string resourceType,
        string? resourceId,
        object? details = null,
        CancellationToken cancellationToken = default);

    /// <summary>Records a report download (compliance-grade trail).</summary>
    Task RecordReportDownloadAsync(
        Guid reportId,
        Guid? userId,
        string? ipAddress,
        CancellationToken cancellationToken = default);

    /// <summary>Records the outcome of an AI processing pass against an article.</summary>
    Task RecordAiProcessingAsync(
        Guid articleId,
        bool success,
        string? failureReason,
        TimeSpan duration,
        CancellationToken cancellationToken = default);

    /// <summary>Records OCR completion (article/page level depending on caller).</summary>
    Task RecordOcrProcessingAsync(
        Guid downloadedFileId,
        bool success,
        string? failureReason,
        TimeSpan duration,
        CancellationToken cancellationToken = default);
}
