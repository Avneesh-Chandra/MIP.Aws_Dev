using MIP.Aws.Application.Abstractions.Portal;

namespace MIP.Aws.Infrastructure.Portal;

/// <summary>
/// In-process cache of Playwright storage state for daralkhaleej.pressreader.com.
/// Both UAE Al Khaleej editions share credentials; restoring cookies avoids a second login.
/// </summary>
public sealed class DarAlKhaleejPressReaderSessionStore : IDarAlKhaleejPressReaderSessionStore
{
    private static readonly TimeSpan SessionTtl = TimeSpan.FromHours(2);
    private readonly object _sync = new();
    private string? _username;
    private string? _storageStateJson;
    private DateTimeOffset _expiresAt;

    public bool TryGetStorageState(string username, out string storageStateJson)
    {
        storageStateJson = string.Empty;
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        lock (_sync)
        {
            if (_storageStateJson is null
                || _username is null
                || !string.Equals(_username, username.Trim(), StringComparison.OrdinalIgnoreCase)
                || DateTimeOffset.UtcNow >= _expiresAt)
            {
                return false;
            }

            storageStateJson = _storageStateJson;
            return true;
        }
    }

    public void SaveStorageState(string username, string storageStateJson)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(storageStateJson))
        {
            return;
        }

        lock (_sync)
        {
            _username = username.Trim();
            _storageStateJson = storageStateJson;
            _expiresAt = DateTimeOffset.UtcNow.Add(SessionTtl);
        }
    }

    public void Clear(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        lock (_sync)
        {
            if (string.Equals(_username, username.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                _username = null;
                _storageStateJson = null;
                _expiresAt = default;
            }
        }
    }
}
