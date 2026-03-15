using System.Text.Json;
using Baileys.Extensions;
using Baileys.Options;
using Baileys.Session;
using Baileys.Types;
using Baileys.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace Baileys.Tests.Session;

// ─────────────────────────────────────────────────────────────────────────────
//  InMemorySignalKeyStore
// ─────────────────────────────────────────────────────────────────────────────

public sealed class InMemorySignalKeyStoreTests
{
    [Fact]
    public async Task GetAsync_ReturnsNull_ForMissingKey()
    {
        var store = new InMemorySignalKeyStore();

        var result = await store.GetAsync(SignalDataTypes.PreKey, ["1"]);

        Assert.Null(result["1"]);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredValue()
    {
        var store = new InMemorySignalKeyStore();
        var value = new byte[] { 1, 2, 3 };

        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["abc"] = value });
        var result = await store.GetAsync(SignalDataTypes.Session, ["abc"]);

        Assert.Equal(value, result["abc"]);
    }

    [Fact]
    public async Task SetAsync_NullValue_RemovesEntry()
    {
        var store = new InMemorySignalKeyStore();
        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["x"] = [0x01] });
        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["x"] = null });

        var result = await store.GetAsync(SignalDataTypes.Session, ["x"]);

        Assert.Null(result["x"]);
    }

    [Fact]
    public async Task GetAsync_ReturnsOnlyRequestedIds()
    {
        var store = new InMemorySignalKeyStore();
        await store.SetAsync(SignalDataTypes.PreKey, new Dictionary<string, byte[]?>
        {
            ["1"] = [0x01],
            ["2"] = [0x02],
            ["3"] = [0x03]
        });

        var result = await store.GetAsync(SignalDataTypes.PreKey, ["1", "3"]);

        Assert.Equal(2, result.Count);
        Assert.NotNull(result["1"]);
        Assert.NotNull(result["3"]);
        Assert.False(result.ContainsKey("2"));
    }

    [Fact]
    public async Task ClearAsync_RemovesAllKeys()
    {
        var store = new InMemorySignalKeyStore();
        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["a"] = [1], ["b"] = [2] });
        await store.SetAsync(SignalDataTypes.PreKey, new Dictionary<string, byte[]?> { ["1"] = [1] });

        await store.ClearAsync();

        var sessions = await store.GetAsync(SignalDataTypes.Session, ["a", "b"]);
        var preKeys  = await store.GetAsync(SignalDataTypes.PreKey,   ["1"]);
        Assert.Null(sessions["a"]);
        Assert.Null(sessions["b"]);
        Assert.Null(preKeys["1"]);
    }

    [Fact]
    public async Task SetAsync_DifferentTypes_DoNotInterfere()
    {
        var store = new InMemorySignalKeyStore();
        await store.SetAsync(SignalDataTypes.PreKey,  new Dictionary<string, byte[]?> { ["1"] = [0xAA] });
        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["1"] = [0xBB] });

        var preKey  = await store.GetAsync(SignalDataTypes.PreKey,  ["1"]);
        var session = await store.GetAsync(SignalDataTypes.Session, ["1"]);

        Assert.Equal([0xAA], preKey["1"]);
        Assert.Equal([0xBB], session["1"]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  DirectorySignalKeyStore
// ─────────────────────────────────────────────────────────────────────────────

public sealed class DirectorySignalKeyStoreTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"baileys_key_test_{Guid.NewGuid():N}");

    public DirectorySignalKeyStoreTests() =>
        System.IO.Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Constructor_CreatesDirectoryWhenAbsent()
    {
        var path = Path.Combine(_tempDir, "sub_" + Guid.NewGuid().ToString("N"));
        _ = new DirectorySignalKeyStore(path);
        Assert.True(System.IO.Directory.Exists(path));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenDirectoryIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new DirectorySignalKeyStore(""));
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_RoundTrip()
    {
        var store = new DirectorySignalKeyStore(_tempDir);
        var data  = new byte[] { 10, 20, 30 };

        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["jid1"] = data });
        var result = await store.GetAsync(SignalDataTypes.Session, ["jid1"]);

        Assert.Equal(data, result["jid1"]);
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_ForAbsentKey()
    {
        var store = new DirectorySignalKeyStore(_tempDir);

        var result = await store.GetAsync(SignalDataTypes.PreKey, ["999"]);

        Assert.Null(result["999"]);
    }

    [Fact]
    public async Task SetAsync_NullValue_DeletesFile()
    {
        var store = new DirectorySignalKeyStore(_tempDir);
        await store.SetAsync(SignalDataTypes.PreKey, new Dictionary<string, byte[]?> { ["42"] = [0xAB] });

        var filePath = store.GetFilePath(SignalDataTypes.PreKey, "42");
        Assert.True(File.Exists(filePath));

        await store.SetAsync(SignalDataTypes.PreKey, new Dictionary<string, byte[]?> { ["42"] = null });
        Assert.False(File.Exists(filePath));
    }

    [Fact]
    public async Task ClearAsync_DeletesAllFiles()
    {
        var store = new DirectorySignalKeyStore(_tempDir);
        await store.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["a"] = [1], ["b"] = [2] });

        await store.ClearAsync();

        var result = await store.GetAsync(SignalDataTypes.Session, ["a", "b"]);
        Assert.Null(result["a"]);
        Assert.Null(result["b"]);
    }

    [Fact]
    public void GetFilePath_SanitisesSlashAndColon()
    {
        var store = new DirectorySignalKeyStore(_tempDir);

        var path = store.GetFilePath("session", "group/123:456");

        Assert.Contains("group__123-456", path);
        Assert.DoesNotContain("/", Path.GetFileName(path));
        Assert.DoesNotContain(":", Path.GetFileName(path));
    }

    [Fact]
    public async Task PersistsAcrossInstances()
    {
        var data = new byte[] { 7, 8, 9 };
        await new DirectorySignalKeyStore(_tempDir).SetAsync(
            SignalDataTypes.SenderKey,
            new Dictionary<string, byte[]?> { ["key1"] = data });

        // New instance pointing to the same directory
        var result = await new DirectorySignalKeyStore(_tempDir)
            .GetAsync(SignalDataTypes.SenderKey, ["key1"]);

        Assert.Equal(data, result["key1"]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  SignalKeyStoreExtensions (typed accessors)
// ─────────────────────────────────────────────────────────────────────────────

public sealed class SignalKeyStoreExtensionsTests
{
    private static InMemorySignalKeyStore CreateStore() => new();

    [Fact]
    public async Task PreKeys_RoundTrip()
    {
        var store = CreateStore();
        var kp = new KeyPair([1, 2, 3], [4, 5, 6]);

        await store.SetPreKeysAsync(new Dictionary<string, KeyPair?> { ["1"] = kp });
        var result = await store.GetPreKeysAsync(["1"]);

        Assert.NotNull(result["1"]);
        Assert.Equal(kp.Public,  result["1"]!.Public);
        Assert.Equal(kp.Private, result["1"]!.Private);
    }

    [Fact]
    public async Task PreKeys_NullValue_RemovesEntry()
    {
        var store = CreateStore();
        await store.SetPreKeysAsync(new Dictionary<string, KeyPair?> { ["1"] = new KeyPair([1], [2]) });
        await store.SetPreKeysAsync(new Dictionary<string, KeyPair?> { ["1"] = null });

        var result = await store.GetPreKeysAsync(["1"]);
        Assert.Null(result["1"]);
    }

    [Fact]
    public async Task SenderKeyMemory_RoundTrip()
    {
        var store = CreateStore();
        var memory = new Dictionary<string, bool> { ["jid1@s.whatsapp.net"] = true, ["jid2@s.whatsapp.net"] = false };

        await store.SetSenderKeyMemoriesAsync(new Dictionary<string, Dictionary<string, bool>?> { ["group1"] = memory });
        var result = await store.GetSenderKeyMemoriesAsync(["group1"]);

        Assert.NotNull(result["group1"]);
        Assert.True(result["group1"]!["jid1@s.whatsapp.net"]);
        Assert.False(result["group1"]!["jid2@s.whatsapp.net"]);
    }

    [Fact]
    public async Task AppStateSyncVersions_RoundTrip()
    {
        var store = CreateStore();
        var state = new LtHashState
        {
            Version = 7,
            Hash = [0xDE, 0xAD, 0xBE, 0xEF],
            IndexValueMap = { ["key1"] = [0x01, 0x02] }
        };

        await store.SetAppStateSyncVersionsAsync(new Dictionary<string, LtHashState?> { ["critical_block"] = state });
        var result = await store.GetAppStateSyncVersionsAsync(["critical_block"]);

        Assert.NotNull(result["critical_block"]);
        Assert.Equal(7, result["critical_block"]!.Version);
        Assert.Equal([0xDE, 0xAD, 0xBE, 0xEF], result["critical_block"]!.Hash);
    }

    [Fact]
    public async Task LidMappings_RoundTrip()
    {
        var store = CreateStore();

        await store.SetLidMappingsAsync(new Dictionary<string, string?> { ["lid123"] = "15551234567" });
        var result = await store.GetLidMappingsAsync(["lid123"]);

        Assert.Equal("15551234567", result["lid123"]);
    }

    [Fact]
    public async Task DeviceLists_RoundTrip()
    {
        var store = CreateStore();
        IReadOnlyList<string> devices = ["0", "2", "3"];

        await store.SetDeviceListsAsync(new Dictionary<string, IReadOnlyList<string>?> { ["user@s.whatsapp.net"] = devices });
        var result = await store.GetDeviceListsAsync(["user@s.whatsapp.net"]);

        Assert.NotNull(result["user@s.whatsapp.net"]);
        Assert.Equal(devices, result["user@s.whatsapp.net"]);
    }

    [Fact]
    public async Task TcTokens_RoundTrip()
    {
        var store = CreateStore();
        var token = new TcToken { Token = [0xCA, 0xFE], Timestamp = "2024-01-01" };

        await store.SetTcTokensAsync(new Dictionary<string, TcToken?> { ["device1"] = token });
        var result = await store.GetTcTokensAsync(["device1"]);

        Assert.NotNull(result["device1"]);
        Assert.Equal(token.Token, result["device1"]!.Token);
        Assert.Equal(token.Timestamp, result["device1"]!.Timestamp);
    }

    [Fact]
    public async Task Sessions_PassThrough()
    {
        var store = CreateStore();
        var data = new byte[] { 0x11, 0x22, 0x33 };

        await store.SetSessionsAsync(new Dictionary<string, byte[]?> { ["jid@s.whatsapp.net:0"] = data });
        var result = await store.GetSessionsAsync(["jid@s.whatsapp.net:0"]);

        Assert.Equal(data, result["jid@s.whatsapp.net:0"]);
    }

    [Fact]
    public async Task IdentityKeys_PassThrough()
    {
        var store = CreateStore();
        var key = new byte[] { 0xFF, 0xFE };

        await store.SetIdentityKeysAsync(new Dictionary<string, byte[]?> { ["jid"] = key });
        var result = await store.GetIdentityKeysAsync(["jid"]);

        Assert.Equal(key, result["jid"]);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  DirectoryAuthStateProvider
// ─────────────────────────────────────────────────────────────────────────────

public sealed class DirectoryAuthStateProviderTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"baileys_dir_test_{Guid.NewGuid():N}");

    public DirectoryAuthStateProviderTests() =>
        System.IO.Directory.CreateDirectory(_tempDir);

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenDirectoryIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new DirectoryAuthStateProvider(""));
    }

    [Fact]
    public void Constructor_ExposesSameDirectory()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);
        Assert.Equal(_tempDir, provider.Directory);
    }

    [Fact]
    public async Task LoadCredsAsync_ReturnsFreshCreds_WhenDirectoryIsEmpty()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);

        var creds = await provider.LoadCredsAsync();

        Assert.NotNull(creds);
        Assert.False(creds.Registered);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRegistrationId()
    {
        var provider  = new DirectoryAuthStateProvider(_tempDir);
        var original  = AuthUtils.InitAuthCreds();
        original.RegistrationId = 99_999;

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(99_999, loaded.RegistrationId);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesNoiseKey()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);
        var original = AuthUtils.InitAuthCreds();

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(original.NoiseKey.Public, loaded.NoiseKey.Public);
    }

    [Fact]
    public async Task ClearAsync_RemovesCredsFileAndFreshCredsOnReload()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);
        var creds    = AuthUtils.InitAuthCreds();
        creds.RegistrationId = 42;
        await provider.SaveCredsAsync(creds);

        await provider.ClearAsync();

        var fresh = await provider.LoadCredsAsync();
        Assert.NotEqual(42, fresh.RegistrationId);
    }

    [Fact]
    public async Task LoadAuthStateAsync_ReturnsBundledCredsAndKeys()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);

        var state = await provider.LoadAuthStateAsync();

        Assert.NotNull(state.Creds);
        Assert.NotNull(state.Keys);
        Assert.Same(provider.Keys, state.Keys);
    }

    [Fact]
    public async Task Keys_CanStoreAndRetrieveSignalKeys()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);
        var state    = await provider.LoadAuthStateAsync();
        var data     = new byte[] { 0xAA, 0xBB, 0xCC };

        await state.Keys.SetAsync(SignalDataTypes.Session, new Dictionary<string, byte[]?> { ["jid@test:0"] = data });
        var result = await state.Keys.GetAsync(SignalDataTypes.Session, ["jid@test:0"]);

        Assert.Equal(data, result["jid@test:0"]);
    }

    [Fact]
    public async Task SignalKeys_PersistAcrossProviderInstances()
    {
        var data = new byte[] { 1, 2, 3 };
        var p1   = new DirectoryAuthStateProvider(_tempDir);
        await p1.Keys.SetAsync(SignalDataTypes.PreKey, new Dictionary<string, byte[]?> { ["1"] = data });

        var p2     = new DirectoryAuthStateProvider(_tempDir);
        var result = await p2.Keys.GetAsync(SignalDataTypes.PreKey, ["1"]);

        Assert.Equal(data, result["1"]);
    }

    [Fact]
    public async Task SaveCredsAsync_CreatesCredsJson()
    {
        var provider = new DirectoryAuthStateProvider(_tempDir);
        await provider.SaveCredsAsync(AuthUtils.InitAuthCreds());

        var credsFile = Path.Combine(_tempDir, "creds.json");
        Assert.True(File.Exists(credsFile));
        var json = await File.ReadAllTextAsync(credsFile);
        Assert.Contains("registrationId", json);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  AuthStateExtensions
// ─────────────────────────────────────────────────────────────────────────────

public sealed class AuthStateExtensionsTests
{
    [Fact]
    public async Task LoadAuthStateAsync_ReturnsCredsFromProvider()
    {
        var provider = new InMemoryAuthStateProvider();

        var state = await provider.LoadAuthStateAsync();

        Assert.NotNull(state.Creds);
        Assert.False(state.Creds.Registered);
    }

    [Fact]
    public async Task LoadAuthStateAsync_CreatesInMemoryKeyStore_WhenNotProvided()
    {
        var provider = new InMemoryAuthStateProvider();

        var state = await provider.LoadAuthStateAsync();

        Assert.NotNull(state.Keys);
        Assert.IsType<InMemorySignalKeyStore>(state.Keys);
    }

    [Fact]
    public async Task LoadAuthStateAsync_UsesSuppliedKeyStore()
    {
        var provider  = new InMemoryAuthStateProvider();
        var keyStore  = new InMemorySignalKeyStore();

        var state = await provider.LoadAuthStateAsync(keys: keyStore);

        Assert.Same(keyStore, state.Keys);
    }

    [Fact]
    public async Task LoadAuthStateAsync_WithDirectoryProvider_UsesBundledKeyStore()
    {
        var tempDir  = Path.Combine(Path.GetTempPath(), $"auth_ext_test_{Guid.NewGuid():N}");
        try
        {
            var provider = new DirectoryAuthStateProvider(tempDir);

            var state = await provider.LoadAuthStateAsync();

            Assert.NotNull(state.Keys);
            Assert.IsType<DirectorySignalKeyStore>(state.Keys);
            Assert.Same(provider.Keys, state.Keys);
        }
        finally
        {
            if (System.IO.Directory.Exists(tempDir))
                System.IO.Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public async Task LoadAuthStateAsync_FileProvider_WithCustomKeyStore()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"auth_ext_{Guid.NewGuid():N}.json");
        try
        {
            var provider = new FileAuthStateProvider(tempFile);
            var keys     = new InMemorySignalKeyStore();

            var state = await provider.LoadAuthStateAsync(keys: keys);

            Assert.NotNull(state.Creds);
            Assert.Same(keys, state.Keys);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  ServiceCollectionExtensions — AddBaileysWithDirectoryStorage
// ─────────────────────────────────────────────────────────────────────────────

public sealed class ServiceCollectionExtensionsDirectoryTests : IDisposable
{
    private readonly string _tempDir =
        Path.Combine(Path.GetTempPath(), $"di_dir_test_{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (System.IO.Directory.Exists(_tempDir))
            System.IO.Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void AddBaileysWithDirectoryStorage_RegistersDirectoryProvider()
    {
        var services = new ServiceCollection();
        services.AddBaileysWithDirectoryStorage(_tempDir);
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IAuthStateProvider>();
        Assert.IsType<DirectoryAuthStateProvider>(provider);
    }

    [Fact]
    public void AddBaileysWithDirectoryStorage_SetsDirectory()
    {
        var services = new ServiceCollection();
        services.AddBaileysWithDirectoryStorage(_tempDir);
        var sp = services.BuildServiceProvider();

        var provider = (DirectoryAuthStateProvider)sp.GetRequiredService<IAuthStateProvider>();
        Assert.Equal(_tempDir, provider.Directory);
    }

    [Fact]
    public void AddBaileysWithDirectoryStorage_SetsPhoneNumber()
    {
        var services = new ServiceCollection();
        services.AddBaileysWithDirectoryStorage(_tempDir, o => o.PhoneNumber = "15551234567");
        var sp = services.BuildServiceProvider();

        var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<BaileysOptions>>().Value;
        Assert.Equal("15551234567", opts.PhoneNumber);
    }

    [Fact]
    public void AddBaileysWithDirectoryStorage_IsSingleton()
    {
        var services = new ServiceCollection();
        services.AddBaileysWithDirectoryStorage(_tempDir);
        var sp = services.BuildServiceProvider();

        var p1 = sp.GetRequiredService<IAuthStateProvider>();
        var p2 = sp.GetRequiredService<IAuthStateProvider>();
        Assert.Same(p1, p2);
    }
}

// ─────────────────────────────────────────────────────────────────────────────
//  AuthenticationState type
// ─────────────────────────────────────────────────────────────────────────────

public sealed class AuthenticationStateTests
{
    [Fact]
    public void CanConstruct_WithCredsAndKeys()
    {
        var creds = AuthUtils.InitAuthCreds();
        var keys  = new InMemorySignalKeyStore();

        var state = new AuthenticationState { Creds = creds, Keys = keys };

        Assert.Same(creds, state.Creds);
        Assert.Same(keys,  state.Keys);
    }

    [Fact]
    public void RequiredProperties_AreEnforced()
    {
        // This test is a compile-time check only: AuthenticationState must have
        // 'required' on both Creds and Keys.  If either required keyword is
        // removed, the compiler will flag every construction site without the
        // property, so this test documents the intent.
        var state = new AuthenticationState
        {
            Creds = AuthUtils.InitAuthCreds(),
            Keys  = new InMemorySignalKeyStore()
        };

        Assert.NotNull(state.Creds);
        Assert.NotNull(state.Keys);
    }
}
