using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class FadePointerReleaseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwallowedReleaseCancelsOnlyTheButtonHoldingThePanelFace(bool rightButton)
    {
        // RULE-AUDIO-003, FND-AUDIO-016: dispatch during the fade does not run game actions.
        // FND-UI-020: releases still clear the corresponding window button state.
        // Exercise the rebuild's held-control cleanup, not an OS message pump.
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        var invoked = false;
        var face = (new Rectangle(0, 0, 20, 20), ClientScreen.City, (Action)(() => invoked = true));
        Field("_pressedPanelFace").SetValue(game, face);
        Field("_pressedPanelFaceByRightButton").SetValue(game, rightButton);
        Field("_previousMouse").SetValue(game, Mouse(ButtonState.Pressed, ButtonState.Pressed));

        // Let go of the other button first; its release cannot cancel this face.
        Cancel(game, rightButton
            ? Mouse(ButtonState.Released, ButtonState.Pressed)
            : Mouse(ButtonState.Pressed, ButtonState.Released));
        Assert.NotNull(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        Field("_previousMouse").SetValue(game, rightButton
            ? Mouse(ButtonState.Released, ButtonState.Pressed)
            : Mouse(ButtonState.Pressed, ButtonState.Released));
        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        // Subsequent releases cannot complete the face that the fade cancelled.
        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.False(invoked);
    }

    private static MouseState Mouse(ButtonState left, ButtonState right) =>
        new(10, 10, 0, left, ButtonState.Released, right, ButtonState.Released, ButtonState.Released);
    private static void Cancel(ChaosGame game, MouseState mouse) => typeof(ChaosGame)
        .GetMethod("CancelSwallowedPointerReleases", BindingFlags.Instance | BindingFlags.NonPublic)!
        .Invoke(game, [mouse]);
    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
}
