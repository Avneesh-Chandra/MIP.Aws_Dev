using MIP.Aws.Application.Features.Reports;

namespace MIP.Aws.Application.Abstractions.Reporting;

public interface IMailConfigStatusService
{
    Task<MailConfigStatusDto> GetStatusAsync(CancellationToken cancellationToken);
}
