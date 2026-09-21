using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class CityStatusMessage
{
    public const int MaxCharacters = 32;

    public static bool Fits(string message) =>
        (message ?? throw new ArgumentNullException(nameof(message))).Length <= MaxCharacters;

    public static string RequireFit(string message)
    {
        if (!Fits(message))
            throw new ArgumentException(
                $"City status messages cannot exceed {MaxCharacters} characters.", nameof(message));
        return message;
    }

    public static string Error(string message) =>
        RequireFit((message ?? throw new ArgumentNullException(nameof(message))).ToUpperInvariant());

    public static string Clip(string message) => Fits(message)
        ? message
        : message[..MaxCharacters];
}

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

    public static Rectangle Scenario => new(476, 0, 108, 10);
    public static Rectangle Score => Entry(ScoreY);
    public static Rectangle Cash => Entry(CashY);
    public static Rectangle CashLabel => new(476, SectorValueY(4) - 1, 44, 9);
    public static Rectangle SectorEntry(int row) => Entry(SectorValueY(row));

    private static Rectangle Entry(int y) => new(476, y - 1, 108, 9);
}

public static class StatusConsoleTooltip
{
    public const int QueuedChaosRangeRow = 4;
    public const string QueuedChaosRangePrefix = "YOUR QUEUED CHAOS: ";

    public static IReadOnlyList<string> At(Point point)
        => At(point, null, GameDuration.SixMonths);

    public static IReadOnlyList<string> At(
        Point point,
        ScenarioId? scenario,
        GameDuration duration,
        int? tolerance = null,
        ChaosRangeEstimate? chaosEstimate = null,
        IReadOnlyList<string>? chaosBreakdown = null,
        bool knownEnemyGangs = false)
    {
        if (scenario is { } mode && StatusConsoleLayout.Scenario.Contains(point))
            return ScenarioSetupTooltip.Lines(mode, duration);
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
            return tolerance is { } value && chaosEstimate is { } estimate
                ? Tolerance(value, estimate, chaosBreakdown ?? [], knownEnemyGangs)
                : ["TOLERANCE", "CHAOS ABOVE THIS VALUE TRIGGERS A POLICE CRACKDOWN."];
        if (StatusConsoleLayout.SectorEntry(3).Contains(point))
            return ["SUPPORT", "INFLUENCED-SITE SUPPORT ADDED AGAINST ENEMY CONTROL."];
        if (StatusConsoleLayout.SectorEntry(4).Contains(point))
            return [
                "SECTOR CASH",
                "OWNER-ONLY UPKEEP: $1 TAX PLUS COMPLETED-SITE CASH."
            ];
        return [];
    }

    public static IReadOnlyList<string> Tolerance(
        int tolerance,
        ChaosRangeEstimate chaosEstimate,
        IReadOnlyList<string> chaosBreakdown,
        bool knownEnemyGangs = false)
    {
        List<string> lines =
        [
            "TOLERANCE",
            "CHAOS ABOVE THIS VALUE TRIGGERS",
            "A POLICE CRACKDOWN.",
            "",
            $"{QueuedChaosRangePrefix}{chaosEstimate.Range.Minimum}-{chaosEstimate.Range.Maximum}",
            "",
            "SUCCESS RANGE FROM YOUR QUEUED ORDERS.",
            "EACH POINT ROLLS ONE STANDARD SIX-SIDED DIE.",
            "ONLY SUCCESSFUL ROLLS ADD CHAOS OR CASH.",
            "ITEMS AND LOCAL SITES MODIFY THE ROLL POOL.",
            "CONTROLLED: EACH SUCCESS PAYS $1.",
            "UNCONTROLLED: HALF THE COMBINED",
            "SUCCESSES, ROUNDED DOWN.",
            "CRACKDOWN: NO CHAOS CASH PAID.",
            ""
        ];
        if (knownEnemyGangs) lines.Add("KNOWN ENEMY GANGS MAY ADD MORE CHAOS.");
        lines.Add(chaosEstimate.Range.CanTriggerCrackdown(tolerance)
            ? "YOUR RANGE CAN TRIGGER A CRACKDOWN."
            : "YOUR RANGE CANNOT TRIGGER A CRACKDOWN.");
        lines.Add("");
        lines.Add("CHAOS RANGE BREAKDOWN:");
        lines.AddRange(chaosBreakdown);
        return lines;
    }

    public static IReadOnlyList<string> Tolerance(
        int tolerance,
        ChaosRange chaosRange,
        bool knownEnemyGangs = false) =>
        Tolerance(tolerance, new ChaosRangeEstimate(chaosRange, []), [], knownEnemyGangs);

    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines) =>
        HoverTooltipLayout.Bounds(point, lines);
}

public static class StatusConsolePresentation
{
    public static Color QueuedChaosRangeColor(ChaosRange range, int tolerance) =>
        range.CanTriggerCrackdown(tolerance) ? Color.Red : Color.Lime;

    public static string Cash(int current, int projectedChange) =>
        $"{current} {projectedChange:+#;-#;0}";

    public static int SectorCash(PlayerId? owner, PlayerId activePlayer, int cash) =>
        owner == activePlayer ? cash : 0;

    public static IReadOnlyList<string> ChaosBreakdown(
        MatchState state,
        ChaosRangeEstimate estimate)
    {
        ArgumentNullException.ThrowIfNull(state);
        var lines = estimate.Contributions.SelectMany(contribution =>
        {
            var gang = state.FindGang(contribution.Gang)!;
            var name = state.Definitions.Gangs.Single(
                definition => definition.Id == gang.DefinitionId).Name;
            var values = new List<string>
            {
                $"{name}: {contribution.Dice} D6 ROLLS",
                $"  {contribution.Income} INCOME + {contribution.Force} FORCE + {contribution.EffectiveChaos} CHAOS"
            };
            if (contribution.Dice != Math.Max(0, contribution.RawDice))
                values.Add($"  ADJUSTED FROM {contribution.RawDice} TO {contribution.Dice} ROLLS");
            return values;
        }).ToList();
        if (lines.Count == 0) lines.Add("NO QUEUED CHAOS GANGS.");
        return lines;
    }
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
