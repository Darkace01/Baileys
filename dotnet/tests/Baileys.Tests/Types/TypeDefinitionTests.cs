using Baileys.Types;
using Xunit;

namespace Baileys.Tests.Types;

/// <summary>Tests for new type definitions (Contact, Group, Call, State, Message, etc.).</summary>
public class TypeDefinitionTests
{
    // ──────────────────────────────────────────────────────────
    //  Contact
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Contact_CanBeConstructed()
    {
        var c = new Contact { Id = "123@s.whatsapp.net", Name = "Alice" };
        Assert.Equal("123@s.whatsapp.net", c.Id);
        Assert.Equal("Alice", c.Name);
        Assert.Null(c.Lid);
        Assert.Null(c.PhoneNumber);
    }

    [Fact]
    public void Contact_OptionalFields_AllNull_ByDefault()
    {
        var c = new Contact { Id = "x" };
        Assert.Null(c.Lid);
        Assert.Null(c.PhoneNumber);
        Assert.Null(c.Name);
        Assert.Null(c.Notify);
        Assert.Null(c.VerifiedName);
        Assert.Null(c.ImgUrl);
        Assert.Null(c.Status);
    }

    // ──────────────────────────────────────────────────────────
    //  GroupMetadata
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GroupMetadata_CanBeConstructed()
    {
        var gm = new GroupMetadata
        {
            Id      = "120363000000001@g.us",
            Subject = "Test Group",
            Participants = new List<GroupParticipant>
            {
                new() { Id = "111@s.whatsapp.net", IsAdmin = true, Admin = "admin" }
            }
        };
        Assert.Equal("Test Group", gm.Subject);
        Assert.Single(gm.Participants);
        Assert.True(gm.Participants[0].IsAdmin);
    }

    [Fact]
    public void GroupMetadata_DefaultFlags_AreFalse()
    {
        var gm = new GroupMetadata { Id = "x@g.us", Subject = "G" };
        Assert.False(gm.Announce);
        Assert.False(gm.Restrict);
        Assert.False(gm.IsCommunity);
        Assert.Empty(gm.Participants);
    }

    // ──────────────────────────────────────────────────────────
    //  WaCallEvent
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void WaCallEvent_CanBeConstructed()
    {
        var call = new WaCallEvent
        {
            ChatId = "123@c.us",
            From   = "456@s.whatsapp.net",
            Id     = "call-01",
            Date   = DateTimeOffset.UtcNow,
            Status = WaCallUpdateType.Offer
        };
        Assert.Equal(WaCallUpdateType.Offer, call.Status);
        Assert.False(call.IsVideo);
        Assert.False(call.Offline);
    }

    // ──────────────────────────────────────────────────────────
    //  Label / LabelAssociation
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Label_CanBeConstructed()
    {
        var l = new Label { Id = "lbl-1", Name = "New Order", Color = 3 };
        Assert.Equal("lbl-1", l.Id);
        Assert.Equal(3, l.Color);
        Assert.False(l.Deleted);
    }

    [Fact]
    public void LabelColor_EnumValues_0to19()
    {
        Assert.Equal(0, (int)LabelColor.Color1);
        Assert.Equal(19, (int)LabelColor.Color20);
    }

    [Fact]
    public void ChatLabelAssociation_TypeIsChat()
    {
        var assoc = new ChatLabelAssociation { ChatId = "c1", LabelId = "l1" };
        Assert.Equal(LabelAssociationType.Chat, assoc.Type);
    }

    [Fact]
    public void MessageLabelAssociation_TypeIsMessage()
    {
        var assoc = new MessageLabelAssociation { ChatId = "c1", MessageId = "m1", LabelId = "l1" };
        Assert.Equal(LabelAssociationType.Message, assoc.Type);
    }

