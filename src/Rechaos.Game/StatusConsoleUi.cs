using Microsoft.Xna.Framework;

namespace Rechaos.Game;

public static class StatusConsoleLayout
{
    public const int LabelLeft = 480;
    public const int ValueRight = 579;
    public const int ScenarioY = 3;
    public const int DateY = 15;
    public const int ScoreY = 24;
    public const int CashY = 42;

    public static int SectorValueY(int row)
    {
        if (row is < 0 or >= 5) throw new ArgumentOutOfRangeException(nameof(row));
        return 60 + row * 9;
    }

    public static Rectangle Score => Entry(ScoreY);
    public static Rectangle Cash => Entry(CashY);
    public static Rectangle ChaosLabel => new(476, SectorValueY(4) - 1, 44, 9);
    public static Rectangle SectorEntry(int row) => Entry(SectorValueY(row));

    private static Rectangle Entry(int y) => new(476, y - 1, 108, 9);
}

public static class StatusConsoleTooltip
{
    public static IReadOnlyList<string> At(Point point)
    {
        if (StatusConsoleLayout.Score.Contains(point))
            return ["SCORE", "CURRENT SCENARIO PROGRESS USED FOR RANKING AND VICTORY."];
        if (StatusConsoleLayout.Cash.Contains(point))
            return [
                "CASH / PROJECTED CHANGE",
                "FIRST VALUE IS AVAILABLE CASH; THE SIGNED VALUE IS CASHFLOW.",
                "IT INCLUDES QUEUED COSTS AND ESTIMATED CHAOS PROCEEDS."
            ];
        if (StatusConsoleLayout.SectorEntry(0).Contains(point))
            return ["SECTOR", "THE COORDINATES OF THE CURRENTLY SELECTED SECTOR."];
        if (StatusConsoleLayout.SectorEntry(1).Contains(point))
            return [
                "SECTOR INCOME",
                "ADDED TO EACH GANG'S CHAOS DICE IN THIS SECTOR.",
                "IT IS NOT PASSIVE CASH; CONTROL PAYS $1 SECTOR TAX."
            ];
        if (StatusConsoleLayout.SectorEntry(2).Contains(point))
            return ["TOLERANCE", "CHAOS ABOVE THIS VALUE TRIGGERS A POLICE CRACKDOWN."];
        if (StatusConsoleLayout.SectorEntry(3).Contains(point))
            return ["SUPPORT", "INFLUENCED-SITE SUPPORT ADDED AGAINST ENEMY CONTROL."];
        if (StatusConsoleLayout.SectorEntry(4).Contains(point))
            return ["CHAOS", "CHAOS SUCCESSES ACCUMULATED IN THIS SECTOR THIS TURN."];
        return [];
    }

    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines) =>
        HoverTooltipLayout.Bounds(point, lines);
}

public static class StatusConsolePresentation
{
    public static string Cash(int current, int projectedChange) =>
        $"{current} {projectedChange:+#;-#;0}";
}

public static class HoverTooltipLayout
{
    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        if (lines.Count == 0) return Rectangle.Empty;
        var width = Math.Min(VirtualInput.Width - 16,
            lines.Max(line => line.Length) * OriginalFontLayout.CellWidth + 16);
        var height = lines.Count * OriginalFontLayout.LineHeight + 16;
        var x = Math.Clamp(point.X + 10, 4, VirtualInput.Width - width - 4);
        var below = point.Y + 12;
        var y = below + height <= VirtualInput.Height - 4
            ? below
            : Math.Max(4, point.Y - height - 8);
        return new Rectangle(x, y, width, height);
    }
}
