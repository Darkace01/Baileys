using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>
/// Tests for <see cref="Generics"/> mirroring the TypeScript
/// <c>Utils/generics.ts</c> helpers.
/// </summary>
public class GenericsTests
{
    // ──────────────────────────────────────────────────────────
    //  EncodeBigEndian
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void EncodeBigEndian_Int_CorrectBytes()
    {
        var result = Generics.EncodeBigEndian(0x01020304, 4);
        Assert.Equal([0x01, 0x02, 0x03, 0x04], result);
    }

    [Fact]
    public void EncodeBigEndian_Zero()
    {
        var result = Generics.EncodeBigEndian(0, 4);
        Assert.Equal([0, 0, 0, 0], result);
    }

    [Fact]
    public void EncodeBigEndian_Long()
    {
        var result = Generics.EncodeBigEndian(0x0102030405060708L, 8);
        Assert.Equal([0x01, 0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08], result);
    }

    // ──────────────────────────────────────────────────────────
    //  Padding
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void WriteRandomPadMax16_AddsAtLeastOneByte()
    {
        var msg = new byte[] { 1, 2, 3 };
        var padded = Generics.WriteRandomPadMax16(msg);

        Assert.True(padded.Length > msg.Length);
        Assert.True(padded.Length <= msg.Length + 16);
    }

    [Fact]
    public void WriteUnpadRandomMax16_RoundTrip()
    {
        var original = new byte[] { 0xAA, 0xBB, 0xCC };
        var padded = Generics.WriteRandomPadMax16(original);
        var restored = Generics.UnpadRandomMax16(padded);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void UnpadRandomMax16_EmptyInput_Throws()
    {
        Assert.Throws<InvalidOperationException>(
            () => Generics.UnpadRandomMax16(Array.Empty<byte>()));
    }

    // ──────────────────────────────────────────────────────────
    //  Message ID generation
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateMessageId_StartsWithPrefix()
    {
        var id = Generics.GenerateMessageId();
        Assert.StartsWith("3EB0", id);
    }

    [Fact]
    public void GenerateMessageId_CorrectLength()
    {
        var id = Generics.GenerateMessageId();
        // "3EB0" + 18 bytes × 2 hex chars = 4 + 36 = 40 chars
        Assert.Equal(40, id.Length);
    }

    [Fact]
    public void GenerateMessageId_Unique()
    {
        var ids = Enumerable.Range(0, 100).Select(_ => Generics.GenerateMessageId()).ToHashSet();
        Assert.Equal(100, ids.Count);
    }

    [Fact]
    public void GenerateMessageIdV2_StartsWithPrefix()
    {
        var id = Generics.GenerateMessageIdV2();
        Assert.StartsWith("3EB0", id);
    }

    [Fact]
    public void GenerateMessageIdV2_WithUserId()
    {
        var id = Generics.GenerateMessageIdV2("123456789@s.whatsapp.net");
        Assert.StartsWith("3EB0", id);
    }

    // ──────────────────────────────────────────────────────────
    //  Crockford Base-32
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void BytesToCrockford_EmptyBytes_ReturnsEmpty()
    {
        var result = Generics.BytesToCrockford(Array.Empty<byte>());
        Assert.Equal("", result);
    }

    [Fact]
    public void BytesToCrockford_KnownBytes()
    {
        // Each 5-bit group is mapped to CrockfordChars
        // 0xFF = 11111111 → groups of 5: 11111 111xx → indices 31 and partial
        var result = Generics.BytesToCrockford(new byte[] { 0xFF });
        Assert.False(string.IsNullOrEmpty(result));
        // Result should only contain valid Crockford chars
        const string chars = "123456789ABCDEFGHJKLMNPQRSTVWXYZ";
        Assert.All(result, c => Assert.Contains(c, chars));
    }

    [Fact]
    public void BytesToCrockford_Deterministic()
    {
        var data = new byte[] { 1, 2, 3, 4, 5 };
        Assert.Equal(Generics.BytesToCrockford(data), Generics.BytesToCrockford(data));
    }

    // ──────────────────────────────────────────────────────────
    //  Participant hash
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateParticipantHashV2_StartsWithPrefix()
    {
        var hash = Generics.GenerateParticipantHashV2(["a@c.us", "b@c.us"]);
        Assert.StartsWith("2:", hash);
    }

    [Fact]
    public void GenerateParticipantHashV2_SortOrderIndependent()
    {
        var h1 = Generics.GenerateParticipantHashV2(["b@c.us", "a@c.us"]);
        var h2 = Generics.GenerateParticipantHashV2(["a@c.us", "b@c.us"]);
        Assert.Equal(h1, h2);
    }

    [Fact]
    public void GenerateParticipantHashV2_DifferentSets()
    {
        var h1 = Generics.GenerateParticipantHashV2(["a@c.us"]);
        var h2 = Generics.GenerateParticipantHashV2(["b@c.us"]);
        Assert.NotEqual(h1, h2);
    }

    // ──────────────────────────────────────────────────────────
    //  String helpers
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null,  true)]
    [InlineData("",    true)]
    [InlineData(" ",   false)]
    [InlineData("abc", false)]
    public void IsNullOrEmpty(string? value, bool expected)
        => Assert.Equal(expected, Generics.IsNullOrEmpty(value));

    // ──────────────────────────────────────────────────────────
    //  Business platform detection
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("smbi", true)]
    [InlineData("smba", true)]
    [InlineData("ios",  false)]
    [InlineData("",     false)]
    public void IsWaBusinessPlatform(string platform, bool expected)
        => Assert.Equal(expected, Generics.IsWaBusinessPlatform(platform));

    // ──────────────────────────────────────────────────────────
    //  Unix timestamp
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void UnixTimestampSeconds_CurrentTime_GreaterThanZero()
    {
        var ts = Generics.UnixTimestampSeconds();
        Assert.True(ts > 0);
    }

    [Fact]
    public void UnixTimestampSeconds_KnownDate()
    {
        var dt = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(1704067200L, Generics.UnixTimestampSeconds(dt));
    }

    // ──────────────────────────────────────────────────────────
    //  MD-tag prefix
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GenerateMdTagPrefix_EndsWithDash()
    {
        var prefix = Generics.GenerateMdTagPrefix();
        Assert.EndsWith("-", prefix);
    }

    [Fact]
    public void GenerateMdTagPrefix_ContainsDot()
    {
        var prefix = Generics.GenerateMdTagPrefix();
        Assert.Contains(".", prefix);
    }
}
