using Microsoft.Xna.Framework;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// DEV-UI-019: the title buttons in the setup screen's stone, labelled in the lettering cut from
/// the setup and console buttons.
/// </summary>
public sealed class TitleButtonArtTests
{
    [Fact]
    public void EveryTitleLabelIsSpelledInTheLetteringAndFitsItsButton()
    {
        foreach (var (button, label) in ChaosGame.TitleButtons)
        {
            Assert.True(TitleButtonArt.CanSpell(label), label);
            Assert.True(TitleButtonArt.LabelOffset.X + TitleButtonArt.Width(label) < button.Width - TitleButtonArt.Face.Edge, label);
            Assert.True(TitleButtonArt.LabelOffset.Y + TitleButtonArt.GlyphHeight + 1 < button.Height - TitleButtonArt.Face.Edge, label);
        }
    }

    [Fact]
    public void LettersAreCutFromInsideTheirSheets()
    {
        // PX00129 is 512 by 646 pixels and PX00140 312 by 282.
        foreach (var letter in TitleButtonArt.Letters)
        {
            var glyph = TitleButtonArt.GlyphOf(letter);
            var sheet = glyph.Sheet == TitleButtonArt.Sheet.Console
                ? new Rectangle(0, 0, 512, 646)
                : new Rectangle(0, 0, 312, 282);
            Assert.True(sheet.Contains(glyph.Source), letter.ToString());
            Assert.Equal(TitleButtonArt.GlyphHeight, glyph.Source.Height);
        }
    }

    [Fact]
    public void FramesAreBeginsFaceAndHousingAndTheirTiledPartsStayOnThem()
    {
        Assert.Equal(SetupButtonLayout.Start, TitleButtonArt.Face.Source);
        foreach (var frame in new[] { TitleButtonArt.Face, TitleButtonArt.Housing })
            Assert.True(frame.Source.Contains(frame.TopEdge));
        Assert.True(TitleButtonArt.Face.Source.Contains(TitleButtonArt.Face.Interior));
        // The housing's fill runs the width of the gap under New Game in one piece.
        Assert.True(TitleButtonArt.Housing.Interior.Width
            >= ChaosGame.TitleUpperHousing.Width - 2 * TitleButtonArt.Housing.Edge);
        // Begin and Cancel sit inside the housing by its margin at least.
        var housing = TitleButtonArt.Housing.Source;
        Assert.True(SetupButtonLayout.Start.X - housing.X >= TitleButtonArt.HousingMargin.X);
        Assert.True(SetupButtonLayout.Start.Y - housing.Y >= TitleButtonArt.HousingMargin.Y);
    }

    [Fact]
    public void HousingsHoldEveryButtonMeetAtTheSeamAndClearTheCopyrightLine()
    {
        var upper = ChaosGame.TitleUpperHousing;
        var lower = ChaosGame.TitleLowerHousing;
        Assert.Equal(upper.Bottom, lower.Y);
        foreach (var (button, label) in ChaosGame.TitleButtons)
            Assert.True(upper.Contains(button) || lower.Contains(button), label);
        Assert.True(lower.Bottom <= ChaosGame.TitleCopyright.Y);
    }

    [Theory]
    [InlineData(255, 213, 65, true)]   // the yellow of a stem
    [InlineData(123, 98, 32, true)]    // a half-tone at a letter's edge
    [InlineData(8, 8, 8, true)]        // the shadow
    [InlineData(82, 90, 98, false)]    // the stone
    [InlineData(115, 123, 131, false)] // the bevel's light edge
    public void OnlyTheLetteringIsCutFromTheStone(int r, int g, int b, bool lettering) =>
        Assert.Equal(lettering, TitleButtonArt.IsLettering(new Color(r, g, b)));
}
