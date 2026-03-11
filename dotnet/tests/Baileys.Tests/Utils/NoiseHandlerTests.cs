using Baileys.Types;
using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>Tests for <see cref="NoiseHandler"/>.</summary>
public class NoiseHandlerTests
{
    // ──────────────────────────────────────────────────────────
    //  Construction
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void NoiseHandler_CanBeCreated_WithMinimalArgs()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nh = new NoiseHandler(kp);
        Assert.NotNull(nh);
    }

    [Fact]
    public void NoiseHandler_IntroHeader_StartsWithWA()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nh = new NoiseHandler(kp);
        var header = nh.IntroHeader.ToArray();

        Assert.Equal((byte)'W', header[0]);
        Assert.Equal((byte)'A', header[1]);
    }

    [Fact]
    public void NoiseHandler_WithRoutingInfo_LongerIntroHeader()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var routingInfo = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        var nhPlain  = new NoiseHandler(kp);
        var nhRouted = new NoiseHandler(kp, routingInfo: routingInfo);

        Assert.True(nhRouted.IntroHeader.Length > nhPlain.IntroHeader.Length);
    }

    // ──────────────────────────────────────────────────────────
    //  Handshake-phase encryption (pre-transport)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Encrypt_BeforeFinish_ProducesCiphertext()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nh = new NoiseHandler(kp);

        var plaintext = new byte[] { 0x01, 0x02, 0x03 };
        var ciphertext = nh.Encrypt(plaintext);

        // ciphertext should be different from plaintext and non-empty
        Assert.NotEmpty(ciphertext);
        Assert.NotEqual(plaintext, ciphertext);
    }

    [Fact]
    public void Encrypt_Decrypt_HandshakePhase_RoundTrip()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nhEnc = new NoiseHandler(kp);
        var nhDec = new NoiseHandler(kp);

        var original  = new byte[] { 0xAA, 0xBB, 0xCC, 0xDD };
        var encrypted = nhEnc.Encrypt(original);

        // The decrypt counterpart must share the same hash state;
        // since we use the same key pair and initial state it should round-trip
        var decrypted = nhDec.Decrypt(encrypted);
        Assert.Equal(original, decrypted);
    }

    // ──────────────────────────────────────────────────────────
    //  Transport-phase encryption (post-Finish)
    //
    //  In the Noise XX protocol the two peers derive the SAME enc/dec
    //  key pair but use them in opposite roles.  For a local loopback
    //  test we need a handler whose decKey equals our encKey.
    //  We achieve this by exposing an internal factory for tests.
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AfterFinish_Encrypt_ProducesDifferentBytes()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nh = new NoiseHandler(kp);
        nh.Finish();

        var original  = System.Text.Encoding.UTF8.GetBytes("Hello transport mode");
        var encrypted = nh.Encrypt(original);
        Assert.NotEqual(original, encrypted);
    }

    [Fact]
    public void AfterFinish_EncryptDecrypt_SameHandler_RoundTrip()
    {
        // Use the same handler in "mirror" mode: swap enc/dec keys via
        // a second handler whose encKey == our decKey.
        var kp = AuthUtils.GenerateKeyPair();
        var nhSend = new NoiseHandler(kp);
        nhSend.Finish();

        // Create a second handler from the same key pair so it derives the same keys.
        var nhRecv = new NoiseHandler(kp);
        nhRecv.Finish();

        // nhSend._encKey == nhRecv._encKey and nhSend._decKey == nhRecv._decKey
        // because both start from the same initial hash chain.
        // To get a working loopback, recv must decrypt with the SAME key used for encrypt.
        // Since both have encKey == decKey after HKDF expansion when encKey == decKey,
        // verify with a single handler self-loopback.
        var nh = new NoiseHandler(kp);
        nh.Finish();

        // Test that we can call Encrypt and then Decrypt on the same handler.
        // This validates the GCM tag round-trip even though real usage is asymmetric.
        var original  = System.Text.Encoding.UTF8.GetBytes("Hello transport mode");
        var encrypted = nh.Encrypt(original);

        // Decrypt using a fresh handler with the same derived keys
        var nhPeer = new NoiseHandler(kp);
        nhPeer.Finish();

        // Peek: both handlers have derived the same enc+dec key.
        // For loopback, use peer.decKey == enc.encKey — which is always true
        // when both derive from the same initial salt.
        var decrypted = nhPeer.DecryptWithEncKey(encrypted);
        Assert.Equal(original, decrypted);
    }

    [Fact]
    public void AfterFinish_MultipleMessages_CorrectOrder()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nhSend = new NoiseHandler(kp);
        nhSend.Finish();
        var nhRecv = new NoiseHandler(kp);
        nhRecv.Finish();

        for (int i = 0; i < 5; i++)
        {
            var msg       = new byte[] { (byte)i, (byte)(i + 1) };
            var encrypted = nhSend.Encrypt(msg);
            var decrypted = nhRecv.DecryptWithEncKey(encrypted);
            Assert.Equal(msg, decrypted);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  MixHash (authentication chain)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void MixHash_DoesNotThrow()
    {
        var kp = AuthUtils.GenerateKeyPair();
        var nh = new NoiseHandler(kp);
        var ex = Record.Exception(() => nh.MixHash(new byte[] { 1, 2, 3 }));
        Assert.Null(ex);
    }

    // ──────────────────────────────────────────────────────────
    //  Logger injection
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void NoiseHandler_AcceptsLogger()
    {
        var kp     = AuthUtils.GenerateKeyPair();
        var logger = new NullLogger();
        var nh     = new NoiseHandler(kp, logger);
        Assert.NotNull(nh);
    }
}
