using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class ItemInformationLayoutTests
{
    [Theory]
    [InlineData(0, "0", false)]
    [InlineData(99999, "99999", false)]
    [InlineData(-12345, "12345", true)]
    [InlineData(100000, ":0000", false)]
    [InlineData(-123456, "<3456", true)]
    public void ConsoleScoreUsesFiveOriginalNumericCells(int score, string digits, bool negative)
    {
        // FND-UI-040, RULE-UI-004: five cells at (550,24); the leading cell retains the whole quotient.
        Assert.Equal((550, 24, 5), (StatusConsoleLayout.ScoreLeft, StatusConsoleLayout.ScoreY,
            StatusConsoleLayout.ScoreCells));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(digits, negative, false),
            NativeTwoCellNumberPresentation.Format(score, NativeTwoCellNumberPresentation.Kind.Baseline,
                StatusConsoleLayout.ScoreCells));
    }

    [Fact]
    public void UsesTheRecoveredAlternatePanelGeometry()
    {
        Assert.Equal(new Rectangle(128, 124, 320, 209), ItemInformationLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), ItemInformationLayout.BackgroundSource);
        Assert.Equal(new Rectangle(162, 141, 48, 48), ItemInformationLayout.Portrait);
        Assert.Equal(new Rectangle(161, 293, 49, 22), ItemInformationLayout.Ok);
        Assert.Equal(228, ItemInformationLayout.NameLeft);
        Assert.Equal(408, ItemInformationLayout.TypeRight);
        Assert.Equal(30, ItemInformationLayout.DescriptionColumns);
        Assert.Equal([169, 178, 187], Enumerable.Range(0, 3)
            .Select(row => ItemInformationLayout.DescriptionY + row * 9));
        Assert.Equal(new Rectangle(300, 216, 12, 7),
            GangInformationLayout.ValueField(ItemInformationLayout.LeftValueLeft, 216));
        Assert.Equal(new Rectangle(396, 216, 12, 7),
            GangInformationLayout.ValueField(ItemInformationLayout.RightValueLeft, 216));
    }

    [Fact]
    public void PreservesAuthoredFixedWidthDescriptionRows()
    {
        var description = "TITANIUM ALLOY.               "
            + "GOOD FOR BUSTING IN A FEW     "
            + "HARD HEADS.";

        Assert.Equal(
        [
            "TITANIUM ALLOY.",
            "GOOD FOR BUSTING IN A FEW",
            "HARD HEADS."
        ], ItemInformationLayout.DescriptionLines(description));
    }

    [Fact]
    public void FormatsNegativeModifiersWithinTheNativeTwoCellField()
    {
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("2", true, false),
            NativeTwoCellNumberPresentation.Format(-2));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("12", true, false),
            NativeTwoCellNumberPresentation.Format(-12));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("45", false, false),
            NativeTwoCellNumberPresentation.Format(45));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("0", false, true),
            NativeTwoCellNumberPresentation.Format(0));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("0", false, false),
            NativeTwoCellNumberPresentation.Format(0, NativeTwoCellNumberPresentation.Kind.Baseline));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("9999", false, false),
            NativeTwoCellNumberPresentation.Format(9999, NativeTwoCellNumberPresentation.Kind.Baseline, 4));
    }

    [Fact]
    public void PutsTheWholeLeadingQuotientOfAWideValueInTheFirstCell()
    {
        // RULE-UI-004: 123 in two cells draws glyph 28, the character '<', and then 3.
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("<3", false, false),
            NativeTwoCellNumberPresentation.Format(123, NativeTwoCellNumberPresentation.Kind.Baseline));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(":0", true, false),
            NativeTwoCellNumberPresentation.Format(-100, NativeTwoCellNumberPresentation.Kind.Baseline));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(":5", false, false),
            NativeTwoCellNumberPresentation.Format(105));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(":05", false, false),
            NativeTwoCellNumberPresentation.Format(1005, width: 3));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(":", false, false),
            NativeTwoCellNumberPresentation.Format(10, width: 1));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value("Z9", false, false),
            NativeTwoCellNumberPresentation.Format(429));
    }

    [Fact]
    public void TakesALeadingQuotientPastTheFontStripFromTheRawAtlasCell()
    {
        // RULE-UI-004: glyph 16 + q is copied from (6(16 + q), 0) of PX00129, or from row 8 when
        // negative, even past the strip's last character; the digits keep a blank in its place.
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(" 0", false, false, 59),
            NativeTwoCellNumberPresentation.Format(430));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(" 0000", true, false, 59),
            NativeTwoCellNumberPresentation.Format(-430000, NativeTwoCellNumberPresentation.Kind.Baseline,
                StatusConsoleLayout.ScoreCells));
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(" 8", true, false, 214748380),
            NativeTwoCellNumberPresentation.Format(int.MinValue));
        Assert.Equal(new Rectangle(354, 0, 6, 7), NativeTwoCellNumberPresentation.AtlasCell(59, false, 512));
        Assert.Equal(new Rectangle(354, 8, 6, 7), NativeTwoCellNumberPresentation.AtlasCell(59, true, 512));
        Assert.Equal(new Rectangle(504, 0, 6, 7), NativeTwoCellNumberPresentation.AtlasCell(84, false, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(85, false, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(int.MaxValue, false, 512));
    }
}
