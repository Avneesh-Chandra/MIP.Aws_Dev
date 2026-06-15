using MIP.Aws.Application.Abstractions.Reporting;

namespace MIP.Aws.Infrastructure.Reporting;

/// <summary>Low-level provider transport used by <see cref="ReportEmailDispatcher"/>.</summary>
public interface IReportEmailTransport
{
    Task<ReportEmailSendResult> SendAsync(ReportEmailMessage message, CancellationToken cancellationToken);
}
