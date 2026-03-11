using Baileys.Defaults;
using Baileys.Types;
using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>Tests for <see cref="AuthUtils"/>.</summary>
public class AuthUtilsTests
{
    // ──────────────────────────────────────────────────────────
    //  InitAuthCreds
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void InitAuthCreds_ReturnsNonNullCreds()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.NotNull(creds);
    }

    [Fact]
    public void InitAuthCreds_NoiseKey_Has32BytePublicAndPrivate()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.Equal(32, creds.NoiseKey.Public.Length);
        Assert.Equal(32, creds.NoiseKey.Private.Length);
    }

    [Fact]
    public void InitAuthCreds_SignedIdentityKey_Has32ByteKeys()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.Equal(32, creds.SignedIdentityKey.Public.Length);
        Assert.Equal(32, creds.SignedIdentityKey.Private.Length);
    }

    [Fact]
    public void InitAuthCreds_AdvSecretKey_IsBase64_32Bytes()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.False(string.IsNullOrEmpty(creds.AdvSecretKey));
        var decoded = Convert.FromBase64String(creds.AdvSecretKey);
        Assert.Equal(32, decoded.Length);
    }

    [Fact]
    public void InitAuthCreds_RegistrationId_InRange()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.InRange(creds.RegistrationId, 0, 16383);
    }

    [Fact]
    public void InitAuthCreds_Defaults()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.False(creds.Registered);
        Assert.Null(creds.PairingCode);
        Assert.Equal(1, creds.NextPreKeyId);
        Assert.Equal(1, creds.FirstUnuploadedPreKeyId);
        Assert.Equal(0, creds.AccountSyncCounter);
        Assert.False(creds.AccountSettings.UnarchiveChats);
    }

    [Fact]
    public void InitAuthCreds_TwoCalls_DifferentKeys()
    {
        var a = AuthUtils.InitAuthCreds();
        var b = AuthUtils.InitAuthCreds();
        Assert.NotEqual(a.NoiseKey.Public, b.NoiseKey.Public);
        Assert.NotEqual(a.AdvSecretKey, b.AdvSecretKey);
    }

    [Fact]
    public void InitAuthCreds_SignedPreKey_HasSignature()
    {
        var creds = AuthUtils.InitAuthCreds();
        Assert.NotNull(creds.SignedPreKey);
        Assert.NotEmpty(creds.SignedPreKey.Signature);
        Assert.Equal(1, creds.SignedPreKey.KeyId);
    }

    // ──────────────────────────────────────────────────────────
    //  GenerateKeyPair
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateKeyPair_Returns32ByteKeys()
    {
        var kp = AuthUtils.GenerateKeyPair();
        Assert.Equal(32, kp.Public.Length);
        Assert.Equal(32, kp.Private.Length);
    }

    [Fact]
    public void GenerateKeyPair_UniqueEachCall()
    {
        var a = AuthUtils.GenerateKeyPair();
        var b = AuthUtils.GenerateKeyPair();
        Assert.NotEqual(a.Public, b.Public);
        Assert.NotEqual(a.Private, b.Private);
    }

    [Fact]
    public void GenerateKeyPair_PrivateKey_Clamped()
    {
        // Per RFC 7748 §5: private[0] & 248, private[31] & 127, private[31] | 64
        for (int i = 0; i < 10; i++)
        {
            var kp = AuthUtils.GenerateKeyPair();
            Assert.Equal(0, kp.Private[0]  & 0x07);  // low 3 bits of byte 0 = 0
            Assert.Equal(0, kp.Private[31] & 0x80);  // high bit of byte 31 = 0
            Assert.Equal(64, kp.Private[31] & 0x40); // bit 6 of byte 31 = 1
        }
    }
}

/// <summary>Tests for <see cref="BaileysDefaults"/>.</summary>
public class BaileysDefaultsTests
{
    [Fact]
    public void BaileysVersion_HasThreeComponents()
    {
        Assert.Equal(3, BaileysDefaults.BaileysVersion.Length);
        Assert.Equal(2, BaileysDefaults.BaileysVersion[0]);
    }

    [Fact]
    public void NoiseWaHeader_StartsWithWA()
    {
        Assert.Equal((byte)'W', BaileysDefaults.NoiseWaHeader[0]);
        Assert.Equal((byte)'A', BaileysDefaults.NoiseWaHeader[1]);
    }

    [Fact]
    public void KeyBundleType_IsCorrect()
    {
        Assert.Equal(new byte[] { 5 }, BaileysDefaults.KeyBundleType);
    }

    [Fact]
    public void WaDefaultEphemeral_SevenDays()
    {
        Assert.Equal(7 * 24 * 60 * 60, BaileysDefaults.WaDefaultEphemeral);
    }

    [Fact]
    public void UnauthorizedCodes_Contains401And403()
    {
        Assert.Contains(401, BaileysDefaults.UnauthorizedCodes);
        Assert.Contains(403, BaileysDefaults.UnauthorizedCodes);
        Assert.Contains(419, BaileysDefaults.UnauthorizedCodes);
    }

    [Fact]
    public void WaWebSocketUrl_IsWss()
    {
        Assert.StartsWith("wss://", BaileysDefaults.WaWebSocketUrl);
    }
}

/// <summary>Tests for <see cref="Browsers"/>.</summary>
public class BrowserDescriptionsTests
{
    [Fact]
    public void MacOs_Returns3Tuple()
    {
        var b = Browsers.MacOs();
        Assert.Equal(3, b.Length);
        Assert.Equal("Mac OS", b[0]);
        Assert.Equal("Chrome", b[1]);
    }

    [Fact]
    public void Ubuntu_Returns3Tuple()
    {
        var b = Browsers.Ubuntu("Firefox");
        Assert.Equal("Ubuntu", b[0]);
        Assert.Equal("Firefox", b[1]);
    }

    [Fact]
    public void Windows_Returns3Tuple()
    {
        var b = Browsers.Windows();
        Assert.Equal("Windows", b[0]);
        Assert.Equal(3, b.Length);
    }

    [Fact]
    public void Baileys_Returns3Tuple()
    {
        var b = Browsers.Baileys();
        Assert.Equal("Baileys", b[0]);
    }

    [Fact]
    public void Appropriate_Returns3Tuple()
    {
        var b = Browsers.Appropriate();
        Assert.Equal(3, b.Length);
        Assert.False(string.IsNullOrEmpty(b[0]));
    }

    [Fact]
    public void MediaHkdfKeyMapping_ContainsAudio()
    {
        Assert.True(Browsers.MediaHkdfKeyMapping.ContainsKey("audio"));
        Assert.Equal("Audio", Browsers.MediaHkdfKeyMapping["audio"]);
    }

    [Fact]
    public void MediaPathMap_ContainsImage()
    {
        Assert.True(Browsers.MediaPathMap.ContainsKey("image"));
        Assert.Equal("/mms/image", Browsers.MediaPathMap["image"]);
    }
}
