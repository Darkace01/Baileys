# Dependency Injection

The `Baileys` package integrates with the **Microsoft.Extensions.DependencyInjection** stack used by ASP.NET Core, Worker Services, and any host built with `Microsoft.Extensions.Hosting`.

---

## Registered Services

| Service | Implementation | Lifetime |
|---|---|---|
| `IOptions<BaileysOptions>` | Configured options | Singleton |
| `IAuthStateProvider` | Chosen backend | Singleton |

---

## `BaileysOptions` — Configuration Class

`BaileysOptions` holds all runtime configuration for a Baileys session.

```csharp
namespace Baileys.Options;

public sealed class BaileysOptions
{
    // Default section name in appsettings.json
    public const string SectionName = "Baileys";

    // WhatsApp phone number / chat ID (JID user part), e.g. "15551234567"
    public string PhoneNumber { get; set; } = string.Empty;

    // Optional JID server suffix override (default: s.whatsapp.net)
    public string? JidServer { get; set; }

    // Number of pre-keys to generate on first connect (default: 31)
    public int InitialPreKeyCount { get; set; } = 31;

    // Milliseconds to wait before retrying after a dropped connection (default: 3000)
    public int RetryRequestDelayMs { get; set; } = 3_000;

    // Mirror AccountSettings.unarchiveChats (default: false)
    public bool UnarchiveChats { get; set; }
}
```

---

## Registration Methods

### `AddBaileys()` — In-Memory Session (Default)

Registers an `InMemoryAuthStateProvider`. Session state lives in process memory and is reset when the process restarts. Ideal for development, testing, or single-use scripts.

```csharp
// Option A: configure programmatically
builder.Services.AddBaileys(o =>
{
    o.PhoneNumber       = "15551234567";
    o.UnarchiveChats    = false;
    o.RetryRequestDelayMs = 5_000;
});

// Option B: no configuration (use defaults)
builder.Services.AddBaileys();
```

---

### `AddBaileysWithFileStorage()` — File-Based Persistence

Registers a `FileAuthStateProvider` that reads/writes credentials as JSON.  
The directory must already exist; the file is created on first save.

```csharp
// Relative path (resolves to current working directory)
builder.Services.AddBaileysWithFileStorage("baileys_auth.json", o =>
{
    o.PhoneNumber = "15551234567";
});

// Absolute path (recommended for production)
builder.Services.AddBaileysWithFileStorage(
    filePath:  "/var/data/whatsapp/creds.json",
    configure: o => o.PhoneNumber = "15551234567");
```

---

### `AddBaileysWithProvider<T>()` — Custom Provider

Register your own `IAuthStateProvider` implementation (database, Redis, Azure Blob Storage, etc.):

```csharp
builder.Services.AddBaileysWithProvider<MyDatabaseAuthStateProvider>(o =>
{
    o.PhoneNumber = "15551234567";
});
```

See [Session Storage → Custom Provider](session-storage.md#custom-provider) for a complete example.

---

## Binding Options from `appsettings.json`

You can keep your phone number (and other settings) in `appsettings.json` and bind them at startup — no need to hard-code credentials in source:

### `appsettings.json`

```json
{
  "Baileys": {
    "PhoneNumber": "15551234567",
    "UnarchiveChats": false,
    "InitialPreKeyCount": 31,
    "RetryRequestDelayMs": 3000
  }
}
```

### `Program.cs`

```csharp
// Bind options from configuration
builder.Services.Configure<BaileysOptions>(
    builder.Configuration.GetSection(BaileysOptions.SectionName));

// Then register with any backend (options are already bound)
builder.Services.AddBaileys();
// or:
builder.Services.AddBaileysWithFileStorage("/var/data/creds.json");
```

---

## Injecting Services

Once registered, inject `IAuthStateProvider` and `IOptions<BaileysOptions>` via constructor injection:

### In a Controller

```csharp
using Baileys.Session;
using Baileys.Options;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;

[ApiController, Route("api/[controller]")]
public class WhatsAppController(
    IAuthStateProvider session,
    IOptions<BaileysOptions> options) : ControllerBase
{
    [HttpGet("status")]
    public async Task<IActionResult> GetStatus(CancellationToken ct)
    {
        var creds = await session.LoadCredsAsync(ct);
        return Ok(new
        {
            phone      = options.Value.PhoneNumber,
            registered = creds.Registered
        });
    }

    [HttpPost("reset")]
    public async Task<IActionResult> Reset(CancellationToken ct)
    {
        await session.ClearAsync(ct);
        return NoContent();
    }
}
```

### In a Background Service

```csharp
using Baileys.Session;
using Baileys.Options;
using Baileys.Utils;
using Microsoft.Extensions.Options;

public class WhatsAppWorker(
    IAuthStateProvider session,
    IOptions<BaileysOptions> options,
    ILogger<WhatsAppWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var creds = await session.LoadCredsAsync(ct);

        if (!creds.Registered)
        {
            logger.LogInformation("New session for {Phone}", options.Value.PhoneNumber);
            // … perform QR / pairing code flow …
            creds.Registered = true;
            await session.SaveCredsAsync(creds, ct);
        }

        logger.LogInformation("Session active — registration ID {Id}", creds.RegistrationId);
    }
}
```

---

## Environment-Specific Configuration

Use the standard ASP.NET Core `appsettings.{Environment}.json` pattern:

```
appsettings.json            ← shared defaults
appsettings.Development.json ← dev phone number / file path
appsettings.Production.json  ← prod phone number / secrets via environment variables
```

Or supply values via environment variables (useful in Docker / Kubernetes):

```bash
# Environment variable overrides (double-underscore for hierarchy)
BAILEYS__PHONENUMBER=15551234567
BAILEYS__RETRYREQUESTEDALAYMS=5000
```

---

## Full `Program.cs` Example (ASP.NET Core)

```csharp
using Baileys.Extensions;
using Baileys.Options;

var builder = WebApplication.CreateBuilder(args);

// Option 1: in-memory session, options from appsettings.json
builder.Services.Configure<BaileysOptions>(
    builder.Configuration.GetSection(BaileysOptions.SectionName));
builder.Services.AddBaileys();

// Option 2: file-based session, programmatic options
// builder.Services.AddBaileysWithFileStorage(
//     filePath:  builder.Configuration["Baileys:CredsFile"] ?? "creds.json",
//     configure: o => o.PhoneNumber = builder.Configuration["Baileys:PhoneNumber"]!);

builder.Services.AddControllers();
var app = builder.Build();
app.MapControllers();
app.Run();
```
