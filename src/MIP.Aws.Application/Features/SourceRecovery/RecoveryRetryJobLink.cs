namespace MIP.Aws.Application.Features.SourceRecovery;

public sealed record RecoveryRetryJobLink(Guid AttemptId, bool IsAutomatic);
