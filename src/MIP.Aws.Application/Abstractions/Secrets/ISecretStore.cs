namespace MIP.Aws.Application.Abstractions.Secrets;

public interface ISecretStore
{
    Task<string?> GetSecretAsync(string key, CancellationToken cancellationToken = default);
    Task SetSecretAsync(string key, string value, CancellationToken cancellationToken = default);
}
