using System.Reflection;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class PixelFontTests
{
    [Fact]
    public void EveryAdvertisedCharacterHasAGlyph()
    {
        var characters = (string)typeof(PixelFont)
            .GetField("Characters", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetRawConstantValue()!;
        var glyphs = (string[])typeof(PixelFont)
            .GetField("Glyphs", BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        Assert.Equal(characters.Length, glyphs.Length);
        Assert.Equal('\'', characters[^1]);
    }
}
