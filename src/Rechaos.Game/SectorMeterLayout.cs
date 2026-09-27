using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static partial class SectorDetailLayout
{
    /// <summary>SCR-UI-004, FND-UI-015: the group order strip, one-off on the left.</summary>
    public static Rectangle GroupOrderStrip => new(253, 61, 152, 16);

    /// <summary>FND-UI-015: only a press beyond x 367, the strip's last quarter, is recurring.</summary>
    public static bool GroupOrderIsRecurring(Point point) => point.X > 367;

    public static int SiteControlWidth(int baseResistance, int remainingResistance)
    {
        if (baseResistance < 0) throw new ArgumentOutOfRangeException(nameof(baseResistance));
        if (baseResistance == 0) return 100;
        var progress = baseResistance - Math.Clamp(remainingResistance, 0, baseResistance);
        return progress * 100 / baseResistance;
    }
}
