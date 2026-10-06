using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>The close face held down on an information panel, and what releasing it does.</summary>
    private (Rectangle Face, ClientScreen Screen, Action Close)? _pressedPanelFace;

    /// <summary>Whether the right button holds <see cref="_pressedPanelFace"/>, so only its release lets go.</summary>
    private bool _pressedPanelFaceByRightButton;

    /// <summary>
    /// A press on an information panel: on the close face it plays slot 3 and holds the face until
    /// the release, outside the panel it is refused with slot 4, and elsewhere inside it does
    /// nothing (SCR-GANG-001, SCR-GANG-002, SCR-UI-005). The held-button helper plays slot 3 when
    /// the press starts, whether or not the release lands inside the face (FND-AUDIO-011).
    /// </summary>
    private void PressPanelFace(Point point, Rectangle panel, Rectangle face, Action close)
    {
        if (face.Contains(point))
        {
            AcceptInput();
            _pressedPanelFace = (face, _screens.Current, close);
            _pressedPanelFaceByRightButton = false;
        }
        else if (!panel.Contains(point))
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
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
    /// A button released: completes what its press holds. Only a face pressed with the right button
    /// waits on that button; every other held control follows the left one.
    /// </summary>
    private void CompletePointerRelease(bool pointerMapped, Point point, bool rightButton)
    {
        if (_pressedPanelFace is { } pressedFace)
        {
            if (_pressedPanelFaceByRightButton != rightButton) return;
            _pressedPanelFace = null;
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
