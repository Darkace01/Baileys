# Baileys — WhatsApp Web Library for .NET 10

[![NuGet](https://img.shields.io/nuget/v/Baileys.svg)](https://www.nuget.org/packages/Baileys)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com)

A .NET 10 port of the [Baileys](https://github.com/WhiskeySockets/Baileys) TypeScript library for interacting with the WhatsApp Web API.

## Installation

```bash
dotnet add package Baileys
```

Or via the NuGet Package Manager:

```
Install-Package Baileys
```

## Documentation

Full documentation is available in the [`docs/`](../../docs/) folder:

| Guide | Description |
|-------|-------------|
| [Getting Started](../../docs/getting-started.md) | Installation, quick start, first session |
| [Dependency Injection](../../docs/dependency-injection.md) | ASP.NET Core / Worker Service integration |
| [Session Storage](../../docs/session-storage.md) | InMemory, File, and custom DB providers |
| [Cryptography](../../docs/api/crypto.md) | AES-GCM/CBC/CTR, HMAC, HKDF, PBKDF2 |
| [JID Utilities](../../docs/api/jid-utils.md) | Encode, decode, classify JIDs |
| [Auth Utilities](../../docs/api/auth-utils.md) | Credential init, key generation |
| [WABinary Codec](../../docs/api/wabinary.md) | Binary node encoding / decoding |
| [Types Reference](../../docs/api/types.md) | All WhatsApp domain types |
| [Events Reference](../../docs/api/events.md) | Event payload types |
| [Defaults & Constants](../../docs/api/defaults.md) | Protocol constants, browser presets |

## Features

- **Binary Protocol Codec** — Encode and decode the WhatsApp binary node wire format (WABinary), including zlib decompression
- **Cryptographic Utilities** — AES-256-GCM/CBC/CTR, HMAC-SHA-256/512, SHA-256/MD5, HKDF, PBKDF2 — all via `System.Security.Cryptography` (no third-party deps)
- **JID Utilities** — Encode, decode, normalise, and classify JIDs (user, group, broadcast, newsletter, LID, hosted)
- **Auth Utilities** — `InitAuthCreds()`, Curve25519 key-pair generation, signed pre-key generation
- **Noise Protocol Handler** — Noise_XX_25519_AESGCM_SHA256 handshake state machine
- **Complete Type Definitions** — All WhatsApp types: Contact, Chat, GroupMetadata, Message, Call, Label, Newsletter, Business, Product, State, Event payloads
- **Default Constants** — WebSocket URL, noise-protocol constants, media HKDF key mappings, browser description presets, timing defaults
- **Structured Logging** — `ILogger` interface + `NullLogger` and `ConsoleLogger` implementations
- **Dependency Injection** — `AddBaileys()`, `AddBaileysWithFileStorage()`, `AddBaileysWithProvider<T>()` helpers
- **Pluggable Session Storage** — `IAuthStateProvider` interface with in-memory and file-based implementations

## Quick Start

```csharp
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;
using Baileys.Defaults;
using Baileys.Extensions;

// ── Dependency injection (Program.cs) ────────────────────────────────────────
builder.Services.AddBaileys(o => o.PhoneNumber = "15551234567");
// or persist to a JSON file:
builder.Services.AddBaileysWithFileStorage("creds.json", o => o.PhoneNumber = "15551234567");

// ── Credential initialisation ─────────────────────────────────────────────────
var creds = AuthUtils.InitAuthCreds();
Console.WriteLine($"Registration ID: {creds.RegistrationId}");

// ── JID utilities ─────────────────────────────────────────────────────────────
var jid     = JidUtils.JidDecode("123456789@s.whatsapp.net");
var isGroup = JidUtils.IsJidGroup("120363000000001@g.us");    // true

// ── Crypto ────────────────────────────────────────────────────────────────────
var key = Crypto.RandomBytes(32);
var iv  = Crypto.RandomBytes(12);
var ct  = Crypto.AesEncryptGcm("Hello WhatsApp!"u8, key, iv, ReadOnlySpan<byte>.Empty);
var pt  = Crypto.AesDecryptGcm(ct, key, iv, ReadOnlySpan<byte>.Empty);

// ── WABinary round-trip ───────────────────────────────────────────────────────
var node    = new BinaryNode { Tag = "iq", Attrs = new() { ["type"] = "get" } };
var wire    = WaBinaryEncoder.EncodeBinaryNode(node);
var decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(wire);

// ── Browser description ───────────────────────────────────────────────────────
var browser = Browsers.MacOs("Chrome");   // ["Mac OS", "Chrome", "14.4.1"]
```

## Namespace Map

| Namespace | Contents |
|-----------|-----------|
| `Baileys.Types` | All domain types and enums |
| `Baileys.Utils` | `Crypto`, `JidUtils`, `AuthUtils`, `Generics`, `NoiseHandler`, `ILogger` |
| `Baileys.WABinary` | `WaBinaryEncoder`, `WaBinaryDecoder`, `WaBinaryConstants` |
| `Baileys.Defaults` | `BaileysDefaults`, `Browsers` |
| `Baileys.Options` | `BaileysOptions` |
| `Baileys.Session` | `IAuthStateProvider`, `InMemoryAuthStateProvider`, `FileAuthStateProvider` |
| `Baileys.Extensions` | `ServiceCollectionExtensions` |

## Requirements

- .NET 10+
- Runtime NuGet dependencies: `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`

## License

MIT — see [LICENSE](https://github.com/WhiskeySockets/Baileys/blob/master/LICENSE)
