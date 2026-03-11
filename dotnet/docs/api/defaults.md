# Defaults & Constants API Reference

`Baileys.Defaults` exposes all WhatsApp protocol constants and browser presets used by the library.

---

## Namespace

```csharp
using Baileys.Defaults;
```

---

## `BaileysDefaults` — Protocol Constants

### Version

```csharp
// Current WA Web version triplet [major, minor, patch]
int[] version = BaileysDefaults.BaileysVersion;   // [2, 3000, 1033846690]
```

### URLs

| Constant | Value | Description |
|---|---|---|
| `WaWebSocketUrl` | `"wss://web.whatsapp.com/ws/chat"` | Primary WebSocket endpoint |
| `DefaultOrigin` | `"https://web.whatsapp.com"` | HTTP `Origin` header value |
| `CallVideoPrefix` | `"https://call.whatsapp.com/video/"` | Video call URL prefix |
| `CallAudioPrefix` | `"https://call.whatsapp.com/voice/"` | Voice call URL prefix |

### Protocol Prefixes

| Constant | Value | Description |
|---|---|---|
| `DefCallbackPrefix` | `"CB:"` | Callback message prefix |
| `DefTagPrefix` | `"TAG:"` | Tag message prefix |
| `PhoneConnectionCb` | `"CB:Pong"` | Keep-alive pong callback |

### Signature Prefixes (byte arrays)

```csharp
byte[] accSig     = BaileysDefaults.WaAdvAccountSigPrefix;       // [6, 0]
byte[] devSig     = BaileysDefaults.WaAdvDeviceSigPrefix;        // [6, 1]
byte[] hostedAcc  = BaileysDefaults.WaAdvHostedAccountSigPrefix; // [6, 5]
byte[] hostedDev  = BaileysDefaults.WaAdvHostedDeviceSigPrefix;  // [6, 6]
```

### Timing Defaults

| Constant | Value | Description |
|---|---|---|
| `WaDefaultEphemeral` | `604800` (7 days) | Default ephemeral message lifetime (seconds) |
| `StatusExpirySeconds` | `86400` (24 hours) | Status message expiry |
| `PlaceholderMaxAgeSeconds` | `1209600` (14 days) | Max age for placeholder resend |
| `ConnectTimeoutMs` | `20000` | WebSocket connect timeout |
| `KeepAliveIntervalMs` | `30000` | Keep-alive ping interval |
| `DefaultQueryTimeoutMs` | `60000` | Default query timeout |
| `RetryRequestDelayMs` | `250` | Base delay between retries |
| `MaxMsgRetryCount` | `5` | Max message retry attempts |
| `DelayBetweenTriesMs` | `3000` | Delay between reconnect attempts |

### Noise Protocol

```csharp
string noiseMode    = BaileysDefaults.NoiseMode;        // "Noise_XX_25519_AESGCM_SHA256\0\0\0\0"
int dictVersion     = BaileysDefaults.DictVersion;      // 3
byte[] keyBundleType = BaileysDefaults.KeyBundleType;   // [5]

// WA header: "WA" + 0x06 + DictVersion
byte[] noiseHeader  = BaileysDefaults.NoiseWaHeader;    // ['W', 'A', 6, 3]
```

### Certificate Details

```csharp
int serial    = BaileysDefaults.WaCertSerial;   // 0
string issuer = BaileysDefaults.WaCertIssuer;   // "WhatsAppLongTerm1"
byte[] pubKey = BaileysDefaults.WaCertPublicKey; // 32-byte public key
```

### Pre-key Management

| Constant | Value | Description |
|---|---|---|
| `MinPreKeyCount` | `5` | Minimum pre-keys before uploading more |
| `InitialPreKeyCount` | `812` | Pre-keys generated on first connect |

### Upload / Download

| Constant | Value | Description |
|---|---|---|
| `UploadTimeoutMs` | `30000` | Media upload timeout |
| `MinUploadIntervalMs` | `5000` | Minimum interval between uploads |

### Cache TTLs (seconds)

