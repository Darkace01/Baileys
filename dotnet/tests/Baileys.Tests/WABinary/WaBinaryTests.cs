using Baileys.Types;
using Baileys.WABinary;
using Xunit;

namespace Baileys.Tests.WABinary;

/// <summary>
/// Tests for <see cref="WaBinaryEncoder"/> and <see cref="WaBinaryDecoder"/>.
/// </summary>
public class WaBinaryTests
{
    // ──────────────────────────────────────────────────────────
    //  Helpers
    // ──────────────────────────────────────────────────────────

    private static BinaryNode MakeNode(
        string tag,
        Dictionary<string, string>? attrs = null,
        BinaryNodeContent? content = null)
        => new() { Tag = tag, Attrs = attrs ?? [], Content = content };

    // ──────────────────────────────────────────────────────────
    //  Encoder — basic structure
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Encode_SimpleNode_ProducesBytesStartingWithZero()
    {
        var node = MakeNode("iq");
        var bytes = WaBinaryEncoder.EncodeBinaryNode(node);

        // First byte is the (no-compression) 0x00 flag
        Assert.NotEmpty(bytes);
        Assert.Equal(0, bytes[0]);
    }

    [Fact]
    public void Encode_TagCannotBeEmpty_Throws()
    {
        var node = MakeNode("");
        Assert.Throws<ArgumentException>(() => WaBinaryEncoder.EncodeBinaryNode(node));
    }

    // ──────────────────────────────────────────────────────────
    //  Decode — synchronous (no compression)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void DecodeSynchronous_SimpleNode_RoundTrip()
    {
        var original = MakeNode("iq", new Dictionary<string, string> { ["id"] = "test-123" });
        var encoded = WaBinaryEncoder.EncodeBinaryNode(original);

        // Strip the leading 0x00 and pass to the decompressed decoder
        var decoded = WaBinaryDecoder.DecodeDecompressedBinaryNode(encoded[1..]);

        Assert.Equal("iq", decoded.Tag);
        Assert.Equal("test-123", decoded.Attrs["id"]);
    }

    [Fact]
    public void DecodeSynchronous_NodeWithChildren_RoundTrip()
    {
        var child = MakeNode("item", new Dictionary<string, string> { ["key"] = "val" });
        var parent = MakeNode("list", null, new BinaryNodeList([child]));

        var encoded = WaBinaryEncoder.EncodeBinaryNode(parent);
        var decoded = WaBinaryDecoder.DecodeDecompressedBinaryNode(encoded[1..]);

        Assert.Equal("list", decoded.Tag);
        var children = BinaryNodeExtensions.GetChildren(decoded).ToList();
        Assert.Single(children);
        Assert.Equal("item", children[0].Tag);
        Assert.Equal("val", children[0].Attrs["key"]);
    }

    [Fact]
    public void DecodeSynchronous_NodeWithBytes_RoundTrip()
    {
        var data = new byte[] { 0x01, 0x02, 0x03, 0xFF };
        var original = MakeNode("media", null, new BinaryNodeBytes(data));

        var encoded = WaBinaryEncoder.EncodeBinaryNode(original);
        var decoded = WaBinaryDecoder.DecodeDecompressedBinaryNode(encoded[1..]);

        Assert.Equal("media", decoded.Tag);
        var bytes = BinaryNodeExtensions.GetBytes(decoded);
        Assert.NotNull(bytes);
        Assert.Equal(data, bytes);
    }

    [Fact]
    public void DecodeSynchronous_NodeWithMultipleAttrs_RoundTrip()
    {
        var attrs = new Dictionary<string, string>
        {
            ["type"] = "text",
            ["from"] = "123@s.whatsapp.net",
            ["id"]   = "abc-123"
        };
        var original = MakeNode("message", attrs);

        var encoded = WaBinaryEncoder.EncodeBinaryNode(original);
        var decoded = WaBinaryDecoder.DecodeDecompressedBinaryNode(encoded[1..]);

        Assert.Equal("message", decoded.Tag);
        Assert.Equal("text", decoded.Attrs["type"]);
        Assert.Equal("123@s.whatsapp.net", decoded.Attrs["from"]);
        Assert.Equal("abc-123", decoded.Attrs["id"]);
    }

    // ──────────────────────────────────────────────────────────
    //  Decode — async (with possible decompression)
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task DecodeAsync_SimpleNode_RoundTrip()
    {
        var original = MakeNode("iq", new Dictionary<string, string> { ["type"] = "get" });
        var encoded = WaBinaryEncoder.EncodeBinaryNode(original);

        var decoded = await WaBinaryDecoder.DecodeBinaryNodeAsync(encoded);

        Assert.Equal("iq", decoded.Tag);
        Assert.Equal("get", decoded.Attrs["type"]);
    }

    // ──────────────────────────────────────────────────────────
    //  BinaryNodeExtensions helpers
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void GetChildren_NoContent_Empty()
    {
        var node = MakeNode("empty");
        Assert.Empty(BinaryNodeExtensions.GetChildren(node));
    }

    [Fact]
    public void GetChild_ReturnsFirstMatch()
    {
        var child1 = MakeNode("item");
        var child2 = MakeNode("item");
        var parent = MakeNode("list", null, new BinaryNodeList([child1, child2]));

        var found = BinaryNodeExtensions.GetChild(parent, "item");
        Assert.NotNull(found);
        Assert.Same(child1, found);
    }

    [Fact]
    public void GetChild_NoMatch_ReturnsNull()
    {
        var parent = MakeNode("list", null, new BinaryNodeList([MakeNode("item")]));
        Assert.Null(BinaryNodeExtensions.GetChild(parent, "other"));
    }

    [Fact]
    public void GetBytes_OnBytesContent_ReturnsData()
    {
        var data = new byte[] { 1, 2, 3 };
        var node = MakeNode("n", null, new BinaryNodeBytes(data));
        Assert.Equal(data, BinaryNodeExtensions.GetBytes(node));
    }

    [Fact]
    public void GetBytes_OnStringContent_ReturnsNull()
    {
        var node = MakeNode("n", null, new BinaryNodeString("hello"));
        Assert.Null(BinaryNodeExtensions.GetBytes(node));
    }

    [Fact]
    public void GetString_OnStringContent_ReturnsValue()
    {
        var node = MakeNode("n", null, new BinaryNodeString("hello"));
        Assert.Equal("hello", BinaryNodeExtensions.GetString(node));
    }

    [Fact]
    public void GetString_OnBytesContent_ReturnsNull()
    {
        var node = MakeNode("n", null, new BinaryNodeBytes([0x01]));
        Assert.Null(BinaryNodeExtensions.GetString(node));
    }

    // ──────────────────────────────────────────────────────────
    //  Multiple attribute round-trips using WA single-byte tokens
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("type")]
    [InlineData("from")]
    [InlineData("id")]
    [InlineData("to")]
    [InlineData("xmlns")]
    public void Encode_Decode_TokenAttribute_Preserved(string attrKey)
    {
        var original = MakeNode("iq", new Dictionary<string, string> { [attrKey] = "value" });
        var encoded = WaBinaryEncoder.EncodeBinaryNode(original);
        var decoded = WaBinaryDecoder.DecodeDecompressedBinaryNode(encoded[1..]);

        Assert.True(decoded.Attrs.TryGetValue(attrKey, out var val));
        Assert.Equal("value", val);
    }
}
