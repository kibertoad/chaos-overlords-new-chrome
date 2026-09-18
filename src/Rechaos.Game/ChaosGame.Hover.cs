using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>Virtual-resolution pointer position shared by every hover tooltip; null when off-screen.</summary>
    private Point? _hoverPoint;

    /// <summary>
    /// Stores the pointer position for this frame and advances the dwell timers of the tooltips
    /// that wait for the cursor to settle before they explain themselves.
    /// </summary>
    private void UpdateHoverPoint(Point? point)
    {
        _hoverPoint = point;
        UpdateCommandTooltipDwell();
    }
}
