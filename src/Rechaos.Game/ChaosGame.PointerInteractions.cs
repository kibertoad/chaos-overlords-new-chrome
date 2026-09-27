using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>The close face held down on an information panel, and what releasing it does.</summary>
    private (Rectangle Face, ClientScreen Screen, Action Close)? _pressedPanelFace;

    /// <summary>
    /// A press on an information panel: on the close face it holds the face until the release,
    /// outside the panel it is refused with slot 4, and elsewhere inside it does nothing
    /// (SCR-GANG-001, SCR-GANG-002, SCR-UI-005).
    /// </summary>
    private void PressPanelFace(Point point, Rectangle panel, Rectangle face, Action close)
    {
        if (face.Contains(point))
            _pressedPanelFace = (face, _screens.Current, close);
        else if (!panel.Contains(point))
            PlayGeneralSound(GeneralSoundSlot.RejectedInput);
    }

    /// <summary>Enter, or the Execute key (virtual key 0x2B), which the original's panels also take.</summary>
    private bool PressedEnterOrExecute(KeyboardState keyboard) =>
        Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Execute);

    private void CompletePointerRelease(bool pointerMapped, Point point)
    {
        if (_pressedPanelFace is { } pressedFace)
        {
            _pressedPanelFace = null;
            if (pointerMapped && pressedFace.Screen == _screens.Current
                && pressedFace.Face.Contains(point))
                AcceptAndInvoke(pressedFace.Close);
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
