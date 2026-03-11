# Cryptography API Reference

`Baileys.Utils.Crypto` provides all cryptographic primitives needed by the WhatsApp Web protocol, implemented entirely with `System.Security.Cryptography` — **no external NuGet packages required**.

---

## Namespace

```csharp
using Baileys.Utils;
```

---

## Random Data

### `RandomBytes(int count) → byte[]`

Returns cryptographically secure random bytes.

```csharp
byte[] key   = Crypto.RandomBytes(32);  // AES-256 key
byte[] nonce = Crypto.RandomBytes(12);  // GCM nonce
byte[] iv    = Crypto.RandomBytes(16);  // CBC/CTR IV
```

### `GenerateRegistrationId() → int`

Generates a random Signal-protocol registration ID (1–16383, matching the TypeScript implementation).

```csharp
int regId = Crypto.GenerateRegistrationId();  // e.g. 9473
```

---

## AES-256-GCM

AES with Galois/Counter Mode — authenticated encryption.  
Keys must be **32 bytes**. IVs (nonces) must be **12 bytes**.  
The 16-byte authentication tag is **appended** to the ciphertext.

### `AesEncryptGcm(plaintext, key, iv, additionalData) → byte[]`

Returns `ciphertext ++ 16-byte-tag`.

```csharp
byte[] key = Crypto.RandomBytes(32);
byte[] iv  = Crypto.RandomBytes(12);

byte[] ct = Crypto.AesEncryptGcm(
    plaintext:      "Hello"u8,
    key:            key,
    iv:             iv,
    additionalData: ReadOnlySpan<byte>.Empty);
```

### `AesDecryptGcm(ciphertextWithTag, key, iv, additionalData) → byte[]`

Expects the 16-byte tag appended at the end. Throws `CryptographicException` on authentication failure.

```csharp
byte[] pt = Crypto.AesDecryptGcm(ct, key, iv, ReadOnlySpan<byte>.Empty);
```

---

## AES-256-CBC

AES with Cipher Block Chaining. Keys must be **32 bytes**; IVs must be **16 bytes**. Used for legacy media decryption.

### `AesEncrypt(plaintext, key) → byte[]`

Pads with PKCS#7, generates a random 16-byte IV, prepends it to the output, and returns `iv ++ ciphertext`.

```csharp
byte[] ct = Crypto.AesEncrypt(plaintext, key);
// ct[..16] = random IV, ct[16..] = ciphertext
```

### `AesDecrypt(buffer, key) → byte[]`

Reads the IV from the first 16 bytes of `buffer`, then decrypts and un-pads the remainder.

```csharp
byte[] pt = Crypto.AesDecrypt(ct, key);
```

### `AesEncryptWithIv(plaintext, key, iv) → byte[]`

Encrypts without prepending the IV — caller supplies the IV explicitly.

```csharp
byte[] iv = Crypto.RandomBytes(16);
byte[] ct = Crypto.AesEncryptWithIv(plaintext, key, iv);
```

### `AesDecryptWithIv(ciphertext, key, iv) → byte[]`

Decrypts when the IV is known separately.

```csharp
byte[] pt = Crypto.AesDecryptWithIv(ct, key, iv);
```

---

## AES-256-CTR

Stream cipher mode — no padding required, no authentication tag.

### `AesDecryptCtr(ciphertext, key, iv) → byte[]`

```csharp
byte[] pt = Crypto.AesDecryptCtr(ciphertext, key, iv);
```

### `AesEncryptCtr(plaintext, key, iv) → byte[]`

```csharp
byte[] ct = Crypto.AesEncryptCtr(plaintext, key, iv);
```

---

## HMAC

### `HmacSha256(data, key) → byte[]`

Returns a 32-byte HMAC-SHA-256 digest.

```csharp
byte[] mac = Crypto.HmacSha256(data, key);
```

### `HmacSha512(data, key) → byte[]`

Returns a 64-byte HMAC-SHA-512 digest.

```csharp
byte[] mac = Crypto.HmacSha512(data, key);
```

---

## Hash Functions

### `Sha256(data) → byte[]`

```csharp
byte[] hash = Crypto.Sha256("some data"u8);
```

### `Md5(data) → byte[]`

```csharp
byte[] hash = Crypto.Md5(data);
```

---

## HKDF (RFC 5869)

Extracts and expands key material using HKDF-SHA-256.

### `Hkdf(inputKeyMaterial, outputLength, salt, info) → byte[]`

| Parameter | Description |
|---|---|
| `inputKeyMaterial` | The IKM (e.g. a shared secret) |
| `outputLength` | Number of output bytes to produce |
| `salt` | Optional salt (`default` → all-zero salt) |
| `info` | Optional context bytes |

```csharp
// Derive a 64-byte key from a Diffie-Hellman shared secret
byte[] keys = Crypto.Hkdf(
    inputKeyMaterial: sharedSecret,
    outputLength:     64,
    info:             "WhatsApp Handshake"u8);

// Split into write/read key pair
byte[] writeKey = keys[..32];
byte[] readKey  = keys[32..];
```

---

## PBKDF2

Password-based key derivation (used for pairing-code flow).

### `DerivePairingCodeKey(pairingCode, salt) → byte[]`

Derives a 32-byte key using PBKDF2-SHA-256 with 131 072 iterations — matches the TypeScript `derivePairingCodeKey` function.

```csharp
byte[] key = Crypto.DerivePairingCodeKey(
    pairingCode: "ABCD-1234",
    salt:        saltBytes);
```

---

## Curve25519 Key Operations

Key generation and DH are exposed through `AuthUtils` rather than `Crypto`, but rely on the Curve25519 implementation in `AuthUtils`:

```csharp
using Baileys.Utils;

// Generate a Curve25519 key-pair
KeyPair kp = AuthUtils.GenerateKeyPair();

// Diffie-Hellman shared secret
byte[] secret = AuthUtils.DiffieHellman(myPrivate, theirPublic);
```

See [Auth Utilities](auth-utils.md) for the full reference.

---

## Usage Notes

- **AES-256-GCM** is the primary cipher for the Noise protocol handshake and transport.
- **AES-256-CBC** is used for legacy media file decryption.
- **HMAC-SHA-256** provides message authentication in the Noise handshake's `MixHash` and `MixKey` steps.
- **HKDF** derives the symmetric keys used after the Noise handshake completes.
- All methods accept `ReadOnlySpan<byte>` or `byte[]` inputs — the overloads are interchangeable.
