# Baileys — WhatsApp Web Library for .NET 10

[![NuGet](https://img.shields.io/nuget/v/Baileys.svg)](https://www.nuget.org/packages/Baileys)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

A .NET 10 port of the [Baileys](https://github.com/WhiskeySockets/Baileys) TypeScript library for interacting with the WhatsApp Web API.

## Installation

```bash
dotnet add package Baileys
```

Or via the NuGet Package Manager:

```
Install-Package Baileys
```

## Features

- **Binary Protocol Codec** — Encode and decode the WhatsApp binary node wire format (WABinary), including zlib decompression
- **Cryptographic Utilities** — AES-256-GCM/CBC/CTR, HMAC-SHA-256/512, SHA-256/MD5, HKDF, PBKDF2 — all via the .NET built-in `System.Security.Cryptography` stack (no external dependencies)
- **JID Utilities** — Encode, decode, normalise, and classify JIDs (user, group, broadcast, newsletter, LID, hosted)
- **Auth Utilities** — `InitAuthCreds()`, Curve25519 key-pair generation, signed pre-key generation
- **Noise Protocol Handler** — Noise_XX_25519_AESGCM_SHA256 handshake state machine used for the WhatsApp WebSocket transport
- **Complete Type Definitions** — All WhatsApp types: Contact, Chat, GroupMetadata, Message, Call, Label, Newsletter, Business, Product, State (ConnectionState, WaPresence, privacy settings), Event payloads
- **Default Constants** — WebSocket URL, noise-protocol constants, media HKDF key mappings, browser description presets, timing defaults
- **Structured Logging** — `ILogger` interface + `NullLogger` and `ConsoleLogger` implementations

## Quick Start

```csharp
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;
using Baileys.Defaults;

// --- Credential initialisation ---
var creds = AuthUtils.InitAuthCreds();
Console.WriteLine($"Registration ID: {creds.RegistrationId}");

// --- JID utilities ---
var jid = JidUtils.JidDecode("123456789@s.whatsapp.net");
Console.WriteLine($"User: {jid?.User}, Server: {jid?.Server}");

var isGroup = JidUtils.IsJidGroup("120363000000001@g.us");   // true

// --- Crypto ---
var key = Crypto.RandomBytes(32);
var iv  = Crypto.RandomBytes(12);
var ct  = Crypto.AesEncryptGcm("Hello WhatsApp!"u8, key, iv, ReadOnlySpan<byte>.Empty);
var pt  = Crypto.AesDecryptGcm(ct, key, iv, ReadOnlySpan<byte>.Empty);

// --- WABinary round-trip ---
var node = new BinaryNode
{
    Tag   = "iq",
    Attrs = new() { ["type"] = "get", ["id"] = Generics.GenerateMessageId() }
};
var wire    = WaBinaryEncoder.EncodeBinaryNode(node);
var decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);

// --- Browser description ---
var browser = Browsers.MacOs("Chrome");  // ["Mac OS", "Chrome", "14.4.1"]
```

## Types

| Namespace | Key Types |
|-----------|-----------|
| `Baileys.Types` | `Contact`, `Chat`, `ChatUpdate`, `GroupMetadata`, `GroupParticipant`, `WaCallEvent`, `Label`, `LabelAssociation`, `NewsletterMetadata`, `ConnectionState`, `WaPresence`, `MinimalMessage`, `WaMessageKey`, `MediaType`, `BinaryNode` |
| `Baileys.Types` | Event payloads: `ConnectionUpdateEvent`, `MessagingHistorySetEvent`, `MessagesUpsertEvent`, `GroupParticipantsUpdateEvent`, `BlocklistUpdateEvent`, … |
| `Baileys.Utils` | `Crypto`, `Generics`, `JidUtils`, `AuthUtils`, `NoiseHandler`, `ILogger`, `NullLogger`, `ConsoleLogger` |
| `Baileys.Defaults` | `BaileysDefaults`, `Browsers` |
| `Baileys.WABinary` | `WaBinaryEncoder`, `WaBinaryDecoder`, `WaBinaryConstants` |

## Requirements

- .NET 10+
- No external NuGet dependencies

## License

MIT — see [LICENSE](https://github.com/WhiskeySockets/Baileys/blob/master/LICENSE)
