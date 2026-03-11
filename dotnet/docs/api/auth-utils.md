# Auth Utilities API Reference

`Baileys.Utils.AuthUtils` mirrors the TypeScript `Utils/auth-utils.ts` module. It provides credential initialisation, Curve25519 key-pair generation, and the Diffie-Hellman shared-secret computation used in the Noise protocol handshake.

---

## Namespace

```csharp
using Baileys.Utils;
using Baileys.Types;
```

---

## Credential Initialisation

### `InitAuthCreds() → AuthenticationCreds`

Creates a **fresh** set of `AuthenticationCreds` for a new WhatsApp Web session. This is the .NET equivalent of the TypeScript `initAuthCreds()` function.

```csharp
AuthenticationCreds creds = AuthUtils.InitAuthCreds();

// The returned object contains:
// creds.NoiseKey                — Curve25519 key-pair for the Noise handshake
// creds.PairingEphemeralKeyPair — ephemeral Curve25519 key-pair for pairing
// creds.SignedIdentityKey       — long-term Signal identity key-pair
// creds.SignedPreKey            — first signed pre-key (keyId = 1)
// creds.RegistrationId         — random Signal registration ID
// creds.AdvSecretKey            — base64-encoded 32-byte ADV secret
// creds.NextPreKeyId            → 1
// creds.FirstUnuploadedPreKeyId → 1
// creds.AccountSyncCounter      → 0
// creds.Registered              → false
```

> Persist the returned credentials with an [`IAuthStateProvider`](../session-storage.md) immediately. If you call `InitAuthCreds()` again, you will get a **completely different** identity that WhatsApp will treat as a new device.

---

## Key-Pair Generation

### `GenerateKeyPair() → KeyPair`

Generates a Curve25519 key-pair. The private scalar is clamped per **RFC 7748 §5** before the public key is derived.

```csharp
KeyPair kp = AuthUtils.GenerateKeyPair();
// kp.Public  — 32-byte X25519 public key
// kp.Private — 32-byte clamped private scalar
```

---

## Diffie-Hellman

### `DiffieHellman(privateKey, publicKey) → byte[]`

Computes a 32-byte Curve25519 shared secret:  
`output = X25519(clamp(privateKey), publicKey)`

```csharp
byte[] sharedSecret = AuthUtils.DiffieHellman(
    myKeyPair.Private,
    theirPublicKey);
```

The private key is re-clamped inside the function even if it was already clamped.

---

## Signed Pre-Key Generation

### `GenerateSignedPreKey(identityKey, keyId) → SignedKeyPair`

Generates a new pre-key pair and signs the public key with `identityKey.Private` using the XEdDSA scheme. The signature prefix byte `0x05` is prepended before signing.

```csharp
KeyPair identity  = AuthUtils.GenerateKeyPair();
SignedKeyPair spk = AuthUtils.GenerateSignedPreKey(identity, keyId: 1);

// spk.KeyPair         — the new Curve25519 pre-key pair
// spk.Signature       — 64-byte XEdDSA signature
// spk.KeyId           → 1
// spk.TimestampSeconds — current Unix time
```

---

## `AuthenticationCreds` Type

The full credential set for a WhatsApp Web session:

| Property | Type | Description |
|---|---|---|
| `NoiseKey` | `KeyPair` | Curve25519 key-pair for the Noise_XX handshake |
| `PairingEphemeralKeyPair` | `KeyPair` | Ephemeral key-pair for the pairing flow |
| `AdvSecretKey` | `string` | Base64-encoded 32-byte ADV secret |
| `SignedIdentityKey` | `KeyPair` | Long-term Signal identity key-pair |
| `SignedPreKey` | `SignedKeyPair` | First signed pre-key |
| `RegistrationId` | `int` | Random Signal registration ID (1–16383) |
| `NextPreKeyId` | `int` | Next pre-key ID to upload |
| `FirstUnuploadedPreKeyId` | `int` | Lowest pre-key ID not yet uploaded |
| `AccountSyncCounter` | `int` | App-state sync counter |
| `Registered` | `bool` | `true` after the first successful registration |
| `PairingCode` | `string?` | Pairing code (if using code-based pairing) |
| `LastPropHash` | `string?` | Hash of last received props |
| `RoutingInfo` | `byte[]?` | Routing info bytes |
| `LastAccountSyncTimestamp` | `long?` | Unix timestamp of last account sync |
| `Platform` | `string?` | Platform string sent during registration |
| `AccountSettings` | `AccountSettings` | Per-account settings |

---

## `KeyPair` Record

```csharp
public sealed record KeyPair(byte[] Public, byte[] Private);
```

---

## `SignedKeyPair` Record

```csharp
public sealed record SignedKeyPair(
    KeyPair KeyPair,
    byte[]  Signature,
    int     KeyId,
    long?   TimestampSeconds = null);
```

---

## `AccountSettings` Class

```csharp
public sealed class AccountSettings
{
    public bool UnarchiveChats { get; set; }
    public int? DefaultEphemeralExpiration { get; set; }
    public long? DefaultEphemeralSettingTimestamp { get; set; }
}
```
