# Getting Started

This guide walks you from installing the package to establishing a valid set of WhatsApp Web credentials and encoding your first binary node.

---

## Requirements

- **.NET 10 SDK** or later — [download](https://dotnet.microsoft.com/download)
- A WhatsApp account to pair with

---

## 1. Install the Package

```bash
dotnet add package Baileys
```

Or add it directly to your `.csproj`:

```xml
<PackageReference Include="Baileys" Version="7.0.0" />
```

---

## 2. Initialise Credentials

Every WhatsApp Web session needs a set of cryptographic credentials. Generate a fresh set with `AuthUtils.InitAuthCreds()`:

```csharp
using Baileys.Utils;
using Baileys.Types;

AuthenticationCreds creds = AuthUtils.InitAuthCreds();

Console.WriteLine($"Registration ID : {creds.RegistrationId}");
Console.WriteLine($"Noise key (pub) : {Convert.ToBase64String(creds.NoiseKey.Public)}");
Console.WriteLine($"ADV secret      : {creds.AdvSecretKey}");
```

> **Tip:** Persist `creds` using a [Session Storage Provider](session-storage.md) so you don't need to re-pair on every restart.

---

## 3. Decode & Encode JIDs

WhatsApp uses JIDs (Jabber IDs) to address users, groups, and channels:

```csharp
using Baileys.Utils;
using Baileys.Types;

// Decode a raw JID string
FullJid? jid = JidUtils.JidDecode("15551234567@s.whatsapp.net");
// jid.User   → "15551234567"
// jid.Server → JidServer.SWhatsappNet

// Encode back to a string
string raw = JidUtils.JidEncode("15551234567", JidServer.SWhatsappNet);
// → "15551234567@s.whatsapp.net"

// Normalise (strip device/agent suffix)
string normalised = JidUtils.JidNormalizedUser("15551234567:5@s.whatsapp.net");
// → "15551234567@s.whatsapp.net"

// JID predicates
bool isGroup     = JidUtils.IsJidGroup("120363000000001@g.us");     // true
bool isUser      = JidUtils.IsJidUser("15551234567@s.whatsapp.net"); // true
bool isBroadcast = JidUtils.IsJidBroadcast("status@broadcast");      // true
```

See the full reference: [JID Utilities](api/jid-utils.md)

---

## 4. Cryptographic Primitives

The `Crypto` class wraps `System.Security.Cryptography` — no external dependencies:

```csharp
using Baileys.Utils;

// Random bytes
byte[] key = Crypto.RandomBytes(32);  // AES-256 key
byte[] iv  = Crypto.RandomBytes(12);  // GCM nonce

// AES-256-GCM encrypt → ciphertext ++ 16-byte tag
byte[] ct = Crypto.AesEncryptGcm(
    plaintext:      "Hello WhatsApp!"u8,
    key:            key,
    iv:             iv,
    additionalData: ReadOnlySpan<byte>.Empty);

// AES-256-GCM decrypt
byte[] pt = Crypto.AesDecryptGcm(ct, key, iv, ReadOnlySpan<byte>.Empty);

// HMAC-SHA-256
byte[] mac = Crypto.HmacSha256(data, key);

// HKDF
byte[] derived = Crypto.HkdfSha256(inputKeyMaterial, length: 32, salt: null, info: "WhatsApp Handshake"u8);
```

See the full reference: [Cryptography](api/crypto.md)

---

## 5. WABinary — Encode and Decode Nodes

The WhatsApp wire protocol uses a compact binary node format. Use `WaBinaryEncoder` and `WaBinaryDecoder` to serialise/deserialise:

```csharp
using Baileys.WABinary;
using Baileys.Utils;

// Build a node
var node = new BinaryNode
{
    Tag   = "iq",
    Attrs = new Dictionary<string, string>
    {
        ["type"] = "get",
        ["id"]   = Generics.GenerateMessageId(),
        ["to"]   = "s.whatsapp.net"
    }
    // Content is null (no child nodes or bytes)
};

// Encode → raw bytes ready to send over the WebSocket
byte[] wire = WaBinaryEncoder.EncodeBinaryNode(node);

// Decode bytes received from the WebSocket
BinaryNode decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);
Console.WriteLine(decoded.Tag);               // "iq"
Console.WriteLine(decoded.Attrs["type"]);     // "get"
```

See the full reference: [WABinary Codec](api/wabinary.md)

---

## 6. Using Dependency Injection

For ASP.NET Core or Worker Service applications, register the package with the built-in DI container:

```csharp
// Program.cs

// In-memory session (no persistence)
builder.Services.AddBaileys(o => o.PhoneNumber = "15551234567");

// File-based session persistence
builder.Services.AddBaileysWithFileStorage(
    filePath: "baileys_auth.json",
    configure: o => o.PhoneNumber = "15551234567");
```

Then inject `IAuthStateProvider` and `IOptions<BaileysOptions>` wherever needed:

```csharp
using Baileys.Session;
using Baileys.Options;
using Microsoft.Extensions.Options;

public class MyWhatsAppService(
    IAuthStateProvider session,
    IOptions<BaileysOptions> options)
{
    public async Task ConnectAsync()
    {
        var creds = await session.LoadCredsAsync();
        Console.WriteLine($"Phone: {options.Value.PhoneNumber}");
        Console.WriteLine($"Registered: {creds.Registered}");
    }
}
```

See the full guide: [Dependency Injection](dependency-injection.md)

---

## Next Steps

| Topic | Link |
|-------|------|
| All DI options and config binding | [Dependency Injection](dependency-injection.md) |
| Saving sessions to a database | [Session Storage](session-storage.md) |
| Complete type reference | [Types](api/types.md) |
| Event payload types | [Events](api/events.md) |
| Protocol constants | [Defaults & Constants](api/defaults.md) |