| Constant | Value | Description |
|---|---|---|
| `CacheTtlSignalStore` | `300` | Signal store cache TTL |
| `CacheTtlMsgRetry` | `3600` | Message retry cache TTL |
| `CacheTtlCallOffer` | `300` | Call offer cache TTL |
| `CacheTtlUserDevices` | `300` | User device list cache TTL |

### HTTP Status Codes

```csharp
// 401 (logged out), 403 (banned), 419 (session expired)
IReadOnlySet<int> unauthorized = BaileysDefaults.UnauthorizedCodes;
```

### TimeSpan Helpers

```csharp
TimeSpan minute = BaileysDefaults.TimeMinute;  // 1 minute
TimeSpan hour   = BaileysDefaults.TimeHour;    // 1 hour
TimeSpan day    = BaileysDefaults.TimeDay;     // 1 day
TimeSpan week   = BaileysDefaults.TimeWeek;    // 7 days
```

---

## `Browsers` — Browser Description Presets

Browser descriptions are `string[3]` arrays sent to the WhatsApp servers as `[platform, browser, version]`. They identify the connecting client.

### Factory Methods

| Method | Returns |
|---|---|
| `Browsers.MacOs(browser?)` | `["Mac OS", browser, "14.4.1"]` |
| `Browsers.Ubuntu(browser?)` | `["Ubuntu", browser, "22.04.4"]` |
| `Browsers.Windows(browser?)` | `["Windows", browser, "10.0.22631"]` |
| `Browsers.Baileys(browser?)` | `["Baileys", browser, "6.5.0"]` |
| `Browsers.Appropriate(browser?)` | OS-detected description |

The `browser` parameter defaults to `"Chrome"` for all except `Browsers.Baileys` (defaults to `"Desktop"`).

```csharp
string[] mac     = Browsers.MacOs();          // ["Mac OS", "Chrome", "14.4.1"]
string[] ubuntu  = Browsers.Ubuntu("Firefox"); // ["Ubuntu", "Firefox", "22.04.4"]
string[] current = Browsers.Appropriate();    // Auto-detected from RuntimeInformation
```

### Media HKDF Key Mapping

```csharp
// Maps media type → HKDF info label for key derivation
IReadOnlyDictionary<string, string> hkdf = Browsers.MediaHkdfKeyMapping;

string label = hkdf["image"];    // "Image"
string label2 = hkdf["video"];   // "Video"
string label3 = hkdf["audio"];   // "Audio"
```

Full mapping:

| Media Type | HKDF Label |
|---|---|
| `audio` | `"Audio"` |
| `document` | `"Document"` |
| `gif` | `"Video"` |
| `image` | `"Image"` |
| `ptt` | `"Audio"` |
| `sticker` | `"Image"` |
| `video` | `"Video"` |
| `thumbnail-document` | `"Document Thumbnail"` |
| `thumbnail-image` | `"Image Thumbnail"` |
| `thumbnail-video` | `"Video Thumbnail"` |
| `thumbnail-link` | `"Link Thumbnail"` |
| `md-msg-hist` | `"History"` |
| `md-app-state` | `"App State"` |
| `payment-bg-image` | `"Payment Background"` |
| `ptv` | `"Video"` |
| `biz-cover-photo` | `"Image"` |

### Media Path Mapping

```csharp
// Maps media type → WhatsApp upload/download URL path segment
IReadOnlyDictionary<string, string> paths = Browsers.MediaPathMap;

string path = paths["image"];    // "/mms/image"
string path2 = paths["video"];   // "/mms/video"
```

Full mapping:

| Media Type | URL Path |
|---|---|
| `image` | `/mms/image` |
| `video` | `/mms/video` |
| `document` | `/mms/document` |
| `audio` | `/mms/audio` |
| `sticker` | `/mms/image` |
| `thumbnail-link` | `/mms/image` |
| `product-catalog-image` | `/product/image` |
| `md-app-state` | `` (empty) |
| `md-msg-hist` | `/mms/md-app-state` |
| `biz-cover-photo` | `/pps/biz-cover-photo` |
