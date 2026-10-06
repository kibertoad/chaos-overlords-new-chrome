using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class FadePointerReleaseTests
{
    // RULE-AUDIO-003, FND-AUDIO-016: dispatch during the fade does not run game actions.
    // FND-UI-020: releases still clear the corresponding window button state.
    // These call the rebuild's held-control cleanup directly; no OS message pump runs.

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwallowedReleaseCancelsOnlyTheButtonHoldingThePanelFace(bool rightButton)
    {
        var invoked = false;
        var game = GameHoldingSectorBack(rightButton, () => invoked = true);

        // Let go of the other button first; its release cannot cancel this face.
        var otherReleased = rightButton
            ? Mouse(ButtonState.Released, ButtonState.Pressed)
            : Mouse(ButtonState.Pressed, ButtonState.Released);
        Cancel(game, otherReleased);
        Assert.NotNull(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        Field("_previousMouse").SetValue(game, otherReleased);
        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        // A later press and release of the owning button on the face, after the fade, cannot
        // complete the face that the fade cancelled.
        Field("_previousMouse").SetValue(game, rightButton
            ? Mouse(ButtonState.Released, ButtonState.Pressed)
            : Mouse(ButtonState.Pressed, ButtonState.Released));
        Release(game, SectorDetailLayout.Back.Center, rightButton);
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwallowedReleaseOfBothButtonsInOneFrameCancelsThePanelFace(bool rightButton)
    {
        var invoked = false;
        var game = GameHoldingSectorBack(rightButton, () => invoked = true);

        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);
    }

    /// <summary>
    /// The sector Back face held with both buttons down: the only panel face a right press holds
    /// (FND-UI-015).
    /// </summary>
    private static ChaosGame GameHoldingSectorBack(bool rightButton, Action close)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        Field("_pressedPanelFace").SetValue(game,
            (SectorDetailLayout.Back, ClientScreen.Sector, close));
        Field("_pressedPanelFaceByRightButton").SetValue(game, rightButton);
        Field("_previousMouse").SetValue(game, Mouse(ButtonState.Pressed, ButtonState.Pressed));
        return game;
    }

    private static MouseState Mouse(ButtonState left, ButtonState right) =>
        new(10, 10, 0, left, ButtonState.Released, right, ButtonState.Released, ButtonState.Released);
    private static void Cancel(ChaosGame game, MouseState mouse) =>
        Method("UpdatePointerDuringFade").Invoke(game, [mouse]);
    private static void Release(ChaosGame game, Point point, bool rightButton) =>
        Method("CompletePointerRelease").Invoke(game, [true, point, rightButton]);
    private static MethodInfo Method(string name) => typeof(ChaosGame).GetMethod(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingMethodException(name);
    private static FieldInfo Field(string name) => typeof(ChaosGame).GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic) ?? throw new MissingFieldException(name);
}
