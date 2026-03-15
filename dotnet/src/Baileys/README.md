# Baileys — WhatsApp Web Library for .NET 10

[![NuGet](https://img.shields.io/nuget/v/Baileys.NET.svg)](https://www.nuget.org/packages/Baileys.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com)

A .NET 10 port of the [Baileys](https://github.com/WhiskeySockets/Baileys) TypeScript library for interacting with the WhatsApp Web API.

## Installation

```bash
dotnet add package Baileys.NET
```

Or via the NuGet Package Manager:

```
Install-Package Baileys.NET
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
- **Signal Key Store** — `ISignalKeyStore` interface with in-memory (`InMemorySignalKeyStore`) and directory-backed (`DirectorySignalKeyStore`) implementations; typed extension helpers for every Signal data category (pre-key, session, sender-key, app-state-sync, lid-mapping, device-list, tctoken, identity-key)
- **Full Authentication State** — `AuthenticationState` bundles `AuthenticationCreds` + `ISignalKeyStore` exactly like the TypeScript `AuthenticationState` type; `LoadAuthStateAsync()` extension builds one from any `IAuthStateProvider`
- **Directory Auth Provider** — `DirectoryAuthStateProvider` stores credentials in `creds.json` and Signal keys as individual files in one directory, mirroring the TypeScript `useMultiFileAuthState(folder)` integration pattern
- **Default Constants** — WebSocket URL, noise-protocol constants, media HKDF key mappings, browser description presets, timing defaults
- **Structured Logging** — `ILogger` interface + `NullLogger` and `ConsoleLogger` implementations
- **Dependency Injection** — `AddBaileys()`, `AddBaileysWithFileStorage()`, `AddBaileysWithDirectoryStorage()`, `AddBaileysWithProvider<T>()` helpers
- **Pluggable Session Storage** — `IAuthStateProvider` interface with in-memory, file-based, and directory-based implementations

## Quick Start

```csharp
using Baileys.Types;
using Baileys.Utils;
using Baileys.WABinary;
using Baileys.Defaults;
using Baileys.Extensions;
using Baileys.Session;

// ── Dependency injection (Program.cs) ────────────────────────────────────────
// In-memory session (no persistence):
builder.Services.AddBaileys(o => o.PhoneNumber = "15551234567");

// File-based credentials only:
builder.Services.AddBaileysWithFileStorage("creds.json", o => o.PhoneNumber = "15551234567");

// Directory-based session — mirrors TypeScript useMultiFileAuthState("baileys_auth_info")
// Stores creds.json + one file per Signal key under the given directory:
builder.Services.AddBaileysWithDirectoryStorage(
    directory: "baileys_auth_info",
    configure: o => o.PhoneNumber = "15551234567");

// ── Load full AuthenticationState (creds + Signal keys) ──────────────────────
// Works with any IAuthStateProvider:
IAuthStateProvider provider = new DirectoryAuthStateProvider("baileys_auth_info");
AuthenticationState state = await provider.LoadAuthStateAsync();
// state.Creds — AuthenticationCreds for the handshake
// state.Keys  — ISignalKeyStore with all Signal protocol keys

// Or with an explicit key store:
var keys  = new InMemorySignalKeyStore();
var state2 = await provider.LoadAuthStateAsync(keys: keys);

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

// ── Typed Signal key access ───────────────────────────────────────────────────
var store = new InMemorySignalKeyStore();
var kp    = new KeyPair([1, 2, 3], [4, 5, 6]);
await store.SetPreKeysAsync(new Dictionary<string, KeyPair?> { ["1"] = kp });
var preKeys = await store.GetPreKeysAsync(["1"]);
```

## Namespace Map

| Namespace | Contents |
|-----------|-----------|
| `Baileys.Types` | All domain types and enums, `ISignalKeyStore`, `SignalDataTypes`, `AuthenticationState`, `SignalKeyStoreExtensions`, `TcToken` |
| `Baileys.Utils` | `Crypto`, `JidUtils`, `AuthUtils`, `Generics`, `NoiseHandler`, `ILogger` |
| `Baileys.WABinary` | `WaBinaryEncoder`, `WaBinaryDecoder`, `WaBinaryConstants` |
| `Baileys.Defaults` | `BaileysDefaults`, `Browsers` |
| `Baileys.Options` | `BaileysOptions` |
| `Baileys.Session` | `IAuthStateProvider`, `InMemoryAuthStateProvider`, `FileAuthStateProvider`, `DirectoryAuthStateProvider`, `ISignalKeyStore`, `InMemorySignalKeyStore`, `DirectorySignalKeyStore` |
| `Baileys.Extensions` | `ServiceCollectionExtensions`, `AuthStateExtensions` |

## Requirements

- .NET 10+
- Runtime NuGet dependencies: `Microsoft.Extensions.DependencyInjection.Abstractions` and `Microsoft.Extensions.Options`

## License

MIT — see [LICENSE](https://github.com/WhiskeySockets/Baileys/blob/master/LICENSE)
