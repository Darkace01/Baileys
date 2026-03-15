# Types API Reference

All WhatsApp domain types are in the `Baileys.Types` namespace. They mirror the TypeScript type definitions in `src/Types/` and carry no external dependencies.

---

## Namespace

```csharp
using Baileys.Types;
```

---

## Authentication & Credentials (`Auth.cs`, `SignalKeyStore.cs`)

### `AuthenticationState`

Bundles credentials and the Signal key store into one object — the .NET equivalent of the TypeScript `AuthenticationState` type. Returned by `IAuthStateProvider.LoadAuthStateAsync()`.

```csharp
public sealed class AuthenticationState
{
    public required AuthenticationCreds Creds { get; init; }
    public required ISignalKeyStore     Keys  { get; init; }
}
```

### `AuthenticationCreds`

Full credential set for a WhatsApp Web session. See [Auth Utilities](auth-utils.md) for how to create and manage credentials.

| Property | Type | Description |
|---|---|---|
| `NoiseKey` | `KeyPair` | Curve25519 key-pair for the Noise_XX handshake |
| `PairingEphemeralKeyPair` | `KeyPair` | Ephemeral key used during pairing |
| `AdvSecretKey` | `string` | Base64-encoded 32-byte ADV secret |
| `SignedIdentityKey` | `KeyPair` | Long-term Signal identity key-pair |
| `SignedPreKey` | `SignedKeyPair` | Current signed pre-key |
| `RegistrationId` | `int` | Signal registration ID |
| `NextPreKeyId` | `int` | Next pre-key ID to upload |
| `FirstUnuploadedPreKeyId` | `int` | Lowest not-yet-uploaded pre-key ID |
| `AccountSyncCounter` | `int` | App-state patch counter |
| `Registered` | `bool` | `true` after successful pairing |
| `PairingCode` | `string?` | Code-based pairing code |
| `LastPropHash` | `string?` | Hash of last server-sent props |
| `RoutingInfo` | `byte[]?` | Server routing bytes |
| `Platform` | `string?` | Platform string sent on connect |
| `AccountSettings` | `AccountSettings` | Account-level settings |

### `KeyPair`

```csharp
public sealed record KeyPair(byte[] Public, byte[] Private);
```

### `SignedKeyPair`

```csharp
public sealed record SignedKeyPair(
    KeyPair KeyPair,
    byte[]  Signature,
    int     KeyId,
    long?   TimestampSeconds = null);
```

### `AccountSettings`

```csharp
public sealed class AccountSettings
{
    public bool UnarchiveChats { get; set; }
    public int? DefaultEphemeralExpiration { get; set; }
    public long? DefaultEphemeralSettingTimestamp { get; set; }
}
```

### `DisconnectReason` (enum)

| Value | HTTP-like code | Meaning |
|---|---|---|
| `ConnectionClosed` | 428 | Server closed the WebSocket |
| `ConnectionLost` | 408 | Network loss / timeout |
| `ConnectionReplaced` | 440 | Session opened on another device |
| `TimedOut` | 408 | Request timed out |
| `LoggedOut` | 401 | User logged out |
| `BadSession` | 500 | Invalid session state |
| `RestartRequired` | 515 | Server requested a restart |
| `MultideviceMismatch` | 411 | Multi-device sync error |
| `Forbidden` | 403 | Account banned / forbidden |
| `UnavailableService` | 503 | Server unavailable |

---

## JID Types (`Jid.cs`)

### `FullJid`

```csharp
public sealed record FullJid(
    string?   User,
    JidServer Server,
    int?      Device = null,
    int?      Agent  = null);
```

### `JidServer` (enum)

