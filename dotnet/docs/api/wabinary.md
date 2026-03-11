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

    // Content is a discriminated union — one of:
    //   null              → no content
    //   BinaryNodeList    → child nodes  (.Children: IReadOnlyList<BinaryNode>)
    //   BinaryNodeString  → plain text   (.Value: string)
    //   BinaryNodeBytes   → raw binary   (.Data: byte[])
    public BinaryNodeContent? Content { get; init; }
}
```

### Content Hierarchy

```csharp
// BinaryNodeContent is an abstract base class
public abstract class BinaryNodeContent { }

// Child nodes
public sealed class BinaryNodeList(IReadOnlyList<BinaryNode> children) : BinaryNodeContent
{
    public IReadOnlyList<BinaryNode> Children { get; }
}

// Plain text
public sealed class BinaryNodeString(string value) : BinaryNodeContent
{
    public string Value { get; }
}

// Raw bytes
public sealed class BinaryNodeBytes(byte[] data) : BinaryNodeContent
{
    public byte[] Data { get; }
}
```

### Content Helpers

```csharp
using Baileys.Types;

// Check and access content by type
if (node.Content is BinaryNodeList list)
{
    foreach (var child in list.Children)
        Console.WriteLine(child.Tag);
}

if (node.Content is BinaryNodeBytes bytes)
{
    Console.WriteLine($"{bytes.Data.Length} raw bytes");
}

if (node.Content is BinaryNodeString str)
{
    Console.WriteLine(str.Value);
}

// Extension helpers (no pattern matching needed)
IEnumerable<BinaryNode> children = BinaryNodeExtensions.GetChildren(node);
BinaryNode?             child    = BinaryNodeExtensions.GetChild(node, "pair-device");
byte[]?                 rawData  = BinaryNodeExtensions.GetBytes(node);
string?                 text     = BinaryNodeExtensions.GetString(node);
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
// Node with child nodes (BinaryNodeList content)
var outer = new BinaryNode
{
    Tag     = "stream:features",
    Attrs   = new(),
    Content = new BinaryNodeList(new List<BinaryNode>
    {
        new() { Tag = "sm",  Attrs = new() { ["xmlns"] = "urn:ietf:params:xml:ns:xmpp-session" } },
        new() { Tag = "ver", Attrs = new() { ["xmlns"] = "urn:xmpp:features:rosterver" } }
    })
};

byte[] wire = WaBinaryEncoder.EncodeBinaryNode(outer);
```

```csharp
// Node with raw bytes as content (BinaryNodeBytes)
var mediaNode = new BinaryNode
{
    Tag     = "binary",
    Attrs   = new() { ["enc"] = "v2" },
    Content = new BinaryNodeBytes(encryptedPayload)
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
    Content = new BinaryNodeList(new List<BinaryNode>
    {
        new()
        {
            Tag   = "pair-device",
            Attrs = new() { ["xmlns"] = "md" },
            Content = new BinaryNodeList(new List<BinaryNode>
            {
                new()
                {
                    Tag     = "device-identity",
                    Attrs   = new(),
                    Content = new BinaryNodeBytes(deviceIdentityBytes)
                }
            })
        }
    })
};

// Encode
byte[] wire = WaBinaryEncoder.EncodeBinaryNode(node);

// Decode
BinaryNode decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);

// Verify
Assert.Equal("iq",             decoded.Tag);
Assert.Equal("set",            decoded.Attrs["type"]);
Assert.Equal("s.whatsapp.net", decoded.Attrs["to"]);

var list = (BinaryNodeList)decoded.Content!;
Assert.Equal("pair-device", list.Children[0].Tag);
```

---

## Message ID Generation

`Generics.GenerateMessageId()` produces a unique message ID string in WhatsApp's format (`"3EB0"` prefix + 36 hexadecimal characters, representing 18 random bytes):

```csharp
using Baileys.Utils;

string id = Generics.GenerateMessageId();
// e.g. "3EB0A8B2C4D6E8F0A1B2C3D4E5F60718293A4B5C"
```
