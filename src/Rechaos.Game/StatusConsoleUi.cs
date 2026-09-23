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

    // The template prints its four-cell CASH label at LabelLeft; the value fills the rest of the row.
    public const int CashValueMaxCharacters = (ValueRight - LabelLeft) / OriginalFontLayout.CellWidth - 4;

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
    public const int EnemyChaosWarningRow = 16;
    public const string EnemyChaosWarning = "ENEMY GANGS MAY ADD MORE CHAOS.";

    public static IReadOnlyList<string> At(Point point)
        => At(point, null, GameDuration.SixMonths);

    public static bool Contains(Point point) =>
        StatusConsoleLayout.Scenario.Contains(point)
        || StatusConsoleLayout.Score.Contains(point)
        || StatusConsoleLayout.Cash.Contains(point)
        || Enumerable.Range(0, 5).Any(row => StatusConsoleLayout.SectorEntry(row).Contains(point));

    public static IReadOnlyList<string> At(
        Point point,
        ScenarioId? scenario,
        GameDuration duration,
        int? tolerance = null,
        ChaosRangeEstimate? chaosEstimate = null,
        IReadOnlyList<string>? chaosBreakdown = null,
        bool enemyGangsPresent = false)
    {
        if (scenario is { } mode && StatusConsoleLayout.Scenario.Contains(point))
            return ScenarioSetupTooltip.Lines(mode, duration);
        if (StatusConsoleLayout.Score.Contains(point))
            return ["SCORE", "CURRENT SCENARIO PROGRESS USED FOR RANKING AND VICTORY."];
        if (StatusConsoleLayout.Cash.Contains(point))
            return [
                "CASH  N [UNSPENT] (DELTA)",
                "",
                "N: MONEY ON HAND RIGHT NOW.",
                "",
                "[UNSPENT]: CASH LEFT AFTER QUEUED BRIBES AND EQUIPS.",
                "",
                "(DELTA): ESTIMATED CHANGE OVER THE WHOLE TURN."
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
                ? Tolerance(value, estimate, chaosBreakdown ?? [], enemyGangsPresent)
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
        bool enemyGangsPresent = false)
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
            "ONLY ROLLS OF 5+ ADD CHAOS AND CASH.",
            "ITEMS AND LOCAL SITES MODIFY THE ROLL POOL.",
            "",
            "CONTROLLED: EACH SUCCESS PAYS $1.",
            "UNCONTROLLED: HALF THE COMBINED",
            "SUCCESSES, ROUNDED DOWN.",
            "CRACKDOWN: NO CHAOS CASH PAID.",
            ""
        ];
        if (enemyGangsPresent) lines.Add(EnemyChaosWarning);
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
        bool enemyGangsPresent = false) =>
        Tolerance(tolerance, new ChaosRangeEstimate(chaosRange, []), [], enemyGangsPresent);

    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines) =>
        HoverTooltipLayout.Bounds(point, lines);
}

public static class StatusConsolePresentation
{
    public static Color QueuedChaosRangeColor(ChaosRange range, int tolerance) =>
        range.CanTriggerCrackdown(tolerance) ? Color.Red : Color.Lime;

    public static string ProjectedChange(int projectedChange) =>
        projectedChange.ToString("+#;-#;0");

    /// <summary>
    /// The status-console cash row, <c>CASH 20 [18] (+1)</c>: cash on hand, unspent cash in
    /// brackets and the whole-turn delta in parentheses. The spaces are dropped when the spaced
    /// form would run into the template's CASH label.
    /// </summary>
    public static string CashSummary(int cash, int unspent, int projectedChange)
    {
        var delta = ProjectedChange(projectedChange);
        var spaced = $"{cash} [{unspent}] ({delta})";
        return spaced.Length <= StatusConsoleLayout.CashValueMaxCharacters
            ? spaced
            : $"{cash}[{unspent}]({delta})";
    }