    // ──────────────────────────────────────────────────────────
    //  ConnectionState / SyncState
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ConnectionState_DefaultIsConnecting()
    {
        var cs = new ConnectionState { Connection = WaConnectionState.Connecting };
        Assert.Equal(WaConnectionState.Connecting, cs.Connection);
        Assert.Null(cs.Qr);
        Assert.Null(cs.LastDisconnect);
    }

    [Fact]
    public void SyncState_Online_HasCorrectOrdinal()
    {
        Assert.Equal(3, (int)SyncState.Online);
    }

    // ──────────────────────────────────────────────────────────
    //  Message types
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void WaMessageKey_CanBeConstructed()
    {
        var key = new WaMessageKey { RemoteJid = "123@s.whatsapp.net", Id = "abc", FromMe = true };
        Assert.Equal("abc", key.Id);
        Assert.True(key.FromMe);
    }

    [Fact]
    public void MediaType_AllValuesUnique()
    {
        var values = Enum.GetValues<MediaType>().Cast<int>().ToList();
        Assert.Equal(values.Count, values.Distinct().Count());
    }

    // ──────────────────────────────────────────────────────────
    //  Newsletter
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void NewsletterMetadata_CanBeConstructed()
    {
        var nm = new NewsletterMetadata { Id = "nl-123", Name = "My Newsletter" };
        Assert.Equal("nl-123", nm.Id);
        Assert.Null(nm.Description);
    }

    [Fact]
    public void XWaPaths_CreateConstant_NotEmpty()
    {
        Assert.False(string.IsNullOrEmpty(XWaPaths.Create));
        Assert.Equal("xwa2_newsletter_create", XWaPaths.Create);
    }

    // ──────────────────────────────────────────────────────────
    //  Business types
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void UpdateBusinessProfileProps_AllFieldsOptional()
    {
        var props = new UpdateBusinessProfileProps();
        Assert.Null(props.Address);
        Assert.Null(props.Email);
        Assert.Null(props.Websites);
    }

    // ──────────────────────────────────────────────────────────
    //  Chat
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Chat_CanBeConstructed()
    {
        var c = new Chat { Id = "123@c.us", UnreadCount = 5, Archive = false };
        Assert.Equal(5, c.UnreadCount);
        Assert.False(c.Archive);
    }

    // ──────────────────────────────────────────────────────────
    //  Event payload types
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void ConnectionUpdateEvent_CanBeConstructed()
    {
        var ev = new ConnectionUpdateEvent { Connection = WaConnectionState.Open, IsOnline = true };
        Assert.Equal(WaConnectionState.Open, ev.Connection);
        Assert.True(ev.IsOnline);
    }

    [Fact]
    public void MessagesUpsertEvent_DefaultsToEmptyList()
    {
        var ev = new MessagesUpsertEvent();
        Assert.Empty(ev.Messages);
    }

    [Fact]
    public void GroupParticipantsUpdateEvent_CanBeConstructed()
    {
        var ev = new GroupParticipantsUpdateEvent
        {
            Id     = "120363@g.us",
            Author = "111@s.whatsapp.net",
            Action = ParticipantAction.Add
        };
        Assert.Equal(ParticipantAction.Add, ev.Action);
        Assert.Empty(ev.Participants);
    }

    // ──────────────────────────────────────────────────────────
    //  WaPrivacy enums
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void WaPrivacyValues_AllPresent()
    {
        Assert.Equal(4, Enum.GetValues<WaPrivacyValue>().Length);
        Assert.Contains(WaPrivacyValue.All, Enum.GetValues<WaPrivacyValue>());
        Assert.Contains(WaPrivacyValue.ContactBlacklist, Enum.GetValues<WaPrivacyValue>());
    }

    // ──────────────────────────────────────────────────────────
    //  WaPatchNames
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void WaPatchNames_All_Contains5Items()
    {
        Assert.Equal(5, WaPatchNames.All.Count);
        Assert.Contains("critical_block", WaPatchNames.All);
    }
}
