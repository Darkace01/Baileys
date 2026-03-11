# JID Utilities API Reference

WhatsApp uses JIDs (Jabber IDs) to address every entity — users, groups, broadcast lists, newsletters, bots, and hosted servers.  
`Baileys.Utils.JidUtils` provides encode, decode, normalise, and classify helpers that mirror `WABinary/jid-utils.ts`.

---

## Namespace

```csharp
using Baileys.Utils;
using Baileys.Types;
```

---

## JID Format

A full JID has the form:

```
[user][_agent][:device]@server
```

| Part | Example | Description |
|---|---|---|
| `user` | `15551234567` | Phone number or group/channel ID |
| `agent` | `_2` | Optional multi-agent suffix |
| `device` | `:5` | Optional multi-device index |
| `server` | `s.whatsapp.net` | JID server domain |

---

## `JidServer` Enum

```csharp
public enum JidServer
{
    ContactUs,       // "c.us"          — legacy contact server
    GroupUs,         // "g.us"          — group chats
    Broadcast,       // "broadcast"     — broadcast lists
    SWhatsappNet,    // "s.whatsapp.net" — standard users (Signal)
    Call,            // "call"          — call JIDs
    Lid,             // "lid"           — linked-device IDs
    Newsletter,      // "newsletter"    — WhatsApp Channels
    Bot,             // "bot"           — WhatsApp Bots
    Hosted,          // "hosted"        — hosted servers
    HostedLid        // "hosted.lid"    — hosted LID servers
}
```

---

## Encoding & Decoding

### `JidEncode(user, server, device?, agent?) → string`

Builds a JID string from its components.

```csharp
// Standard user JID
string jid = JidUtils.JidEncode("15551234567", JidServer.SWhatsappNet);
// → "15551234567@s.whatsapp.net"

// Group JID
string group = JidUtils.JidEncode("120363000000001", JidServer.GroupUs);
// → "120363000000001@g.us"

// Multi-device JID (user:device@server)
string md = JidUtils.JidEncode("15551234567", JidServer.SWhatsappNet, device: 3);
// → "15551234567:3@s.whatsapp.net"

// Multi-agent JID (user_agent:device@server)
string ma = JidUtils.JidEncode("15551234567", JidServer.SWhatsappNet, device: 3, agent: 1);
// → "15551234567_1:3@s.whatsapp.net"
```

---

### `JidDecode(jid) → FullJid?`

Parses a JID string. Returns `null` when the input is null, empty, or malformed.

```csharp
FullJid? jid = JidUtils.JidDecode("15551234567@s.whatsapp.net");
// jid.User       → "15551234567"
// jid.Server     → JidServer.SWhatsappNet
// jid.Device     → null
// jid.DomainType → 0  (WaJidDomains.WhatsApp)

FullJid? group = JidUtils.JidDecode("120363000000001@g.us");
// group.User   → "120363000000001"
// group.Server → JidServer.GroupUs

FullJid? md = JidUtils.JidDecode("15551234567:3@s.whatsapp.net");
// md.User   → "15551234567"
// md.Device → 3
```

---

### `JidNormalizedUser(jid) → string`

Removes the `:device` and `_agent` suffixes, and replaces `c.us` with `s.whatsapp.net`.

```csharp
string norm = JidUtils.JidNormalizedUser("15551234567:3@s.whatsapp.net");
// → "15551234567@s.whatsapp.net"

string norm2 = JidUtils.JidNormalizedUser("15551234567@c.us");
// → "15551234567@s.whatsapp.net"
```

---

## Classification Predicates

All predicates return `false` for `null` or empty input.

| Method | Returns `true` when … | Example input |
|--------|----------------------|---------------|
| `IsPnUser(jid)` | Server is `s.whatsapp.net` | `"123@s.whatsapp.net"` |
| `IsLidUser(jid)` | Server is `lid` | `"123@lid"` |
| `IsJidGroup(jid)` | Server is `g.us` | `"120363…@g.us"` |
| `IsJidBroadcast(jid)` | Server is `broadcast` | `"status@broadcast"` |
| `IsJidStatusBroadcast(jid)` | JID is exactly `status@broadcast` | `"status@broadcast"` |
| `IsJidNewsletter(jid)` | Server is `newsletter` | `"abc@newsletter"` |
| `IsJidMetaAi(jid)` | JID ends with `@bot` | `"13135550002@bot"` |
| `IsJidBot(jid)` | JID matches known bot number pattern | `"1313555XXXX@c.us"` |
| `IsHostedPnUser(jid)` | Server is `hosted` | `"123@hosted"` |
| `IsHostedLidUser(jid)` | Server is `hosted.lid` | `"123@hosted.lid"` |
| `AreJidsSameUser(jid1, jid2)` | Normalised users are equal | Compare two JID variants |

```csharp
JidUtils.IsJidGroup("120363000000001@g.us");    // true
JidUtils.IsPnUser("15551234567@s.whatsapp.net"); // true
JidUtils.IsJidBroadcast("status@broadcast");     // true
JidUtils.IsJidNewsletter("abc123@newsletter");   // true
JidUtils.IsLidUser("12345@lid");                 // true

JidUtils.AreJidsSameUser(
    "15551234567:3@s.whatsapp.net",
    "15551234567@c.us");  // true — same normalised user
```

---

## Server Helpers

### `ServerToString(JidServer) → string`

```csharp
JidUtils.ServerToString(JidServer.GroupUs);   // "g.us"
JidUtils.ServerToString(JidServer.Newsletter); // "newsletter"
```

### `ServerFromString(string) → JidServer`

```csharp
JidUtils.ServerFromString("g.us");        // JidServer.GroupUs
JidUtils.ServerFromString("newsletter");  // JidServer.Newsletter
JidUtils.ServerFromString("unknown");     // JidServer.ContactUs (fallback)
```

---

## `FullJid` Record

```csharp
namespace Baileys.Types;

public sealed record FullJid(
    string     User,
    JidServer  Server,
    int?       Device     = null,
    int?       DomainType = null);
```
