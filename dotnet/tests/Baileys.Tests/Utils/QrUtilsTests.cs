using Baileys.Utils;
using Xunit;

namespace Baileys.Tests.Utils;

/// <summary>Tests for <see cref="QrUtils"/>.</summary>
public class QrUtilsTests
{
    // ──────────────────────────────────────────────────────────
    //  Generate – matrix size
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData(0,   21)] // V1 (0–17 bytes)
    [InlineData(1,   21)]
    [InlineData(17,  21)]
    [InlineData(18,  25)] // V2 (18–32 bytes)
    [InlineData(32,  25)]
    [InlineData(33,  29)] // V3 (33–53 bytes)
    [InlineData(53,  29)]
    [InlineData(79,  37)] // V5 (79–106 bytes)
    [InlineData(154, 45)] // V7 (135–154 bytes)
    [InlineData(271, 57)] // V10 (231–271 bytes)
    public void Generate_VariousLengths_MatrixHasCorrectSize(int byteCount, int expectedSize)
    {
        var text   = new string('A', byteCount);
        var matrix = QrUtils.Generate(text);

        Assert.Equal(expectedSize, matrix.GetLength(0));
        Assert.Equal(expectedSize, matrix.GetLength(1));
    }

    [Fact]
    public void Generate_TooLongInput_ThrowsArgumentException()
    {
        var tooLong = new string('A', 272);
        Assert.Throws<ArgumentException>(() => QrUtils.Generate(tooLong));
    }

    // ──────────────────────────────────────────────────────────
    //  Generate – finder patterns
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TopLeftFinderPattern_CornersAndCentreDark()
    {
        var m = QrUtils.Generate("TEST");

        // Outer ring corners must be dark.
        Assert.True(m[0, 0]);
        Assert.True(m[0, 6]);
        Assert.True(m[6, 0]);
        Assert.True(m[6, 6]);

        // Inner-ring corners (one module in from the outer border) must be light.
        Assert.False(m[1, 1]);
        Assert.False(m[1, 5]);
        Assert.False(m[5, 1]);
        Assert.False(m[5, 5]);

        // Centre 3×3 block must be dark.
        Assert.True(m[2, 2]);
        Assert.True(m[3, 3]);
        Assert.True(m[4, 4]);
    }

    [Fact]
    public void Generate_TopRightFinderPattern_CornersAreDark()
    {
        var m    = QrUtils.Generate("TEST");
        int size = m.GetLength(0);

        Assert.True(m[0, size - 7]);
        Assert.True(m[0, size - 1]);
        Assert.True(m[6, size - 7]);
        Assert.True(m[6, size - 1]);
    }

    [Fact]
    public void Generate_BottomLeftFinderPattern_CornersAreDark()
    {
        var m    = QrUtils.Generate("TEST");
        int size = m.GetLength(0);

        Assert.True(m[size - 7, 0]);
        Assert.True(m[size - 7, 6]);
        Assert.True(m[size - 1, 0]);
        Assert.True(m[size - 1, 6]);
    }

    // ──────────────────────────────────────────────────────────
    //  Generate – timing patterns
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_TimingPatterns_AlternateDarkLight()
    {
        var m    = QrUtils.Generate("TEST");
        int size = m.GetLength(0);

        // Row 6: modules between the finders (cols 8..size-9) alternate dark/light.
        for (int c = 8; c < size - 8; c++)
            Assert.Equal(c % 2 == 0, m[6, c]);

        // Col 6: same rule for rows.
        for (int r = 8; r < size - 8; r++)
            Assert.Equal(r % 2 == 0, m[r, 6]);
    }

    // ──────────────────────────────────────────────────────────
    //  Generate – dark module
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_DarkModule_AlwaysDark()
    {
        var m    = QrUtils.Generate("TEST");
        int size = m.GetLength(0);

        Assert.True(m[size - 8, 8]);
    }

    // ──────────────────────────────────────────────────────────
    //  Generate – determinism
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Generate_SameInput_ProducesIdenticalMatrix()
    {
        const string text = "1,ABCDEFGH,IJKLMNOP,QRSTUVWX,YZABCDEF";
        var m1 = QrUtils.Generate(text);
        var m2 = QrUtils.Generate(text);
        int size = m1.GetLength(0);

        for (int r = 0; r < size; r++)
        for (int c = 0; c < size; c++)
            Assert.Equal(m1[r, c], m2[r, c]);
    }

    // ──────────────────────────────────────────────────────────
    //  RenderToAscii
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void RenderToAscii_ReturnsNonEmptyString()
    {
        var matrix = QrUtils.Generate("TEST");
        var ascii  = QrUtils.RenderToAscii(matrix);

        Assert.False(string.IsNullOrEmpty(ascii));
    }

    [Fact]
    public void RenderToAscii_ContainsDarkModuleCharacter()
    {
        var matrix = QrUtils.Generate("TEST");
        var ascii  = QrUtils.RenderToAscii(matrix);

        // Finder patterns guarantee dark modules are present.
        Assert.Contains("██", ascii);
    }

    [Fact]
    public void RenderToAscii_LineWidthIncludesQuietZone()
    {
        var matrix    = QrUtils.Generate("TEST");
        int size      = matrix.GetLength(0);
        const int border = 4;
        int expected  = (size + border * 2) * 2; // two chars per module

        var ascii = QrUtils.RenderToAscii(matrix);

        // The first line should be the quiet-zone blank row.
        var firstLine = ascii.Split('\n')[0].TrimEnd('\r');
        Assert.Equal(expected, firstLine.Length);
    }

    // ──────────────────────────────────────────────────────────
    //  LogQr
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void LogQr_InvokesLoggerInfoWithQrMessage()
    {
        var logger = new CapturingLogger();

        // Suppress the QR art written to Console.Out so CI output stays clean.
        var originalOut = Console.Out;
        Console.SetOut(new StringWriter());
        try
        {
            QrUtils.LogQr("TEST", logger);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Contains(logger.Messages, msg =>
            msg.Contains("QR", StringComparison.OrdinalIgnoreCase) ||
            msg.Contains("WhatsApp", StringComparison.OrdinalIgnoreCase));
    }
}

// ──────────────────────────────────────────────────────────────────────────────
//  Test helper: captures log messages
// ──────────────────────────────────────────────────────────────────────────────

internal sealed class CapturingLogger : ILogger
{
    public List<string> Messages { get; } = [];

    public string Level => "trace";
    public ILogger Child(IReadOnlyDictionary<string, object> _) => this;

    public void Trace(object message, string? template = null) => Messages.Add(message.ToString()!);
    public void Debug(object message, string? template = null) => Messages.Add(message.ToString()!);
    public void Info(object message, string? template = null)  => Messages.Add(message.ToString()!);
    public void Warn(object message, string? template = null)  => Messages.Add(message.ToString()!);
    public void Error(object message, string? template = null) => Messages.Add(message.ToString()!);
}
