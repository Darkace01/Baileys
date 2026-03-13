# Baileys .NET — Documentation

> **A .NET 10 port of the [Baileys](https://github.com/WhiskeySockets/Baileys) WhatsApp Web library.**

[![NuGet](https://img.shields.io/nuget/v/Baileys.NET.svg)](https://www.nuget.org/packages/Baileys.NET)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/WhiskeySockets/Baileys/blob/master/LICENSE)
[![.NET 10](https://img.shields.io/badge/.NET-10.0-blue.svg)](https://dotnet.microsoft.com)

---

## Table of Contents

| Guide | Description |
|-------|-------------|
| [Getting Started](getting-started.md) | Installation, quick start, first session |
| [Dependency Injection](dependency-injection.md) | Configure the package with Microsoft DI |
| [Session Storage](session-storage.md) | In-memory, file, and custom DB providers |
| **API Reference** | |
| [Types](api/types.md) | All WhatsApp type definitions |
| [Events](api/events.md) | Event payload types |
| [Cryptography](api/crypto.md) | AES-GCM/CBC/CTR, HMAC, HKDF, PBKDF2 |
| [JID Utilities](api/jid-utils.md) | Encode, decode, classify JIDs |
| [Auth Utilities](api/auth-utils.md) | Credential initialisation, key generation |
| [WABinary Codec](api/wabinary.md) | Binary node encoding/decoding |
| [Defaults & Constants](api/defaults.md) | Protocol constants, browser descriptions |

---

## Overview

The `Baileys.NET` NuGet package provides all the building blocks needed to interact with the WhatsApp Web binary protocol from .NET:

```
Baileys.NET
├── Types/          — All WhatsApp domain types (Auth, Chat, Contact, Group, …)
├── Utils/          — Crypto, JID, Auth, Noise protocol, Generics, Logging
├── WABinary/       — Binary protocol encoder/decoder
├── Defaults/       — Protocol constants and browser presets
├── Options/        — BaileysOptions (phone number / config)
├── Session/        — IAuthStateProvider + InMemory & File implementations
└── Extensions/     — IServiceCollection registration helpers
```

## Package Information

| Property | Value |
|---|---|
| **Package ID** | `Baileys.NET` |
| **Current Version** | 1.2.0 |
| **Target Framework** | `net10.0` |
| **License** | MIT |
| **Runtime Dependencies** | `Microsoft.Extensions.DependencyInjection.Abstractions` 10.0.4, `Microsoft.Extensions.Options` 10.0.4 |

## Installation

```bash
dotnet add package Baileys.NET
```

```xml
<!-- .csproj -->
<PackageReference Include="Baileys.NET" Version="1.2.0" />
```

## Namespace Map

| Namespace | Contents |
|---|---|
| `Baileys.Types` | All domain types: `AuthenticationCreds`, `Chat`, `Contact`, `GroupMetadata`, `BinaryNode`, … |
| `Baileys.Utils` | `Crypto`, `JidUtils`, `AuthUtils`, `Generics`, `NoiseHandler`, `ILogger` |
| `Baileys.WABinary` | `WaBinaryEncoder`, `WaBinaryDecoder`, `WaBinaryConstants` |
| `Baileys.Defaults` | `BaileysDefaults`, `Browsers` |
| `Baileys.Options` | `BaileysOptions` |
| `Baileys.Session` | `IAuthStateProvider`, `InMemoryAuthStateProvider`, `FileAuthStateProvider` |
| `Baileys.Extensions` | `ServiceCollectionExtensions` |
