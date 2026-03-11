using Baileys.Extensions;
using Baileys.Options;
using Baileys.Session;
using Baileys.Types;
using Baileys.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Baileys.Tests.Session;

public sealed class InMemoryAuthStateProviderTests
{
    [Fact]
    public async Task LoadCredsAsync_ReturnsInitialCreds_WhenNoCredsStored()
    {
        var provider = new InMemoryAuthStateProvider();

        var creds = await provider.LoadCredsAsync();

        Assert.NotNull(creds);
        Assert.NotNull(creds.NoiseKey);
        Assert.False(creds.Registered);
    }

    [Fact]
    public async Task LoadCredsAsync_ReturnsSameInstance_OnMultipleCalls()
    {
        var provider = new InMemoryAuthStateProvider();

        var first  = await provider.LoadCredsAsync();
        var second = await provider.LoadCredsAsync();

        // The same AuthenticationCreds object must be returned each time
        Assert.Same(first, second);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRegistrationId()
    {
        var provider = new InMemoryAuthStateProvider();
        var original = AuthUtils.InitAuthCreds();
        original.RegistrationId = 42_000;

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(42_000, loaded.RegistrationId);
    }

    [Fact]
    public async Task ClearAsync_CausesNextLoadToReturnFreshCreds()
    {
        var provider = new InMemoryAuthStateProvider();
        var original = await provider.LoadCredsAsync();
        original.RegistrationId = 99;
        await provider.SaveCredsAsync(original);

        await provider.ClearAsync();
        var fresh = await provider.LoadCredsAsync();

        Assert.NotSame(original, fresh);
    }

    [Fact]
    public async Task Constructor_WithExistingCreds_ReturnsThem()
    {
        var creds = AuthUtils.InitAuthCreds();
        creds.RegistrationId = 7777;
        var provider = new InMemoryAuthStateProvider(creds);

        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(7777, loaded.RegistrationId);
    }
}

public sealed class FileAuthStateProviderTests : IDisposable
{
    private readonly string _tempFile;

    public FileAuthStateProviderTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"baileys_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (File.Exists(_tempFile))
            File.Delete(_tempFile);
    }

    [Fact]
    public async Task LoadCredsAsync_ReturnsFreshCreds_WhenFileAbsent()
    {
        var provider = new FileAuthStateProvider(_tempFile);

        var creds = await provider.LoadCredsAsync();

        Assert.NotNull(creds);
        Assert.False(File.Exists(_tempFile)); // file not written until SaveCredsAsync
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRegistrationId()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();
        original.RegistrationId = 12_345;

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(12_345, loaded.RegistrationId);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesAdvSecretKey()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(original.AdvSecretKey, loaded.AdvSecretKey);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesNoiseKeyPublicBytes()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(original.NoiseKey.Public, loaded.NoiseKey.Public);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesSignedPreKeyId()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(original.SignedPreKey.KeyId, loaded.SignedPreKey.KeyId);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesAccountSettings()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();
        original.AccountSettings.UnarchiveChats = true;
        original.AccountSettings.DefaultEphemeralExpiration = 86_400;

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.True(loaded.AccountSettings.UnarchiveChats);
        Assert.Equal(86_400, loaded.AccountSettings.DefaultEphemeralExpiration);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesRoutingInfo()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();
        original.RoutingInfo = [0x01, 0x02, 0x03];

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Equal(original.RoutingInfo, loaded.RoutingInfo);
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_NullRoutingInfo_RemainsNull()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();
        original.RoutingInfo = null;

        await provider.SaveCredsAsync(original);
        var loaded = await provider.LoadCredsAsync();

        Assert.Null(loaded.RoutingInfo);
    }

    [Fact]
    public async Task ClearAsync_DeletesFileAndReturnsNewCredsOnNextLoad()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var original = AuthUtils.InitAuthCreds();
        original.RegistrationId = 55;

        await provider.SaveCredsAsync(original);
        Assert.True(File.Exists(_tempFile));

        await provider.ClearAsync();
        Assert.False(File.Exists(_tempFile));

        var fresh = await provider.LoadCredsAsync();
        Assert.NotEqual(55, fresh.RegistrationId);
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenFilePathIsEmpty()
    {
        Assert.Throws<ArgumentException>(() => new FileAuthStateProvider(""));
    }

    [Fact]
    public void Constructor_ThrowsArgumentException_WhenFilePathIsWhitespace()
    {
        Assert.Throws<ArgumentException>(() => new FileAuthStateProvider("   "));
    }

