# Session Storage

Baileys separates the **authentication credentials** (`AuthenticationCreds`) and the **Signal-protocol key store** (`ISignalKeyStore`) from where they are stored. The `IAuthStateProvider` interface controls credential persistence; `ISignalKeyStore` controls Signal key persistence. Together they form an `AuthenticationState` — the .NET equivalent of the TypeScript `useMultiFileAuthState()` return value.

---

## The `IAuthStateProvider` Interface

```csharp
namespace Baileys.Session;

public interface IAuthStateProvider
{
    // Load persisted credentials, or create fresh ones if none exist
    Task<AuthenticationCreds> LoadCredsAsync(CancellationToken cancellationToken = default);

    // Persist credentials after each successful handshake / update
    Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken cancellationToken = default);

    // Remove all persisted state — forces a full re-pair on next connect
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

> **Thread safety:** All built-in providers use a `SemaphoreSlim` to serialise concurrent access. Your custom provider should do the same if it will be accessed from multiple threads.

---

## Built-in Providers

### 1. `InMemoryAuthStateProvider`

Stores credentials in process memory. State is **lost when the process exits**.

**Best for:** Development, testing, short-lived CLI tools.

```csharp
// Registration (via DI)
builder.Services.AddBaileys(o => o.PhoneNumber = "15551234567");

// Direct construction (without DI)
var provider = new InMemoryAuthStateProvider();

// Pre-load an existing credentials object
var provider = new InMemoryAuthStateProvider(existingCreds);
```

| Advantage | Limitation |
|---|---|
| Zero configuration | State lost on restart |
| Fastest (no I/O) | Not suitable for production |
| Isolated per process | Single-node only |

---

### 2. `FileAuthStateProvider`

Persists credentials as a JSON file on disk. On first save, the file is created automatically. The directory must already exist.

**Best for:** Single-server deployments, development with persistence, Docker containers with a volume mount.

```csharp
// Registration (via DI)
builder.Services.AddBaileysWithFileStorage(
    filePath:  "/var/data/whatsapp/creds.json",
    configure: o => o.PhoneNumber = "15551234567");

// Direct construction (without DI)
var provider = new FileAuthStateProvider("/var/data/whatsapp/creds.json");
```

#### File Format (example)

```json
{
  "noiseKey": {
    "public": "base64==",
    "private": "base64=="
  },
  "pairingEphemeralKeyPair": { ... },
  "advSecretKey": "base64==",
  "signedIdentityKey": { ... },
  "signedPreKey": {
    "keyPair": { ... },
    "signature": "base64==",
    "keyId": 1,
    "timestampSeconds": 1700000000
  },
  "registrationId": 12345,
  "firstUnuploadedPreKeyId": 1,
  "nextPreKeyId": 2,
  "accountSyncCounter": 0,
  "registered": false,
  "accountSettings": {
    "unarchiveChats": false
  }
}
```

All `byte[]` fields are stored as **Base64** strings. The file is overwritten on every `SaveCredsAsync` call.

| Advantage | Limitation |
|---|---|
| Persists across restarts | Not suitable for multi-instance (no distributed locking) |
| Human-readable JSON | Requires a writable file system path |
| Easy backup/restore | Credentials in plain text — secure the file |

---

### 3. `DirectoryAuthStateProvider`

The **recommended production provider** — mirrors the TypeScript `useMultiFileAuthState(folder)` integration pattern.

Stores everything in a single directory:
- **`creds.json`** — authentication credentials (same format as `FileAuthStateProvider`)
- **One file per Signal key** — named `{type}-{sanitized-id}` (e.g. `pre-key-1`, `session-jid@s.whatsapp.net-0`)

**Best for:** Any deployment that needs a full session including Signal-protocol keys — the closest match to the TypeScript Baileys integration.

```csharp
// Registration (via DI) — equivalent to JS useMultiFileAuthState("baileys_auth_info")
builder.Services.AddBaileysWithDirectoryStorage(
    directory: "baileys_auth_info",
    configure: o => o.PhoneNumber = "15551234567");

// Direct construction (without DI)
var provider = new DirectoryAuthStateProvider("baileys_auth_info");

// Load the full AuthenticationState (creds + Signal keys in one call)
AuthenticationState state = await provider.LoadAuthStateAsync();
// state.Creds — AuthenticationCreds (pass to handshake)
// state.Keys  — DirectorySignalKeyStore (pass as signal key store)

// Save updated credentials
await provider.SaveCredsAsync(state.Creds);

// Clear everything (forces full re-pair on next start)
await provider.ClearAsync();
```

The `Keys` property (a `DirectorySignalKeyStore`) persists automatically — no manual save call is needed for Signal keys.

| Advantage | Limitation |
|---|---|
| Full session state (creds + Signal keys) | Requires a writable directory |
| Survives process restarts | Not suitable for multi-instance without distributed locking |
| Matches TypeScript `useMultiFileAuthState` | Keys stored as plain files — secure the directory |
| Easy to backup (copy the directory) | |

---

## The `ISignalKeyStore` Interface

`ISignalKeyStore` is the Signal-protocol equivalent of `IAuthStateProvider` — it stores and retrieves all Signal cryptographic material (pre-keys, sessions, sender-keys, app-state sync keys, etc.).

```csharp
namespace Baileys.Types;

