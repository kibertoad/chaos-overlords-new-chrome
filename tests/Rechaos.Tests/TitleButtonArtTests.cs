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
            Assert.True(TitleButtonArt.LabelOffset.X + TitleButtonArt.Width(label) < button.Width - TitleButtonArt.Edge, label);
            Assert.True(TitleButtonArt.LabelOffset.Y + TitleButtonArt.GlyphHeight + 1 < button.Height - TitleButtonArt.Edge, label);
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
    public void FaceIsBeginsButtonAndItsTiledPartsStayOnIt()
    {
        Assert.Equal(SetupButtonLayout.Start, TitleButtonArt.Face);
        Assert.True(TitleButtonArt.Face.Contains(TitleButtonArt.TopEdge));
        Assert.True(TitleButtonArt.Face.Contains(TitleButtonArt.Interior));
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
