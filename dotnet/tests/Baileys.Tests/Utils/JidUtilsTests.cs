using Baileys.Types;
using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>
/// Tests for <see cref="JidUtils"/>, mirroring the TypeScript
/// <c>WABinary/jid-utils.ts</c> behaviour.
/// </summary>
public class JidUtilsTests
{
    // ──────────────────────────────────────────────────────────
    //  Encode
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123456789", JidServer.SWhatsappNet, null, null, "123456789@s.whatsapp.net")]
    [InlineData("123456789", JidServer.ContactUs,   null, null, "123456789@c.us")]
    [InlineData("123456789", JidServer.GroupUs,     null, null, "123456789@g.us")]
    [InlineData("123456789", JidServer.Lid,         null, null, "123456789@lid")]
    [InlineData("123456789", JidServer.Newsletter,  null, null, "123456789@newsletter")]
    public void JidEncode_SimpleUser(
        string user, JidServer server, int? device, int? agent, string expected)
    {
        var result = JidUtils.JidEncode(user, server, device, agent);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void JidEncode_WithDevice()
    {
        var result = JidUtils.JidEncode("123", JidServer.SWhatsappNet, 5);
        Assert.Equal("123:5@s.whatsapp.net", result);
    }

    [Fact]
    public void JidEncode_WithAgent()
    {
        var result = JidUtils.JidEncode("123", JidServer.SWhatsappNet, null, 1);
        Assert.Equal("123_1@s.whatsapp.net", result);
    }

    [Fact]
    public void JidEncode_NullUser_EmptyPrefix()
    {
        var result = JidUtils.JidEncode(null, JidServer.ContactUs);
        Assert.Equal("@c.us", result);
    }

    // ──────────────────────────────────────────────────────────
    //  Decode
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void JidDecode_SimpleUser()
    {
        var jid = JidUtils.JidDecode("123456789@s.whatsapp.net");
        Assert.NotNull(jid);
        Assert.Equal("123456789", jid.User);
        Assert.Equal(JidServer.SWhatsappNet, jid.Server);
        Assert.Null(jid.Device);
    }

    [Fact]
    public void JidDecode_WithDevice()
    {
        var jid = JidUtils.JidDecode("123:5@s.whatsapp.net");
        Assert.NotNull(jid);
        Assert.Equal("123", jid.User);
        Assert.Equal(5, jid.Device);
        Assert.Equal(JidServer.SWhatsappNet, jid.Server);
    }

    [Fact]
    public void JidDecode_GroupJid()
    {
        var jid = JidUtils.JidDecode("120363000000001@g.us");
        Assert.NotNull(jid);
        Assert.Equal("120363000000001", jid.User);
        Assert.Equal(JidServer.GroupUs, jid.Server);
    }

    [Fact]
    public void JidDecode_LidJid()
    {
        var jid = JidUtils.JidDecode("some-lid@lid");
        Assert.NotNull(jid);
        Assert.Equal("some-lid", jid.User);
        Assert.Equal(JidServer.Lid, jid.Server);
    }

    [Fact]
    public void JidDecode_Null_ReturnsNull()
        => Assert.Null(JidUtils.JidDecode(null));

    [Fact]
    public void JidDecode_NoAt_ReturnsNull()
        => Assert.Null(JidUtils.JidDecode("nodomain"));

    [Fact]
    public void JidDecode_Empty_ReturnsNull()
        => Assert.Null(JidUtils.JidDecode(""));

    // ──────────────────────────────────────────────────────────
    //  Round-trip
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123456789@s.whatsapp.net")]
    [InlineData("120363000000001@g.us")]
    [InlineData("123456789@c.us")]
    [InlineData("123456789@lid")]
    [InlineData("status@broadcast")]
    public void JidEncodeDecode_RoundTrip(string jid)
    {
        var decoded = JidUtils.JidDecode(jid);
        Assert.NotNull(decoded);
        var encoded = JidUtils.JidEncode(decoded.User, decoded.Server, decoded.Device);
        Assert.Equal(jid, encoded);
    }

    // ──────────────────────────────────────────────────────────
    //  Normalisation
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void JidNormalizedUser_ContactToSWhatsapp()
    {
        var normalized = JidUtils.JidNormalizedUser("123@c.us");
        Assert.Equal("123@s.whatsapp.net", normalized);
    }

    [Fact]
    public void JidNormalizedUser_AlreadyNormalized()
    {
        var normalized = JidUtils.JidNormalizedUser("123@s.whatsapp.net");
        Assert.Equal("123@s.whatsapp.net", normalized);
    }

    [Fact]
    public void JidNormalizedUser_Invalid_ReturnsEmpty()
    {
        Assert.Equal(string.Empty, JidUtils.JidNormalizedUser(null));
        Assert.Equal(string.Empty, JidUtils.JidNormalizedUser("invalid"));
    }

    // ──────────────────────────────────────────────────────────
    //  Type predicates
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("123@s.whatsapp.net", true)]
    [InlineData("123@c.us",           false)]
    [InlineData(null,                  false)]
    public void IsPnUser(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsPnUser(jid));