public interface ISignalKeyStore
{
    // Retrieve raw JSON bytes for the given type + IDs
    Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type, IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default);

    // Store or remove values (null value = remove)
    Task SetAsync(
        string type, IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default);

    // Remove all stored keys of every type
    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### Signal Data Types (`SignalDataTypes`)

Use the constants from `SignalDataTypes` as the `type` argument:

| Constant | Value | Contents |
|---|---|---|
| `SignalDataTypes.PreKey` | `"pre-key"` | X3DH pre-key pairs |
| `SignalDataTypes.Session` | `"session"` | Double-ratchet session state per JID |
| `SignalDataTypes.SenderKey` | `"sender-key"` | Group sender-key material |
| `SignalDataTypes.SenderKeyMemory` | `"sender-key-memory"` | Distribution tracker per group |
| `SignalDataTypes.AppStateSyncKey` | `"app-state-sync-key"` | App-state encryption keys |
| `SignalDataTypes.AppStateSyncVersion` | `"app-state-sync-version"` | LT-hash state for sync patches |
| `SignalDataTypes.LidMapping` | `"lid-mapping"` | LID ↔ phone-number pairs |
| `SignalDataTypes.DeviceList` | `"device-list"` | Known device IDs per JID |
| `SignalDataTypes.TcToken` | `"tctoken"` | TC-token data |
| `SignalDataTypes.IdentityKey` | `"identity-key"` | Raw identity-key bytes |

### Typed Extension Methods (`SignalKeyStoreExtensions`)

`SignalKeyStoreExtensions` adds typed `Get*/Set*Async` helpers so you don't have to work with raw bytes:

```csharp
using Baileys.Types;

var store = new InMemorySignalKeyStore();

// Pre-keys
var kp = new KeyPair([1, 2, 3], [4, 5, 6]);
await store.SetPreKeysAsync(new Dictionary<string, KeyPair?> { ["1"] = kp });
var preKeys = await store.GetPreKeysAsync(["1"]);

// Sessions (raw bytes)
await store.SetSessionsAsync(new Dictionary<string, byte[]?> { ["jid@s.whatsapp.net:0"] = sessionBytes });
var sessions = await store.GetSessionsAsync(["jid@s.whatsapp.net:0"]);

// App-state sync version (LT-hash state)
var state = new LtHashState { Version = 7, Hash = [0xDE, 0xAD] };
await store.SetAppStateSyncVersionsAsync(new Dictionary<string, LtHashState?> { ["critical_block"] = state });

// Sender-key distribution memory
var mem = new Dictionary<string, bool> { ["jid@s.whatsapp.net"] = true };
await store.SetSenderKeyMemoriesAsync(new Dictionary<string, Dictionary<string, bool>?> { ["group@g.us"] = mem });

// LID mappings
await store.SetLidMappingsAsync(new Dictionary<string, string?> { ["lid123"] = "15551234567" });
```

### Built-in `ISignalKeyStore` Implementations

#### `InMemorySignalKeyStore`

Thread-safe, `ConcurrentDictionary`-backed store. State is lost on process exit.

```csharp
var store = new InMemorySignalKeyStore();
```

**Best for:** Testing, ephemeral sessions, or as an inner store wrapped by a caching layer.

#### `DirectorySignalKeyStore`

Persists each key as a separate file — one file per `(type, id)` pair — using the same naming convention as the TypeScript `useMultiFileAuthState` helper.

```csharp
var store = new DirectorySignalKeyStore("baileys_auth_info");
// File names: pre-key-1, session-jid@s.whatsapp.net-0, sender-key-group__g.us, …
```

**Best for:** Production deployments as the backing store for `DirectoryAuthStateProvider`.

> **Note:** `DirectoryAuthStateProvider.Keys` is a `DirectorySignalKeyStore` — you rarely need to construct one directly.

---

## `AuthenticationState` — The Complete Session Bundle

`AuthenticationState` bundles `AuthenticationCreds` and `ISignalKeyStore` into a single object, mirroring the TypeScript `AuthenticationState` type:

```csharp
public sealed class AuthenticationState
{
    public required AuthenticationCreds Creds { get; init; }
    public required ISignalKeyStore     Keys  { get; init; }
}
```

### Building an `AuthenticationState`

Use the `LoadAuthStateAsync()` extension method on any `IAuthStateProvider`:

