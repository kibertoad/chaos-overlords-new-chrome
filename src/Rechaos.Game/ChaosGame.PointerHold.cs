using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// The left button is still down at <paramref name="point"/>: a setup card, hire offer or
    /// gang pressed earlier starts its drag once the pointer has moved far enough from the press.
    /// </summary>
    private void HoldPointerAt(Point point)
    {
        HoldSetupName(point);
        // FND-UI-062: a held panel face the pointer leaves gets its plain face.
        if (_pressedCommandPanelButton is { } held && !CommandPanelFaces.Hit(held).Contains(point))
            LeaveCommandPanelFacePlain(held);
        if (_draggedSetupPlayerSlot is not null && !_setupPlayerDragStarted
            && PlayerPortraitLayout.SetupDragMoved(_setupPlayerPressPoint, point))
        {
            _setupPlayerDragStarted = true;
            _message = string.Empty;
        }
        else if (_draggedHireDefinitionId is not null && !_hireDragStarted
                 && DragMoved(_hirePressPoint, point))
        {
            _hireDragStarted = true;
            _message = string.Empty;
        }
        else if (_draggedGangId is not null && !_gangDragStarted
                 && GangDragMoved(_gangPressPoint, point))
        {
            StartGangDrag();
            _message = string.Empty;
        }
    }
}
