namespace MIP.Aws.Application.Abstractions.Portal;

/// <summary>
/// Reuses a single Dar Al Khaleej PressReader subscriber session across editions
/// (e.g. UAE - Al Khaleej and UAE - Al Khaleej Economy share the same portal and credentials).
/// </summary>
public interface IDarAlKhaleejPressReaderSessionStore
{
    bool TryGetStorageState(string username, out string storageStateJson);

    void SaveStorageState(string username, string storageStateJson);

    void Clear(string username);
}
