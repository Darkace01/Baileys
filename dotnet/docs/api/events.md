# Events API Reference

Event payloads are plain data-carrier classes in `Baileys.Types`. They represent the information emitted by the WhatsApp WebSocket when something happens — a new message, connection change, group update, etc.

These mirror the TypeScript `BaileysEventMap` interface from `Types/Events.ts`.

---

## Namespace

```csharp
using Baileys.Types;
```

---

## Event Payload Types

### `ConnectionUpdateEvent`

Emitted when the connection state changes.  
**TypeScript equivalent:** `connection.update`

| Property | Type | Description |
|---|---|---|
| `Connection` | `WaConnectionState?` | New connection state (`Open`, `Connecting`, `Close`) |
| `LastDisconnect` | `LastDisconnectInfo?` | Details of the last disconnect (error + timestamp) |
| `IsNewLogin` | `bool?` | `true` on the very first registration |
| `Qr` | `string?` | QR code string to display for scanning |
| `ReceivedPendingNotifications` | `bool?` | `true` once all offline messages have been replayed |
| `IsOnline` | `bool?` | Whether the account appears online |

```csharp
void OnConnectionUpdate(ConnectionUpdateEvent e)
{
    if (e.Connection == WaConnectionState.Open)
        Console.WriteLine("Connected!");

    if (e.Qr is not null)
        Console.WriteLine($"Scan QR: {e.Qr}");

    if (e.LastDisconnect?.Error is not null)
        Console.WriteLine($"Disconnected: {e.LastDisconnect.Error.Message}");
}
```

---

### `MessagingHistorySetEvent`

Delivered when a history-sync chunk arrives (app startup, linked device).  
**TypeScript equivalent:** `messaging-history.set`

| Property | Type | Description |
|---|---|---|
| `Chats` | `IReadOnlyList<Chat>` | Chats in this history chunk |
| `Contacts` | `IReadOnlyList<Contact>` | Contacts in this chunk |
| `Messages` | `IReadOnlyList<MinimalMessage>` | Messages in this chunk |
| `LidPnMappings` | `IReadOnlyList<LidMapping>?` | LID ↔ phone-number mappings |
| `IsLatest` | `bool?` | Whether this is the most recent chunk |
| `Progress` | `int?` | Sync progress percentage (0–100) |
| `SyncType` | `int?` | Type of history sync |
| `PeerDataRequestSessionId` | `string?` | Peer data request session |

---

### `MessagesUpsertEvent`

Fired when new messages arrive or history messages are appended.  
**TypeScript equivalent:** `messages.upsert`

| Property | Type | Description |
|---|---|---|
| `Messages` | `IReadOnlyList<MinimalMessage>` | The new or updated messages |
| `Type` | `MessageUpsertType` | `Notify` (real-time) or `Append` (history) |
| `RequestId` | `string?` | Associated request ID |

```csharp
void OnMessagesUpsert(MessagesUpsertEvent e)
{
    foreach (var msg in e.Messages)
    {
        Console.WriteLine($"[{e.Type}] Message {msg.Key.Id} in {msg.Key.RemoteJid}");
    }
}
```

---

### `PresenceUpdateEvent`

Fired when a contact's presence changes.  
**TypeScript equivalent:** `presence.update`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | JID of the chat |
| `Presences` | `Dictionary<string, PresenceData>` | Participant JID → presence data |

```csharp
void OnPresenceUpdate(PresenceUpdateEvent e)
{
    foreach (var (participantJid, presence) in e.Presences)
    {
        Console.WriteLine($"{participantJid}: {presence.LastKnownPresence}");
    }
}
```

---

### `GroupParticipantsUpdateEvent`

Fired when a group's participant list changes.  
**TypeScript equivalent:** `group-participants.update`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Group JID |
| `Author` | `string` | JID of the user who made the change |
| `AuthorPn` | `string?` | Phone-number JID of the author |
| `Participants` | `IReadOnlyList<GroupParticipant>` | Affected participants |
| `Action` | `ParticipantAction` | `Add`, `Remove`, `Promote`, or `Demote` |

```csharp
void OnGroupParticipantsUpdate(GroupParticipantsUpdateEvent e)
{
    Console.WriteLine($"Group {e.Id}: {e.Action} by {e.Author}");
    foreach (var p in e.Participants)
        Console.WriteLine($"  → {p.Id}");
}
```

---

### `GroupJoinRequestEvent`

Fired when someone requests to join a group.  
**TypeScript equivalent:** `group.join-request`

