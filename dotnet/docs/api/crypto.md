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

AES with Cipher Block Chaining — requires a 16-byte IV. Used for historical media decryption.

### `AesDecryptCbc(ciphertext, key, iv, unpad) → byte[]`

| Parameter | Description |
|---|---|
| `ciphertext` | Encrypted bytes (length must be a multiple of 16) |
| `key` | 32-byte AES key |
| `iv` | 16-byte IV |
| `unpad` | When `true`, removes PKCS#7 padding from the result |

```csharp
byte[] pt = Crypto.AesDecryptCbc(ciphertext, key, iv, unpad: true);
```

### `AesEncryptCbc(plaintext, key, iv) → byte[]`

Pads with PKCS#7 automatically.

```csharp
byte[] ct = Crypto.AesEncryptCbc(plaintext, key, iv);
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

Extracts and expands key material.

### `HkdfSha256(ikm, length, salt, info) → byte[]`

| Parameter | Description |
|---|---|
| `ikm` | Input key material |
| `length` | Number of output bytes to produce |
| `salt` | Optional salt bytes (`null` → all-zero salt) |
| `info` | Context / info bytes |

```csharp
// Derive a 64-byte output
byte[] keys = Crypto.HkdfSha256(
    ikm:    sharedSecret,
    length: 64,
    salt:   null,
    info:   "WhatsApp Handshake"u8);

// Split into write/read key pairs
byte[] writeKey = keys[..32];
byte[] readKey  = keys[32..];
```

---

## PBKDF2

Password-based key derivation (used for pairing-code flow).

### `Pbkdf2HmacSha256(password, salt, iterations, keyLength) → byte[]`

```csharp
byte[] key = Crypto.Pbkdf2HmacSha256(
    password:  passwordBytes,
    salt:      saltBytes,
    iterations: 2 << 16,
    keyLength:  32);
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
