using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>Virtual-resolution pointer position shared by every hover tooltip; null when off-screen.</summary>
    private Point? _hoverPoint;

    /// <summary>The sector whose owner the pointer has rested on (DEV-UI-005).</summary>
    private readonly HoverDwellTracker _sectorOwnerDwell = new();

    /// <summary>
    /// Stores the pointer position for this frame and advances the dwell timers of the tooltips
    /// that wait for the cursor to settle before they explain themselves.
    /// </summary>
    private void UpdateHoverPoint(Point? point)
    {
        _hoverPoint = point;
        UpdateCommandTooltipDwell();
        _sectorOwnerDwell.Update(HoveredOwnerSector(), _inputTime);
    }

    /// <summary>
    /// The sector whose owner the pointer shows: a cell of the city map, or on the sector view a
    /// cell of the nine-sector display or the owner strip. None while something is dragged or a
    /// prompt is open, so the tooltip never covers a drop.
    /// </summary>
    private int? HoveredOwnerSector()
    {
        if (_state is null || _hoverPoint is not { } hover || _gameMenuOpen || _idleGangWarningOpen
            || _draggedHireDefinitionId is not null || _draggedGangId is not null)
            return null;
        return _screens.Current switch
        {
            ClientScreen.City when CityMapLayout.TrySectorAt(hover, out var sector) => sector,
            ClientScreen.Sector when SectorDetailLayout.OwnerStrip.Contains(hover) => _cursor,
            ClientScreen.Sector when SectorDetailLayout.TrySectorAt(hover, _cursor, out var sector) => sector,
            _ => null
        };
    }

    /// <summary>DEV-UI-005: names the owner of the sector the pointer has rested on.</summary>
    private void DrawSectorOwnerTooltip(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_state is { } state && _sectorOwnerDwell.SettledRegion is { } sector
            && sector == HoveredOwnerSector() && _hoverPoint is { } hover)
            DrawHoverTooltip(batch, pixel, font, hover,
                SectorOwnerText.Tooltip(state, ViewingPlayer(state), sector));
    }
}
