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

    [Fact]
    public void SwallowedReleaseCancelsThePanelFaceOnlyForTheLeftButton()
    {
        var invoked = false;
        var game = GameHoldingSectorBack(() => invoked = true);

        // The face's helper loop follows the left button only (FND-UI-063).
        var rightReleased = Mouse(ButtonState.Pressed, ButtonState.Released);
        Cancel(game, rightReleased);
        Assert.NotNull(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        Field("_previousMouse").SetValue(game, rightReleased);
        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);

        // A later press and release on the face, after the fade, cannot complete the face that the
        // fade cancelled.
        Field("_previousMouse").SetValue(game, Mouse(ButtonState.Pressed, ButtonState.Released));
        Release(game, SectorDetailLayout.Back.Center, rightButton: false);
        Assert.False(invoked);
    }

    [Fact]
    public void SwallowedReleaseOfBothButtonsInOneFrameCancelsThePanelFace()
    {
        var invoked = false;
        var game = GameHoldingSectorBack(() => invoked = true);

        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedPanelFace").GetValue(game));
        Assert.False(invoked);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SwallowedReleaseCancelsOnlyTheButtonHoldingAConsoleTile(bool rightButton)
    {
        var game = GameHoldingConsoleTile(rightButton);

        // FND-UI-063: a tile pressed with the right button is held until that button comes up.
        var otherReleased = rightButton
            ? Mouse(ButtonState.Released, ButtonState.Pressed)
            : Mouse(ButtonState.Pressed, ButtonState.Released);
        Cancel(game, otherReleased);
        Assert.NotNull(Field("_pressedCityConsoleControl").GetValue(game));

        Field("_previousMouse").SetValue(game, otherReleased);
        Cancel(game, Mouse(ButtonState.Released, ButtonState.Released));
        Assert.Null(Field("_pressedCityConsoleControl").GetValue(game));
        Assert.False((bool)Field("_pressedCityConsoleByRightButton").GetValue(game)!);
    }

    /// <summary>The sector Back face held with both buttons down.</summary>
    private static ChaosGame GameHoldingSectorBack(Action close)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Field("_pressedPanelFace").SetValue(game,
            (SectorDetailLayout.Back, ClientScreen.Sector, close));
        Field("_previousMouse").SetValue(game, Mouse(ButtonState.Pressed, ButtonState.Pressed));
        return game;
    }

    /// <summary>The Done tile held by one button while both are down.</summary>
    private static ChaosGame GameHoldingConsoleTile(bool rightButton)
    {
        var game = (ChaosGame)RuntimeHelpers.GetUninitializedObject(typeof(ChaosGame));
        GC.SuppressFinalize(game);
        Field("_pressedCityConsoleControl").SetValue(game, CityConsoleControl.Done);
        Field("_pressedCityConsoleByRightButton").SetValue(game, rightButton);
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
