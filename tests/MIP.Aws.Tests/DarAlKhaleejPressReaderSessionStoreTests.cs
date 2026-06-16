using MIP.Aws.Infrastructure.Portal;

namespace MIP.Aws.Tests;

public sealed class DarAlKhaleejPressReaderSessionStoreTests
{
    [Fact]
    public void Save_and_restore_storage_state_for_same_subscriber()
    {
        var store = new DarAlKhaleejPressReaderSessionStore();
        const string username = "jm37955283";
        const string state = """{"cookies":[{"name":"sid","value":"abc"}]}""";

        store.SaveStorageState(username, state);

        Assert.True(store.TryGetStorageState(username, out var restored));
        Assert.Equal(state, restored);
    }

    [Fact]
    public void Clear_removes_cached_session()
    {
        var store = new DarAlKhaleejPressReaderSessionStore();
        const string username = "jm37955283";
        store.SaveStorageState(username, """{"cookies":[]}""");

        store.Clear(username);

        Assert.False(store.TryGetStorageState(username, out _));
    }

    [Fact]
    public void Different_subscriber_does_not_receive_another_users_session()
    {
        var store = new DarAlKhaleejPressReaderSessionStore();
        store.SaveStorageState("user-a", """{"cookies":[]}""");

        Assert.False(store.TryGetStorageState("user-b", out _));
    }
}