See [JID Utilities](jid-utils.md#jidserver-enum) for all values and their string representations.

---

## Binary Node (`BinaryNode.cs`)

```csharp
public sealed class BinaryNode
{
    public required string Tag { get; init; }
    public Dictionary<string, string> Attrs { get; init; } = new();
    public object? Content { get; init; }  // null | List<BinaryNode> | byte[] | string
}
```

---

## Contact (`Contact.cs`)

```csharp
public sealed class Contact
{
    public required string Id { get; init; }   // JID
    public string? Name { get; init; }          // display name
    public string? Notify { get; init; }        // push name
    public string? VerifiedName { get; init; }  // business verified name
    public string? ImgUrl { get; init; }        // profile picture URL
    public string? Status { get; init; }        // "About" text
}
```

---

## Group Metadata (`GroupMetadata.cs`)

### `GroupMetadata`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Group JID |
| `Owner` | `string?` | Owner JID |
| `Subject` | `string` | Group name |
| `SubjectOwner` | `string?` | Who set the subject |
| `SubjectTime` | `long?` | When the subject was last set |
| `Creation` | `long?` | Group creation timestamp |
| `Desc` | `string?` | Group description |
| `DescOwner` | `string?` | Who set the description |
| `DescId` | `string?` | Description message ID |
| `Restrict` | `bool?` | Only admins can send messages |
| `Announce` | `bool?` | Only admins can edit info |
| `IsCommunity` | `bool?` | Whether this is a community group |
| `IsCommunityAnnounce` | `bool?` | Community announcement channel |
| `Participants` | `IReadOnlyList<GroupParticipant>` | Member list |
| `EphemeralDuration` | `int?` | Disappearing messages duration |
| `InviteCode` | `string?` | Invite link code |

### `GroupParticipant`

```csharp
public sealed class GroupParticipant
{
    public required string Id { get; init; }    // JID
    public bool IsAdmin { get; init; }
    public bool IsSuperAdmin { get; init; }
    public string? Error { get; init; }          // set on failed invite
}
```

### `ParticipantAction` (enum)

`Add`, `Remove`, `Promote`, `Demote`

### `RequestJoinAction` (enum)

`Approve`, `Reject`, `Add`

### `RequestJoinMethod` (enum)

`InviteLink`, `LinkedGroupJoin`, `NonAdminAdd`, `InviteV4Link`

---

## Connection State (`State.cs`)

### `ConnectionState`

| Property | Type | Description |
|---|---|---|
| `Connection` | `WaConnectionState` | Current socket state |
| `LastDisconnect` | `LastDisconnectInfo?` | Details of last disconnect |
| `IsNewLogin` | `bool?` | First-time registration |
| `Qr` | `string?` | QR code to scan |
| `ReceivedPendingNotifications` | `bool?` | Offline queue processed |
| `IsOnline` | `bool?` | Whether appearing online |

### `WaConnectionState` (enum)

`Open`, `Connecting`, `Close`

### `WaPresence` (enum)

`Unavailable`, `Available`, `Composing`, `Recording`, `Paused`

### `PresenceData`

```csharp
public sealed class PresenceData
{
    public WaPresence LastKnownPresence { get; init; }
    public long? LastSeen { get; init; }
}
```

### Privacy Enums

| Enum | Values |
|---|---|
| `WaPrivacyValue` | `All`, `Contacts`, `ContactBlacklist`, `None` |
| `WaPrivacyOnlineValue` | `All`, `MatchLastSeen` |
| `WaPrivacyGroupAddValue` | `All`, `Contacts`, `ContactBlacklist` |
| `WaReadReceiptsValue` | `All`, `None` |
| `WaPrivacyCallValue` | `All`, `Known` |
| `WaPrivacyMessagesValue` | `All`, `Contacts` |

### `WaPatchName` (enum)

`CriticalBlock`, `CriticalUnblockLow`, `RegularHigh`, `RegularLow`, `Regular`

### `SyncState` (enum)

`Connecting`, `AwaitingInitialSync`, `Syncing`, `Online`

---

## Chat (`Chat.cs`)

### `Chat`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Chat JID |
| `UnreadCount` | `int?` | Number of unread messages |
| `Archive` | `bool?` | Archived status |
| `Pinned` | `bool?` | Pinned status |
| `Mute` | `long?` | Mute expiry (ms); null = unmuted |
| `EphemeralExpiration` | `int?` | Disappearing messages timer (seconds) |
| `EphemeralSettingTimestamp` | `long?` | When timer was last configured |
| `MarkedAsUnread` | `bool?` | Manually marked unread |
| `LastMessageRecvTimestamp` | `long?` | Last received message time |
| `Name` | `string?` | Display name |
| `ReadOnly` | `bool?` | Whether chat is read-only |
| `Locked` | `bool?` | Whether chat is locked |

### `ChatModification` Hierarchy

```
ChatModification (abstract)
├── ArchiveChatModification     — { Archive, LastMessages }
├── PinChatModification         — { Pin }
├── MuteChatModification        — { Mute? }
├── MarkReadChatModification    — { MarkRead, LastMessages }
├── DeleteChatModification      — { LastMessages }
├── ClearChatModification       — { Clear, LastMessages }
├── AddLabelChatModification    — { Label }
├── AddChatLabelModification    — { LabelAssoc }
└── RemoveChatLabelModification — { LabelAssoc }
```

---

## Message (`Message.cs`)

### `WaMessageKey`

Uniquely identifies a WhatsApp message.

| Property | Type | Description |
|---|---|---|
| `RemoteJid` | `string?` | Chat JID |
| `FromMe` | `bool?` | Sent by authenticated user |
| `Id` | `string?` | Unique message ID |
| `Participant` | `string?` | Sender JID (groups only) |
| `ServerId` | `string?` | Server-assigned ID |

### `MinimalMessage`

```csharp
public sealed class MinimalMessage
{
    public required WaMessageKey Key { get; init; }
    public long? MessageTimestamp { get; init; }
}
```

### `MediaType` (enum)

`Audio`, `Document`, `Gif`, `Image`, `Ppic`, `Product`, `Ptt`, `Sticker`, `Video`, `ThumbnailDocument`, `ThumbnailImage`, `ThumbnailVideo`, `ThumbnailLink`, `MdMsgHist`, `MdAppState`, `ProductCatalogImage`, `PaymentBgImage`, `Ptv`, `BizCoverPhoto`

### `MessageUpsertType` (enum)

`Notify` (real-time), `Append` (history sync)

---

## Call (`Call.cs`)

### `WaCallEvent`

| Property | Type | Description |
|---|---|---|
| `ChatId` | `string` | Chat JID for the call |
| `Id` | `string` | Call ID |
| `Offline` | `bool` | Whether received while offline |
| `Participants` | `IReadOnlyList<string>` | Participant JIDs |
| `Timestamp` | `DateTimeOffset` | Call event time |
| `IsGroup` | `bool` | Group call |
| `IsVideo` | `bool` | Video call |
| `From` | `string` | Caller JID |

### `WaCallUpdateType` (enum)

`Offer`, `OfferMsg`, `PreAccept`, `Accept`, `Reject`, `Timeout`, `Terminate`, `TransportChange`

---

## Label (`Label.cs`)

### `Label`

```csharp
public sealed class Label
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string? PredefinedId { get; init; }
    public LabelColor Color { get; init; }
    public int? DeletedAt { get; init; }
    public bool IsDeleted { get; init; }
}
```

### `LabelColor` (enum)

`Color0`–`Color19` (maps to WhatsApp label color palette)

---

## Newsletter (`Newsletter.cs`)

### `NewsletterMetadata`

Key properties for a WhatsApp Channel (newsletter):

```csharp
public sealed class NewsletterMetadata
{
    public required string Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? InviteCode { get; init; }
    public string? PictureUrl { get; init; }
    public long? SubscribersCount { get; init; }
    public NewsletterViewRole ViewRole { get; init; }
    public bool? Verified { get; init; }
}
```

### `NewsletterViewRole` (enum)

`Guest`, `Subscriber`, `Admin`, `Owner`

---

## Business (`Business.cs`)

### `WaBusinessProfile`

```csharp
public sealed class WaBusinessProfile
{
    public required string Wid { get; init; }
    public required string Description { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? Website { get; init; }
    public string? Category { get; init; }
    public IReadOnlyList<WaBusinessHours>? BusinessHours { get; init; }
}
```

---

## Product (`Product.cs`)

### `Product`

```csharp
public sealed class Product
{
    public string? ProductId { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public string? CurrencyCode { get; init; }
    public long? PriceAmount1000 { get; init; }
    public bool? IsHidden { get; init; }
    public string? ImageUrls { get; init; }
    public string? ReviewStatus { get; init; }
    public string? Availability { get; init; }
    public string? RetailerId { get; init; }
    public string? Url { get; init; }
}
```

---

## Signal Protocol Helpers

### `ProtocolAddress`

```csharp
public sealed record ProtocolAddress(string Name, int DeviceId);
```

### `SignalIdentity`

```csharp
public sealed record SignalIdentity(ProtocolAddress Identifier, byte[] IdentifierKey);
```

### `LidMapping`

```csharp
public sealed record LidMapping(string PhoneNumber, string Lid);
```

---

## Signal Key Store (`SignalKeyStore.cs`, `SignalKeyStoreExtensions.cs`)

### `ISignalKeyStore`

Generic key-value store for all Signal-protocol cryptographic material.

```csharp
public interface ISignalKeyStore
{
    Task<IReadOnlyDictionary<string, byte[]?>> GetAsync(
        string type, IReadOnlyList<string> ids,
        CancellationToken cancellationToken = default);

    Task SetAsync(
        string type, IReadOnlyDictionary<string, byte[]?> values,
        CancellationToken cancellationToken = default);

    Task ClearAsync(CancellationToken cancellationToken = default);
}
```

### `ISignalKeyStoreWithTransaction`

Extends `ISignalKeyStore` with atomic transaction support.

```csharp
public interface ISignalKeyStoreWithTransaction : ISignalKeyStore
{
    bool IsInTransaction { get; }
    Task<T> TransactionAsync<T>(Func<Task<T>> action, string key = "");
}
```

### `SignalDataTypes`

Constants for Signal data type names, mirroring the TypeScript `SignalDataTypeMap` keys.

| Constant | Value |
|---|---|
| `SignalDataTypes.PreKey` | `"pre-key"` |
| `SignalDataTypes.Session` | `"session"` |
| `SignalDataTypes.SenderKey` | `"sender-key"` |
| `SignalDataTypes.SenderKeyMemory` | `"sender-key-memory"` |
| `SignalDataTypes.AppStateSyncKey` | `"app-state-sync-key"` |
| `SignalDataTypes.AppStateSyncVersion` | `"app-state-sync-version"` |
| `SignalDataTypes.LidMapping` | `"lid-mapping"` |
| `SignalDataTypes.DeviceList` | `"device-list"` |
| `SignalDataTypes.TcToken` | `"tctoken"` |
| `SignalDataTypes.IdentityKey` | `"identity-key"` |

### `TcToken`

TC-token data used in device authentication.

```csharp
public sealed class TcToken
{
    public required byte[] Token     { get; init; }
    public          string? Timestamp { get; init; }
}
```

### `SignalKeyStoreExtensions`

Typed extension methods on `ISignalKeyStore`. Available helpers:

| Method | Type parameter |
|---|---|
| `GetPreKeysAsync` / `SetPreKeysAsync` | `KeyPair?` |
| `GetSessionsAsync` / `SetSessionsAsync` | `byte[]?` |
| `GetSenderKeysAsync` / `SetSenderKeysAsync` | `byte[]?` |
| `GetSenderKeyMemoriesAsync` / `SetSenderKeyMemoriesAsync` | `Dictionary<string, bool>?` |
| `GetAppStateSyncKeysAsync` / `SetAppStateSyncKeysAsync` | `byte[]?` |
| `GetAppStateSyncVersionsAsync` / `SetAppStateSyncVersionsAsync` | `LtHashState?` |
| `GetLidMappingsAsync` / `SetLidMappingsAsync` | `string?` |
| `GetDeviceListsAsync` / `SetDeviceListsAsync` | `IReadOnlyList<string>?` |
| `GetTcTokensAsync` / `SetTcTokensAsync` | `TcToken?` |
| `GetIdentityKeysAsync` / `SetIdentityKeysAsync` | `byte[]?` |