    [Theory]
    [InlineData("120363@g.us", true)]
    [InlineData("123@c.us",    false)]
    [InlineData(null,           false)]
    public void IsJidGroup(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidGroup(jid));

    [Theory]
    [InlineData("status@broadcast", true)]
    [InlineData("other@broadcast",  false)]
    [InlineData(null,                false)]
    public void IsJidStatusBroadcast(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidStatusBroadcast(jid));

    [Theory]
    [InlineData("news@newsletter", true)]
    [InlineData("news@g.us",       false)]
    [InlineData(null,               false)]
    public void IsJidNewsletter(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidNewsletter(jid));

    [Theory]
    [InlineData("something@broadcast", true)]
    [InlineData("something@c.us",      false)]
    [InlineData(null,                   false)]
    public void IsJidBroadcast(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidBroadcast(jid));

    [Theory]
    [InlineData("abc@lid", true)]
    [InlineData("abc@c.us", false)]
    [InlineData(null,       false)]
    public void IsLidUser(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsLidUser(jid));

    [Theory]
    [InlineData("abc@hosted", true)]
    [InlineData("abc@c.us",   false)]
    [InlineData(null,          false)]
    public void IsHostedPnUser(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsHostedPnUser(jid));

    [Theory]
    [InlineData("abc@hosted.lid", true)]
    [InlineData("abc@hosted",     false)]
    [InlineData(null,              false)]
    public void IsHostedLidUser(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsHostedLidUser(jid));

    [Theory]
    [InlineData("something@bot", true)]
    [InlineData("something@c.us", false)]
    [InlineData(null,              false)]
    public void IsJidMetaAi(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidMetaAi(jid));

    // ──────────────────────────────────────────────────────────
    //  AreJidsSameUser
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AreJidsSameUser_SameUser_DifferentServer()
    {
        Assert.True(JidUtils.AreJidsSameUser("123@c.us", "123@s.whatsapp.net"));
    }

    [Fact]
    public void AreJidsSameUser_DifferentUsers()
    {
        Assert.False(JidUtils.AreJidsSameUser("123@c.us", "456@c.us"));
    }

    [Fact]
    public void AreJidsSameUser_Nulls_BothNull_True()
    {
        // TypeScript: jidDecode(null)?.user === undefined === undefined → true
        Assert.True(JidUtils.AreJidsSameUser(null, null));
    }

    [Fact]
    public void AreJidsSameUser_OneNull_False()
    {
        Assert.False(JidUtils.AreJidsSameUser(null, "123@c.us"));
        Assert.False(JidUtils.AreJidsSameUser("123@c.us", null));
    }

    // ──────────────────────────────────────────────────────────
    //  IsJidBot
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("13135550001@c.us", true)]
    [InlineData("13165550001@c.us", true)]   // matches 131655500\d{2}
    [InlineData("99999999999@c.us", false)]  // no match
    [InlineData("13135550001@g.us", false)]  // wrong server
    [InlineData(null,                false)]
    public void IsJidBot(string? jid, bool expected)
        => Assert.Equal(expected, JidUtils.IsJidBot(jid));

    // ──────────────────────────────────────────────────────────
    //  TransferDevice
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void TransferDevice_CopiesDevice()
    {
        var result = JidUtils.TransferDevice("123:7@s.whatsapp.net", "456@s.whatsapp.net");
        Assert.Equal("456:7@s.whatsapp.net", result);
    }

    [Fact]
    public void TransferDevice_NoDevice_UsesZero()
    {
        var result = JidUtils.TransferDevice("123@s.whatsapp.net", "456@s.whatsapp.net");
        Assert.Equal("456:0@s.whatsapp.net", result);
    }
}
