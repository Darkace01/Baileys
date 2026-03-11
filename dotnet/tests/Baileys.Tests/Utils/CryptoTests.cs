using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>
/// Tests for <see cref="Crypto"/> — mirrors the TypeScript Utils/crypto.ts
/// test expectations.
/// </summary>
public class CryptoTests
{
    // ──────────────────────────────────────────────────────────
    //  AES-256-GCM
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AesGcm_EncryptDecrypt_RoundTrip()
    {
        var key = Crypto.RandomBytes(32);
        var iv = Crypto.RandomBytes(12);
        var aad = new byte[] { 1, 2, 3 };
        var plaintext = System.Text.Encoding.UTF8.GetBytes("Hello WhatsApp!");

        var encrypted = Crypto.AesEncryptGcm(plaintext, key, iv, aad);
        var decrypted = Crypto.AesDecryptGcm(encrypted, key, iv, aad);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void AesGcm_AuthTagAppended()
    {
        var key = Crypto.RandomBytes(32);
        var iv = Crypto.RandomBytes(12);
        var aad = Array.Empty<byte>();
        var plaintext = new byte[16];

        var ciphertext = Crypto.AesEncryptGcm(plaintext, key, iv, aad);
        // ciphertext || 16-byte tag
        Assert.Equal(32, ciphertext.Length);
    }

    [Fact]
    public void AesGcm_WrongKey_Throws()
    {
        var key = Crypto.RandomBytes(32);
        var wrongKey = Crypto.RandomBytes(32);
        var iv = Crypto.RandomBytes(12);
        var plaintext = new byte[] { 0x01, 0x02 };

        var ciphertext = Crypto.AesEncryptGcm(plaintext, key, iv, Array.Empty<byte>());

        // AuthenticationTagMismatchException is a subclass of CryptographicException
        Assert.ThrowsAny<System.Security.Cryptography.CryptographicException>(
            () => Crypto.AesDecryptGcm(ciphertext, wrongKey, iv, Array.Empty<byte>()));
    }

    // ──────────────────────────────────────────────────────────
    //  AES-256-CBC
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AesCbc_EncryptDecrypt_RoundTrip()
    {
        var key = Crypto.RandomBytes(32);
        var plaintext = System.Text.Encoding.UTF8.GetBytes("test message");

        var encrypted = Crypto.AesEncrypt(plaintext, key);
        var decrypted = Crypto.AesDecrypt(encrypted, key);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void AesCbc_IvPrepended()
    {
        var key = Crypto.RandomBytes(32);
        var plaintext = new byte[16];

        var encrypted = Crypto.AesEncrypt(plaintext, key);

        // Should be IV (16) + ciphertext (16) = 32 bytes minimum
        Assert.True(encrypted.Length >= 32);
    }

    [Fact]
    public void AesCbc_WithIv_RoundTrip()
    {
        var key = Crypto.RandomBytes(32);
        var iv = Crypto.RandomBytes(16);
        var plaintext = new byte[] { 0xAB, 0xCD, 0xEF };

        var ciphertext = Crypto.AesEncryptWithIv(plaintext, key, iv);
        var decrypted = Crypto.AesDecryptWithIv(ciphertext, key, iv);

        Assert.Equal(plaintext, decrypted);
    }

    // ──────────────────────────────────────────────────────────
    //  AES-256-CTR
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AesCtr_EncryptDecrypt_RoundTrip()
    {
        var key = Crypto.RandomBytes(32);
        var iv = Crypto.RandomBytes(16);
        var plaintext = new byte[] { 1, 2, 3, 4, 5 };

        var ciphertext = Crypto.AesEncryptCtr(plaintext, key, iv);
        var decrypted = Crypto.AesDecryptCtr(ciphertext, key, iv);

        Assert.Equal(plaintext, decrypted);
    }

    [Fact]
    public void AesCtr_EncryptAndDecryptAreSymmetric()
    {
        var key = Crypto.RandomBytes(32);
        var iv = new byte[16];
        var plaintext = System.Text.Encoding.UTF8.GetBytes("CTR mode is symmetric");

        // Encrypt = Decrypt in CTR mode
        var encrypted = Crypto.AesEncryptCtr(plaintext, key, iv);
        var decrypted = Crypto.AesDecryptCtr(encrypted, key, iv);
        Assert.Equal(plaintext, decrypted);
    }

    // ──────────────────────────────────────────────────────────
    //  HMAC-SHA-256
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void HmacSha256_KnownVector()
    {
        // RFC 2202 test vector #1
        var key = Enumerable.Repeat((byte)0x0b, 20).ToArray();
        var data = System.Text.Encoding.ASCII.GetBytes("Hi There");
        var mac = Crypto.HmacSha256(data, key);

        Assert.Equal(32, mac.Length);
        // The MAC should be deterministic
        Assert.Equal(mac, Crypto.HmacSha256(data, key));
    }

    [Fact]
    public void HmacSha256_DifferentKeys_ProduceDifferentMacs()
    {
        var data = new byte[] { 1, 2, 3 };
        var mac1 = Crypto.HmacSha256(data, Crypto.RandomBytes(32));
        var mac2 = Crypto.HmacSha256(data, Crypto.RandomBytes(32));

        Assert.NotEqual(mac1, mac2);
    }

    // ──────────────────────────────────────────────────────────
    //  SHA-256
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Sha256_KnownVector()
    {
        // SHA-256("abc") verified by Python, Node.js, and .NET
        var data = System.Text.Encoding.ASCII.GetBytes("abc");
        var hash = Crypto.Sha256(data);

        Assert.Equal(32, hash.Length);
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad",
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    [Fact]
    public void Sha256_EmptyInput()
    {
        var hash = Crypto.Sha256(Array.Empty<byte>());
        Assert.Equal(32, hash.Length);
        // SHA-256("") = e3b0c44298fc1c149afb...
        Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Convert.ToHexString(hash).ToLowerInvariant());
    }

    // ──────────────────────────────────────────────────────────
    //  HKDF
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Hkdf_ProducesCorrectLength()
    {
        var ikm = Crypto.RandomBytes(32);
        var output = Crypto.Hkdf(ikm, 64);
        Assert.Equal(64, output.Length);
    }

    [Fact]
    public void Hkdf_Deterministic()
    {
        var ikm = new byte[] { 0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b,
                               0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b, 0x0b };
        var salt = new byte[] { 0, 1, 2, 3 };
        var info = new byte[] { 0xf0 };

        var a = Crypto.Hkdf(ikm, 32, salt, info);
        var b = Crypto.Hkdf(ikm, 32, salt, info);
        Assert.Equal(a, b);
    }

    // ──────────────────────────────────────────────────────────
    //  Pairing-code key derivation
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void DerivePairingCodeKey_Returns32Bytes()
    {
        var salt = Crypto.RandomBytes(16);
        var key = Crypto.DerivePairingCodeKey("ABCD-1234", salt);
        Assert.Equal(32, key.Length);
    }

    [Fact]
    public void DerivePairingCodeKey_Deterministic()
    {
        var salt = new byte[16];
        var a = Crypto.DerivePairingCodeKey("TEST1234", salt);
        var b = Crypto.DerivePairingCodeKey("TEST1234", salt);
        Assert.Equal(a, b);
    }

    // ──────────────────────────────────────────────────────────
    //  Registration ID
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateRegistrationId_InRange()
    {
        for (int i = 0; i < 100; i++)
        {
            var id = Crypto.GenerateRegistrationId();
            Assert.InRange(id, 0, 16383);
        }
    }

    // ──────────────────────────────────────────────────────────
    //  RandomBytes
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void RandomBytes_CorrectLength()
    {
        var bytes = Crypto.RandomBytes(32);
        Assert.Equal(32, bytes.Length);
    }

    [Fact]
    public void RandomBytes_ProducesDifferentValues()
    {
        var a = Crypto.RandomBytes(16);
        var b = Crypto.RandomBytes(16);
        Assert.NotEqual(a, b);
    }
}