| Property | Type | Description |
|---|---|---|
| `Id` | `string` | Group JID |
| `Author` | `string` | Admin JID who approved/rejected |
| `Participant` | `string` | JID of the person requesting to join |
| `Action` | `RequestJoinAction` | `Approve`, `Reject`, or `Add` |
| `Method` | `RequestJoinMethod?` | How the request was created |

---

### `BlocklistUpdateEvent`

Fired when the blocklist changes.  
**TypeScript equivalent:** `blocklist.set` / `blocklist.update`

| Property | Type | Description |
|---|---|---|
| `Blocklist` | `IReadOnlyList<string>` | JIDs in the blocklist |
| `Type` | `string?` | `"add"` or `"remove"` — `null` for full set |

---

### `MessagesDeleteEvent`

Fired when messages are deleted.  
**TypeScript equivalent:** `messages.delete`

| Property | Type | Description |
|---|---|---|
| `Keys` | `IReadOnlyList<WaMessageKey>?` | Specific messages deleted |
| `Jid` | `string?` | Chat to delete all messages from |
| `All` | `bool` | `true` when all messages in `Jid` are deleted |

---

### `WaMessageUpdate`

A single message status update (read receipts, timestamps, etc.).

```csharp
public sealed class WaMessageUpdate
{
    public required WaMessageKey Key { get; init; }
    public required MessageUpdatePayload Update { get; init; }
}

public sealed class MessageUpdatePayload
{
    public int? Status { get; init; }
    public long? MessageTimestamp { get; init; }
}
```

---

### `MessageMediaUpdate`

Fired when media ciphertext is updated.

```csharp
public sealed class MessageMediaUpdate
{
    public required WaMessageKey Key { get; init; }
    public MessageMediaPayload? Media { get; init; }
    public Exception? Error { get; init; }
}

public sealed class MessageMediaPayload
{
    public required byte[] Ciphertext { get; init; }
    public required byte[] Iv { get; init; }
}
```

---

### `MessageReaction`

A reaction on a message.

```csharp
public sealed class MessageReaction
{
    public required WaMessageKey Key { get; init; }
    public string? ReactionText { get; init; }
    public long? Timestamp { get; init; }
}
```

---

### `LabelAssociationEvent`

Fired when a label is associated with or removed from a chat or message.

```csharp
public sealed class LabelAssociationEvent
{
    public required object Association { get; init; }  // ChatLabelAssociation or MessageLabelAssociation
    public required string Type { get; init; }          // "add" or "remove"
}
```

---

### Newsletter Events

#### `NewsletterReactionEvent`

```csharp
public sealed class NewsletterReactionEvent
{
    public required string Id { get; init; }
    public required string ServerId { get; init; }
    public string? ReactionCode { get; init; }
    public int? Count { get; init; }
    public bool Removed { get; init; }
}
```

#### `NewsletterViewEvent`

```csharp
public sealed class NewsletterViewEvent
{
    public required string Id { get; init; }
    public required string ServerId { get; init; }
    public int Count { get; init; }
}
```

#### `NewsletterParticipantsUpdateEvent`

```csharp
public sealed class NewsletterParticipantsUpdateEvent
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public required string User { get; init; }
    public required string NewRole { get; init; }
    public required string Action { get; init; }
}
```

---

### Other Events

#### `ChatLockEvent`

```csharp
public sealed class ChatLockEvent
{
    public required string Id { get; init; }
    public bool Locked { get; init; }
}
```

#### `LidMappingUpdateEvent`

```csharp
public sealed class LidMappingUpdateEvent
{
    public required string PhoneNumber { get; init; }
    public required string Lid { get; init; }
}
```

#### `GroupMemberTagUpdateEvent`

```csharp
public sealed class GroupMemberTagUpdateEvent
{
    public required string GroupId { get; init; }
    public required string Participant { get; init; }
    public string? ParticipantAlt { get; init; }
    public required string LabelValue { get; init; }
    public long? MessageTimestamp { get; init; }
}
```

---

## `MessageUserReceiptUpdate`

Per-user receipt update for a message.

```csharp
public sealed class MessageUserReceiptUpdate
{
    public required WaMessageKey Key { get; init; }
    public required UserReceipt Receipt { get; init; }
}

public sealed class UserReceipt
{
    public required string UserJid { get; init; }
    public long? ReadTimestamp { get; init; }
    public long? DeliveryTimestamp { get; init; }
    public long? PlayedTimestamp { get; init; }
    public string[]? PendingDeviceJids { get; init; }
    public string[]? DeliveredDeviceJids { get; init; }
}
```
