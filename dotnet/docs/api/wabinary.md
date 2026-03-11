# WABinary Codec API Reference

The `Baileys.WABinary` namespace contains the binary protocol codec for the WhatsApp Web wire format. It provides a full encoder and decoder that mirror `WABinary/encode.ts` and `WABinary/decode.ts`.

---

## Namespace

```csharp
using Baileys.WABinary;
using Baileys.Types;
```

---

## `BinaryNode` — the Core Type

Every message exchanged over the WhatsApp WebSocket is a `BinaryNode` (the .NET equivalent of the TypeScript `BinaryNode` union type).

```csharp
namespace Baileys.Types;

public sealed class BinaryNode
{
    // XML-like element tag  (e.g. "iq", "message", "receipt")
    public required string Tag { get; init; }

    // String attributes on the element
    public Dictionary<string, string> Attrs { get; init; } = new();

    // Content — one of:
    //   null                         → no content
    //   IReadOnlyList<BinaryNode>    → child nodes
    //   byte[]                       → raw bytes
    //   string                       → plain text
    public object? Content { get; init; }
}
```

### Content Helpers

```csharp
// Check and access content by type
if (node.Content is IReadOnlyList<BinaryNode> children)
{
    foreach (var child in children)
        Console.WriteLine(child.Tag);
}

if (node.Content is byte[] bytes)
{
    Console.WriteLine($"{bytes.Length} raw bytes");
}

if (node.Content is string text)
{
    Console.WriteLine(text);
}
```

---

## `WaBinaryEncoder`

### `EncodeBinaryNode(BinaryNode node) → byte[]`

Serialises a `BinaryNode` tree to the WhatsApp binary wire format. The output starts with a `0x00` (no-compression) byte.

**Features:**
- Attributes are compacted using the WhatsApp token tables (236 single-byte + 4 × 256 double-byte tokens).
- Integer attribute values are packed as nibble or hex sequences when possible.
- JID attribute values are stored in a compact 3-byte form.

```csharp
// Simple IQ node
var node = new BinaryNode
{
    Tag   = "iq",
    Attrs = new()
    {
        ["type"] = "get",
        ["id"]   = Generics.GenerateMessageId(),
        ["to"]   = "s.whatsapp.net"
    }
};

byte[] wire = WaBinaryEncoder.EncodeBinaryNode(node);
// wire[0] == 0x00  (no compression flag)
```

```csharp
// Node with child nodes
var outer = new BinaryNode
{
    Tag     = "stream:features",
    Attrs   = new(),
    Content = new List<BinaryNode>
    {
        new() { Tag = "sm", Attrs = new() { ["xmlns"] = "urn:ietf:params:xml:ns:xmpp-session" } },
        new() { Tag = "ver", Attrs = new() { ["xmlns"] = "urn:xmpp:features:rosterver" } }
    }
};

byte[] wire = WaBinaryEncoder.EncodeBinaryNode(outer);
```

```csharp
// Node with raw bytes as content
var mediaNode = new BinaryNode
{
    Tag     = "binary",
    Attrs   = new() { ["enc"] = "v2" },
    Content = encryptedPayload   // byte[]
};

byte[] wire = WaBinaryEncoder.EncodeBinaryNode(mediaNode);
```

---

## `WaBinaryDecoder`

### `DecodeBinaryNodeAsync(byte[] buffer) → Task<BinaryNode>`

Deserialises raw bytes received from the WhatsApp WebSocket. Automatically decompresses zlib-deflate payloads (when bit 1 of the first byte is set).

```csharp
BinaryNode node = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);

Console.WriteLine(node.Tag);              // "iq"
Console.WriteLine(node.Attrs["type"]);    // "get"
Console.WriteLine(node.Attrs["id"]);      // "3EB0…"
```

### `DecodeDecompressedBinaryNode(byte[] buffer) → BinaryNode`

Synchronous decode when you know the buffer is already decompressed.

```csharp
BinaryNode node = WaBinaryDecoder.DecodeDecompressedBinaryNode(buffer);
```

---

## `WaBinaryConstants`

Token tables used by the codec — you typically don't need to access these directly.

| Member | Type | Description |
|---|---|---|
| `SingleByteTokens` | `string[]` | 236 single-byte token strings |
| `DoubleByteTokens` | `string[][]` | 4 × 256 double-byte token tables |

---

## Round-Trip Example

```csharp
using Baileys.WABinary;
using Baileys.Types;
using Baileys.Utils;

// Build
var node = new BinaryNode
{
    Tag   = "iq",
    Attrs = new()
    {
        ["id"]   = Generics.GenerateMessageId(),
        ["type"] = "set",
        ["to"]   = "s.whatsapp.net"
    },
    Content = new List<BinaryNode>
    {
        new()
        {
            Tag   = "pair-device",
            Attrs = new() { ["xmlns"] = "md" },
            Content = new List<BinaryNode>
            {
                new()
                {
                    Tag     = "device-identity",
                    Attrs   = new(),
                    Content = deviceIdentityBytes   // byte[]
                }
            }
        }
    }
};

// Encode
byte[] wire = WaBinaryEncoder.EncodeBinaryNode(node);

// Decode
BinaryNode decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);

// Verify
Assert.Equal("iq",           decoded.Tag);
Assert.Equal("set",          decoded.Attrs["type"]);
Assert.Equal("s.whatsapp.net", decoded.Attrs["to"]);

var children = (IReadOnlyList<BinaryNode>)decoded.Content!;
Assert.Equal("pair-device", children[0].Tag);
```

---

## Message ID Generation

`Generics.GenerateMessageId()` produces a unique message ID string in WhatsApp's format (`"3EB0"` prefix + 8 Crockford base-32 characters):

```csharp
using Baileys.Utils;

string id = Generics.GenerateMessageId();
// e.g. "3EB0A8B2C4D6E8F0"
```
