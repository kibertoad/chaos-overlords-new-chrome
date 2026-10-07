using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>The close face held down on an information panel, and what releasing it does.</summary>
    private (Rectangle Face, ClientScreen Screen, Action Close)? _pressedPanelFace;

    /// <summary>
    /// The face kind the held-button helper draws for <see cref="_pressedPanelFace"/>, or null for
    /// the sector view's back control, which draws its own (FND-UI-062).
    /// </summary>
    private HeldButtonKind? _pressedPanelFaceKind;

    /// <summary>
    /// The plain face a release left on a panel that stayed open: the helper copies it when the
    /// button comes up, wherever that happens, and it stays until the panel goes (FND-UI-062).
    /// </summary>
    private (Rectangle Face, ClientScreen Screen, HeldButtonKind Kind)? _releasedPanelFace;

    /// <summary>
    /// The point the information panels' hover tooltips (DEV-UI-005) explain: none while one of
    /// their faces is held, since the held-button helper draws nothing but the face until the
    /// release (FND-UI-046, EXP-UI-041).
    /// </summary>
    private Point? TooltipHoverPoint => _pressedPanelFace is null ? _hoverPoint : null;

    /// <summary>
    /// A press on a panel whose close face goes through the held-button helper: on the face it
    /// plays slot 3 and holds the face until the release, outside the panel it is refused with
    /// slot 4, and elsewhere inside it does nothing (SCR-GANG-001, SCR-GANG-002, SCR-UI-005,
    /// FND-UI-067). The held-button helper plays slot 3 when the press starts, whether or not the
    /// release lands inside the face (FND-AUDIO-011).
    /// </summary>
    private void PressPanelFace(Point point, Rectangle panel, Rectangle face, Action close,
        HeldButtonKind kind = HeldButtonKind.Confirm)
    {
        if (face.Contains(point))
            HoldPanelFace(face, kind, close);
        else if (!panel.Contains(point))
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
    }

    /// <summary>
    /// Holds a face in the held-button helper (FND-UI-062): it draws the lit face of
    /// <paramref name="kind"/> while the pointer is over it, and <paramref name="close"/> runs on a
    /// release inside it.
    /// </summary>
    private void HoldPanelFace(Rectangle face, HeldButtonKind kind, Action close)
    {
        AcceptInput();
        _pressedPanelFace = (face, _screens.Current, close);
        _pressedPanelFaceKind = kind;
    }

    /// <summary>
    /// SCR-UI-005, SCR-UI-006, SCR-UI-007, SCR-UI-008, SCR-GANG-001, SCR-GANG-002, SCR-FINANCE-001,
    /// SCR-OBJECTIVE-001, SCR-COMBAT-001, SCR-HIRE-001, SCR-COMLINK-001, SCR-OPTIONS-001 and
    /// SCR-SEARCH-001 (FND-UI-062, FND-UI-067): the face the held-button helper copies to the window
    /// over a held face, lit while the pointer is over it and plain while it is off it, and the
    /// plain face a release left on a panel that stayed open.
    /// </summary>
    private void DrawHeldPanelFace(Viewport viewport)
    {
        var sprites = UiSprites;
        if (_batch is null || sprites is null) return;
        (Rectangle Destination, Rectangle Source)? drawn = null;
        if (_pressedPanelFace is { } held && _pressedPanelFaceKind is { } kind
            && held.Screen == _screens.Current)
            drawn = HeldButtonFaces.Drawn(kind, held.Face,
                pointerInside: _hoverPoint is { } hover && held.Face.Contains(hover));
        else if (_releasedPanelFace is { } released && released.Screen == _screens.Current)
            drawn = HeldButtonFaces.Drawn(released.Kind, released.Face, pointerInside: false);
        if (drawn is not { } face) return;
        _batch.Begin(samplerState: SamplerState.PointClamp, transformMatrix: VirtualInput.Transform(viewport));
        _batch.Draw(sprites, face.Destination, face.Source, Color.White);
        _batch.End();
    }

    /// <summary>Enter, or the Execute key (virtual key 0x2B), which the original's panels also take.</summary>
    private bool PressedEnterOrExecute(KeyboardState keyboard) =>
        Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute);

    /// <summary>
    /// The pointer while the music fade blocks game events: a move shows the arrow, and a button let
    /// go lets go of what its press holds without completing it.
    /// </summary>
    /// <remarks>
    /// FND-AUDIO-016: the fade dispatches window messages without running the game's event step.
    /// Each pointer message the window handles selects the arrow (RULE-UI-007), so a pointer moved
    /// during a fade that holds a computer's planning back shows the arrow until that planning
    /// selects the hourglass again. A release does nothing in the game, but the press still has to
    /// end, or the control would stay held until some later release, possibly on another screen. It
    /// ends as a release outside the window does, which every held control already takes as a
    /// cancel.
    /// </remarks>
    private void UpdatePointerDuringFade(MouseState mouse)
    {
        if (mouse.Position != _previousMouse.Position) _pointer.PointerMoved();
        if (PointerButtonEdges.Released(mouse.LeftButton, _previousMouse.LeftButton))
            CompletePointerRelease(pointerMapped: false, Point.Zero, rightButton: false);
        if (PointerButtonEdges.Released(mouse.RightButton, _previousMouse.RightButton))
            CompletePointerRelease(pointerMapped: false, Point.Zero, rightButton: true);
    }

    /// <summary>
    /// A button released: completes what its press holds. Only a console tile pressed with the right
    /// button waits on that button (FND-UI-063); every other held control follows the left one.
    /// </summary>
    private void CompletePointerRelease(bool pointerMapped, Point point, bool rightButton)
    {
        if (_pressedCityConsoleControl is not null && _pressedCityConsoleByRightButton)
        {
            if (!rightButton) return;
            if (pointerMapped) CompleteCityConsolePress(point);
            else CancelCityConsolePress();
            return;
        }

        if (_pressedPanelFace is { } pressedFace)
        {
            if (rightButton) return;
            _pressedPanelFace = null;
            // FND-UI-062: the helper copies the plain face when the button comes up; a release
            // inside then closes the panel, and any other leaves the face on it.
            if (_pressedPanelFaceKind is { } kind && pressedFace.Screen == _screens.Current)
                _releasedPanelFace = (pressedFace.Face, pressedFace.Screen, kind);
            if (pointerMapped && pressedFace.Screen == _screens.Current
                && pressedFace.Face.Contains(point))
                pressedFace.Close();
            return;
        }

        if (rightButton) return;

        if (_handoffReadyHeld)
        {
            if (pointerMapped) CompleteHandoffReady(point);
            else _handoffReadyHeld = false;
            return;
        }

        if (_pressedSetupButton is not null)
        {
            if (pointerMapped) CompleteSetupButton(point);
            else _pressedSetupButton = null;
            return;
        }

        if (_pressedSetupPanelControl is not null)
        {
            if (pointerMapped) CompleteSetupPanelControl(point);
            else _pressedSetupPanelControl = null;
            return;
        }

        if (_pressedCityConsoleControl is not null)
        {
            if (pointerMapped) CompleteCityConsolePress(point);
            else CancelCityConsolePress();
            return;
        }

        if (_pressedComlinkSendButton is not null)
        {
            if (pointerMapped) CompleteComlinkSendButton(point);
            else CancelComlinkSendButton();
            return;
        }

        if (_pressedAttackFace is not null)
        {
            if (pointerMapped) CompleteAttackFace(point);
            else CancelAttackFace();
            return;
        }

        if (_pressedEventsButton is not null)
        {
            if (pointerMapped) CompleteEventsButton(point);
            else CancelEventsButton();
            return;
        }

        if (_pressedCommandPanelButton is not null)
        {
            if (pointerMapped) CompleteCommandPanelButton(point);
            else CancelCommandPanelButton();
            return;
        }

        if (_pressedHireRejectSlot is not null)
        {
            if (pointerMapped) CompleteHireReject(point);
            else CancelHireReject();
            return;
        }

        if (_draggedSetupPlayerSlot is not null)
        {
            if (pointerMapped && _setupPlayerDragStarted) CompleteSetupPlayerDrag(point);
            else if (pointerMapped) CompleteSetupPlayerClick();
            else CancelSetupPlayerDrag();
            return;
        }

        if (_draggedHireDefinitionId is not null)
        {
            if (pointerMapped && _hireDragStarted) CompleteHireDrag(point);
            else if (pointerMapped) CompleteHireClick();
            else CancelHireDrag();
            return;
        }

        if (_draggedGangId is not null)
        {
            if (pointerMapped && _gangDragStarted) CompleteGangDrag(point);
            else if (pointerMapped) CompleteGangClick();
            else CancelGangDrag();
        }
    }
}