```csharp
using Baileys.Extensions;

// DirectoryAuthStateProvider — uses its built-in DirectorySignalKeyStore automatically:
var dirProvider = new DirectoryAuthStateProvider("baileys_auth_info");
AuthenticationState state = await dirProvider.LoadAuthStateAsync();

// Any other provider — an InMemorySignalKeyStore is created automatically:
var inMemProvider = new InMemoryAuthStateProvider();
AuthenticationState state2 = await inMemProvider.LoadAuthStateAsync();

// Any other provider — supply your own key store:
var fileProvider = new FileAuthStateProvider("creds.json");
var keys         = new DirectorySignalKeyStore("baileys_keys");
AuthenticationState state3 = await fileProvider.LoadAuthStateAsync(keys: keys);
```

---

## Custom Provider

Implement `IAuthStateProvider` to store credentials anywhere: a relational database, Redis, Azure Blob Storage, AWS Secrets Manager, etc.

### Example: Entity Framework Core

```csharp
using Baileys.Session;
using Baileys.Types;
using Baileys.Utils;
using System.Text.Json;

public class EfCoreAuthStateProvider(AppDbContext db) : IAuthStateProvider
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<AuthenticationCreds> LoadCredsAsync(CancellationToken ct = default)
    {
        var entity = await db.WhatsAppSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == "default", ct);

        if (entity is null)
            return AuthUtils.InitAuthCreds();

        return JsonSerializer.Deserialize<AuthenticationCreds>(entity.CredsJson, JsonOpts)
               ?? AuthUtils.InitAuthCreds();
    }

    public async Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(creds, JsonOpts);

        var entity = await db.WhatsAppSessions.FindAsync(["default"], ct);
        if (entity is null)
        {
            db.WhatsAppSessions.Add(new WhatsAppSession { Id = "default", CredsJson = json });
        }
        else
        {
            entity.CredsJson = json;
            db.WhatsAppSessions.Update(entity);
        }
        await db.SaveChangesAsync(ct);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        var entity = await db.WhatsAppSessions.FindAsync(["default"], ct);
        if (entity is not null)
        {
            db.WhatsAppSessions.Remove(entity);
            await db.SaveChangesAsync(ct);
        }
    }
}

// Register in DI
builder.Services.AddDbContext<AppDbContext>(/* ... */);
builder.Services.AddBaileysWithProvider<EfCoreAuthStateProvider>(o =>
{
    o.PhoneNumber = builder.Configuration["Baileys:PhoneNumber"]!;
});
```

### Example: Redis (StackExchange.Redis)

```csharp
using Baileys.Session;
using Baileys.Types;
using Baileys.Utils;
using StackExchange.Redis;
using System.Text.Json;

public class RedisAuthStateProvider(IConnectionMultiplexer redis, string sessionKey = "baileys:creds")
    : IAuthStateProvider
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task<AuthenticationCreds> LoadCredsAsync(CancellationToken ct = default)
    {
        var value = await _db.StringGetAsync(sessionKey);
        if (!value.HasValue)
            return AuthUtils.InitAuthCreds();

        return JsonSerializer.Deserialize<AuthenticationCreds>(value.ToString())
               ?? AuthUtils.InitAuthCreds();
    }

    public async Task SaveCredsAsync(AuthenticationCreds creds, CancellationToken ct = default)
    {
        var json = JsonSerializer.Serialize(creds);
        await _db.StringSetAsync(sessionKey, json);
    }

    public async Task ClearAsync(CancellationToken ct = default)
    {
        await _db.KeyDeleteAsync(sessionKey);
    }
}

// Register in DI
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("Redis")!));
builder.Services.AddBaileysWithProvider<RedisAuthStateProvider>(o =>
{
    o.PhoneNumber = "15551234567";
});
```

---

## Using the Provider Directly (without DI)

```csharp
using Baileys.Session;
using Baileys.Utils;

// In-memory
var provider = new InMemoryAuthStateProvider();
var creds    = await provider.LoadCredsAsync();

creds.Registered = true;
await provider.SaveCredsAsync(creds);

// File
var fileProvider = new FileAuthStateProvider("session.json");
var creds2       = await fileProvider.LoadCredsAsync();
await fileProvider.SaveCredsAsync(creds2);

// Clear (force re-pair)
await fileProvider.ClearAsync();
```

---

## Credential Lifecycle

```
LoadCredsAsync()          ← called once at startup
       │
       ▼
[QR / pairing code flow]
       │
       ▼
SaveCredsAsync(creds)     ← called after successful registration
       │
       ▼
[Active session …]
       │
     (disconnect / update)
       │
       ▼
SaveCredsAsync(creds)     ← called whenever creds are updated
       │
     (logout / factory reset)
       │
       ▼
ClearAsync()              ← removes all state
```

## Security Considerations

- Credentials contain **private keys** — protect the backing store with appropriate access controls.
- For the `FileAuthStateProvider`, restrict the file's permissions to the service account only (`chmod 600` on Linux).
- For database providers, store credentials in a separate, encrypted column or use a secrets-manager service.
- Never commit credentials files to source control — add them to `.gitignore`.
