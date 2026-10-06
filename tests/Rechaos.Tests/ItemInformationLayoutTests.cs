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
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(354, 0, 6, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(59, false, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(354, 8, 6, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(59, true, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(504, 0, 6, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(84, false, 512));
        // RULE-UI-004, EXP-UI-002, EXP-UI-027, EXP-UI-028: of a source cell not wholly inside the
        // 512-pixel bitmap only the pixel columns inside it are copied, from either row, and a cell
        // with none inside draws nothing. The rest of the cell shows what was drawn beneath it in
        // the same frame, where the original keeps an earlier draw's pixels (DEV-UI-025).
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(510, 0, 2, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(85, false, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(510, 8, 2, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(85, true, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(86, false, 512));
    }

    [Fact]
    public void CutsTheSourceColumnOfAnOffStripCellToSixteenBits()
    {
        // FND-UI-065: the helper computes 6 * (16 + q) in 32 bits and the rectangle packer keeps
        // its low 16 bits, which the copy reads as a signed number. From 6 * 5462 = 32772 the
        // column is negative, and from 6 * 10923 = 65538 it lands inside the bitmap again.
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(5462, false, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(2, 0, 6, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(10923, false, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(506, 8, 6, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(11007, true, 512));
        // Glyph 10922 starts at column -4, so its two right pixel columns come from columns 0 and 1
        // and land four pixels into the cell (EXP-UI-027). Glyphs 16383, 27306 and 5461 start at
        // columns 32762, 32764 and 32766, where the copy becomes a StretchBlt (FND-UI-065) that
        // changes nothing, over the console or an earlier glyph (EXP-UI-028, EXP-UI-038,
        // EXP-UI-039, EXP-UI-040).
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(0, 0, 2, 7), 4), NativeTwoCellNumberPresentation.AtlasCell(10922, false, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(5461, false, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(16383, true, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(27306, true, 512));
        // RULE-UI-004: the other two partly inside columns, 508 from glyph 21930 and -2 from
        // glyph 21845, keep four of the cell's six pixel columns (EXP-UI-038, EXP-UI-039,
        // EXP-UI-040).
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(508, 0, 4, 7), 0), NativeTwoCellNumberPresentation.AtlasCell(21930, false, 512));
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(0, 8, 4, 7), 2), NativeTwoCellNumberPresentation.AtlasCell(21845, true, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(11008, false, 512));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(int.MaxValue, false, 512));
        // A five-cell score of 109070000 has the leading quotient 10907, glyph 10923.
        var score = NativeTwoCellNumberPresentation.Format(109070000, NativeTwoCellNumberPresentation.Kind.Baseline,
            StatusConsoleLayout.ScoreCells);
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(" 0000", false, false, 10923), score);
        Assert.Equal(new NativeTwoCellNumberPresentation.CellCopy(new Rectangle(2, 0, 6, 7), 0),
            NativeTwoCellNumberPresentation.AtlasCell(score.OffStripGlyph!.Value, false, 512));
    }

    [Fact]
    public void DrawsAPartlyInsideCellAtItsOffsetWithinTheCell()
    {
        // RULE-UI-004, EXP-UI-027, EXP-UI-038, EXP-UI-040: at column -4 the two inside pixel
        // columns land four pixels into the cell, and at column -2 the four inside columns land two
        // pixels in; at 510 and 508 the copy starts at the cell's left edge (EXP-UI-039).
        Assert.Equal(new Rectangle(104, 40, 2, 7),
            NativeTwoCellNumberPresentation.AtlasCell(10922, false, 512)!.Value.Destination(100, 40));
        Assert.Equal(new Rectangle(102, 40, 4, 7),
            NativeTwoCellNumberPresentation.AtlasCell(21845, true, 512)!.Value.Destination(100, 40));
        Assert.Equal(new Rectangle(100, 40, 2, 7),
            NativeTwoCellNumberPresentation.AtlasCell(85, false, 512)!.Value.Destination(100, 40));
        Assert.Equal(new Rectangle(100, 40, 4, 7),
            NativeTwoCellNumberPresentation.AtlasCell(21930, false, 512)!.Value.Destination(100, 40));
    }

    [Fact]
    public void KeepsTheOriginalsThirtyTwoBitArithmeticForTheMostNegativeValue()
    {
        // FND-UI-065: negating int.MinValue leaves it negative, so the signed divide gives the
        // quotient -214748364 (glyph -214748348, off the strip) and the remainder -8, which
        // selects glyph 8, the character '(' of the strip, from the red row.
        Assert.Equal(new NativeTwoCellNumberPresentation.Value(" (", true, false, -214748348),
            NativeTwoCellNumberPresentation.Format(int.MinValue));
        Assert.Null(NativeTwoCellNumberPresentation.AtlasCell(-214748348, true, 512));
    }
}