    /// <summary>
    /// The cash tooltip above the queued purchase list: one blank-line separated section per
    /// figure of <see cref="CashSummary"/>, each saying what the figure means and how it is
    /// calculated from the current orders.
    /// </summary>
    public static IReadOnlyList<string> CashTooltip(
        int cash, IReadOnlyList<QueuedCashSpend> spends, FinanceProjection projection)
    {
        ArgumentNullException.ThrowIfNull(spends);
        ArgumentNullException.ThrowIfNull(projection);
        var bribes = spends.Where(spend => spend.Action == GangAction.Bribe).Sum(spend => spend.Price);
        var equips = spends.Where(spend => spend.Action == GangAction.Equip).Sum(spend => spend.Price);
        var unspent = checked(cash - bribes - equips);
        var delta = ProjectedChange(projection.CashAdjustment);
        List<string> lines =
        [
            $"CASH  {CashSummary(cash, unspent, projection.CashAdjustment)}",
            "",
            $"{cash} - CASH: MONEY ON HAND RIGHT NOW.",
            "",
            $"[{unspent}] - UNSPENT: CASH LEFT AFTER QUEUED BRIBES AND EQUIPS.",
            $"  {cash} CASH - {bribes} BRIBES - {equips} EQUIPS = {unspent}",
            "  BRIBES PAY FIRST, THEN EQUIPS IN SUBMISSION ORDER.",
            "  NO CASH IS RESERVED; EARLIER SELLS MAY FUND EQUIPS.",
            "  BELOW ZERO, A QUEUED PURCHASE MAY FAIL.",
            "",
            $"({delta}) - DELTA: ESTIMATED CHANGE OVER THE WHOLE TURN."
        ];
        var components = DeltaComponents(projection).Where(component => component.Value != 0).ToArray();
        if (components.Length == 0) lines.Add("  NO PROJECTED INCOME OR COSTS.");
        lines.AddRange(components.Select(component =>
            $"  {component.Label,-18}{ProjectedChange(component.Value)}"));
        lines.Add($"  {"TOTAL",-18}{delta}");
        lines.Add("  TAX, SITE CASH AND CHAOS ARRIVE AFTER PURCHASES,");
        lines.Add("  SO A POSITIVE DELTA CANNOT PAY FOR AN EQUIP.");
        lines.Add("");
        lines.Add("QUEUED SPENDING IN RESOLUTION ORDER:");
        return lines;
    }

    private static IEnumerable<(string Label, int Value)> DeltaComponents(FinanceProjection projection) =>
    [
        ("GANG UPKEEP", projection.GangUpkeep),
        ("NEW CONTRACTS", projection.NewContracts),
        ("EQUIPMENT", projection.Equipment),
        ("CITY OFFICIALS", projection.CityOfficials),
        ("SECTOR TAX", projection.SectorTax),
        ("SITE CASH", projection.SiteProtection),
        ("CHAOS ESTIMATE", projection.ChaosEstimate)
    ];

    public static IReadOnlyList<QueuedCashSpend> QueuedCashSpends(
        MatchState state, MatchPlayerState player) => state.Commands.ExecutionPlan()
        // The plan is ordered by phase and then by sequence, so Instant Bribes precede every
        // Transaction Equip and each group keeps the submission order the resolver uses.
        .Where(entry => entry.Command.Player == player.Id
            && entry.Command.Action is GangAction.Bribe or GangAction.Equip)
        .Select((entry, index) =>
        {
            var gang = state.FindGang(entry.Command.Gang)!;
            var gangName = state.Definitions.Gang(gang.DefinitionId).Name;
            if (entry.Command.Action == GangAction.Bribe)
                return new QueuedCashSpend(index + 1, gang.Id, gangName, GangAction.Bribe,
                    "BRIBE", CommandRules.ByAction[GangAction.Bribe].CashCost);
            var item = state.Definitions.Items[entry.Command.Target.Id];
            return new QueuedCashSpend(index + 1, gang.Id, gangName, GangAction.Equip,
                item.Name, SpecialSiteRules.EquipmentCost(state, gang, item));
        }).ToArray();

    public static int UnspentCash(MatchState state, MatchPlayerState player) =>
        UnspentCash(player, QueuedCashSpends(state, player));

    public static int UnspentCash(MatchPlayerState player, IEnumerable<QueuedCashSpend> spends) =>
        checked(player.Cash - spends.Sum(entry => entry.Price));

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
            var name = state.Definitions.Gang(gang.DefinitionId).Name;
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

public sealed record QueuedCashSpend(
    int Position, GangId Gang, string GangName, GangAction Action, string Description, int Price);

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