    [Fact]
    public async Task SaveCredsAsync_CreatesValidJsonFile()
    {
        var provider = new FileAuthStateProvider(_tempFile);
        var creds = AuthUtils.InitAuthCreds();

        await provider.SaveCredsAsync(creds);

        Assert.True(File.Exists(_tempFile));
        var json = await File.ReadAllTextAsync(_tempFile);
        Assert.StartsWith("{", json.TrimStart());
        Assert.Contains("\"registrationId\"", json);
    }
}

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddBaileys_RegistersInMemoryProvider()
    {
        var services = new ServiceCollection();
        services.AddBaileys();
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IAuthStateProvider>();
        Assert.IsType<InMemoryAuthStateProvider>(provider);
    }

    [Fact]
    public void AddBaileys_WithConfigure_SetsPhoneNumber()
    {
        var services = new ServiceCollection();
        services.AddBaileys(o => o.PhoneNumber = "15551234567");
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<BaileysOptions>>().Value;
        Assert.Equal("15551234567", options.PhoneNumber);
    }

    [Fact]
    public void AddBaileys_WithoutConfigure_UsesDefaults()
    {
        var services = new ServiceCollection();
        services.AddBaileys();
        var sp = services.BuildServiceProvider();

        var options = sp.GetRequiredService<IOptions<BaileysOptions>>().Value;
        Assert.Equal(string.Empty, options.PhoneNumber);
        Assert.Equal(31, options.InitialPreKeyCount);
        Assert.Equal(3_000, options.RetryRequestDelayMs);
    }

    [Fact]
    public void AddBaileysWithFileStorage_RegistersFileProvider()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"di_test_{Guid.NewGuid():N}.json");
        try
        {
            var services = new ServiceCollection();
            services.AddBaileysWithFileStorage(tmpFile);
            var sp = services.BuildServiceProvider();

            var provider = sp.GetRequiredService<IAuthStateProvider>();
            Assert.IsType<FileAuthStateProvider>(provider);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public void AddBaileysWithFileStorage_SetsFilePath()
    {
        var tmpFile = Path.Combine(Path.GetTempPath(), $"di_path_test_{Guid.NewGuid():N}.json");
        try
        {
            var services = new ServiceCollection();
            services.AddBaileysWithFileStorage(tmpFile, o => o.PhoneNumber = "9876");
            var sp = services.BuildServiceProvider();

            var provider = (FileAuthStateProvider)sp.GetRequiredService<IAuthStateProvider>();
            Assert.Equal(tmpFile, provider.FilePath);
        }
        finally
        {
            if (File.Exists(tmpFile)) File.Delete(tmpFile);
        }
    }

    [Fact]
    public void AddBaileysWithProvider_RegistersCustomProvider()
    {
        var services = new ServiceCollection();
        services.AddBaileysWithProvider<CustomTestAuthStateProvider>();
        var sp = services.BuildServiceProvider();

        var provider = sp.GetRequiredService<IAuthStateProvider>();
        Assert.IsType<CustomTestAuthStateProvider>(provider);
    }

    [Fact]
    public void AddBaileys_IsSingleton_ReturnsSameInstance()
    {
        var services = new ServiceCollection();
        services.AddBaileys();
        var sp = services.BuildServiceProvider();

        var p1 = sp.GetRequiredService<IAuthStateProvider>();
        var p2 = sp.GetRequiredService<IAuthStateProvider>();
        Assert.Same(p1, p2);
    }

    private sealed class CustomTestAuthStateProvider : IAuthStateProvider
    {
        public Task<AuthenticationCreds> LoadCredsAsync(CancellationToken ct = default)
            => Task.FromResult(AuthUtils.InitAuthCreds());
        public Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken ct = default)
            => Task.CompletedTask;
        public Task ClearAsync(CancellationToken ct = default)
            => Task.CompletedTask;
    }
}

public sealed class BaileysOptionsTests
{
    [Fact]
    public void Defaults_AreSetCorrectly()
    {
        var options = new BaileysOptions();

        Assert.Equal(string.Empty, options.PhoneNumber);
        Assert.Null(options.JidServer);
        Assert.Equal(31, options.InitialPreKeyCount);
        Assert.Equal(3_000, options.RetryRequestDelayMs);
        Assert.False(options.UnarchiveChats);
    }

    [Fact]
    public void SectionName_IsCorrect()
    {
        Assert.Equal("Baileys", BaileysOptions.SectionName);
    }

    [Fact]
    public void CanSetAllProperties()
    {
        var options = new BaileysOptions
        {
            PhoneNumber = "447911123456",
            JidServer = "s.whatsapp.net",
            InitialPreKeyCount = 50,
            RetryRequestDelayMs = 5_000,
            UnarchiveChats = true
        };

        Assert.Equal("447911123456", options.PhoneNumber);
        Assert.Equal("s.whatsapp.net", options.JidServer);
        Assert.Equal(50, options.InitialPreKeyCount);
        Assert.Equal(5_000, options.RetryRequestDelayMs);
        Assert.True(options.UnarchiveChats);
    }
}
