# Session Storage

Baileys separates the **authentication credentials** (`AuthenticationCreds`) from where they are stored, via the `IAuthStateProvider` interface. This lets you choose — or swap — your backing store at any time without changing any other code.

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

---

## Security Considerations

- Credentials contain **private keys** — protect the backing store with appropriate access controls.
- For the `FileAuthStateProvider`, restrict the file's permissions to the service account only (`chmod 600` on Linux).
- For database providers, store credentials in a separate, encrypted column or use a secrets-manager service.
- Never commit credentials files to source control — add them to `.gitignore`.
