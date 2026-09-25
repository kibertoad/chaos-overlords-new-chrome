using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CreditsLayoutTests
{
    // SCR-UI-002, FND-UI-007: PX00100 covers (0,0,640,460), and a click there closes it.
    [Fact]
    public void CreditsCoverTheWholeScreen()
    {
        Assert.Equal(new Rectangle(0, 0, 640, 460), CreditsLayout.Screen);
    }

    // SCR-UI-002: any key closes the screen, on the frame it goes down.
    [Fact]
    public void AnyNewlyPressedKeyCloses()
    {
        var none = new KeyboardState();
        Assert.True(CreditsLayout.AnyKeyPressed(new KeyboardState(Keys.A), none));
        Assert.True(CreditsLayout.AnyKeyPressed(new KeyboardState(Keys.Escape), none));
        Assert.True(CreditsLayout.AnyKeyPressed(
            new KeyboardState(Keys.LeftShift, Keys.Space), new KeyboardState(Keys.LeftShift)));
        Assert.False(CreditsLayout.AnyKeyPressed(none, none));
        // The Shift+F1 that opened the screen, still held, does not close it.
        Assert.False(CreditsLayout.AnyKeyPressed(
            new KeyboardState(Keys.LeftShift, Keys.F1), new KeyboardState(Keys.LeftShift, Keys.F1)));
    }
}
