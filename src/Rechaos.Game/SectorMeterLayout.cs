namespace Rechaos.Game;

public static partial class SectorDetailLayout
{
    public static int SiteControlWidth(int baseResistance, int remainingResistance)
    {
        if (baseResistance < 0) throw new ArgumentOutOfRangeException(nameof(baseResistance));
        if (baseResistance == 0) return 100;
        var progress = baseResistance - Math.Clamp(remainingResistance, 0, baseResistance);
        return progress * 100 / baseResistance;
    }
}
