using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void CompletePointerRelease(bool pointerMapped, Point point)
    {
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
