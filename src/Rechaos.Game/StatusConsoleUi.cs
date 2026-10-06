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
    // FND-UI-040: five six-pixel numeric cells at x 550 end at x 580, exclusive.
    public const int ValueRight = 580;
    public const int ScoreCells = 5;
    public const int ScoreLeft = ValueRight - ScoreCells * OriginalFontLayout.CellWidth;
    public const int ScenarioLeft = 481;
    public const int YearLeft = 481;
    public const int WeekLeft = 511;
    public const int RemainingTurnsLeft = 562;
    public const int CompleteLeft = 532;
    /// <summary>FND-UI-040: the string resource drawn at <see cref="CompleteLeft"/> in the final view.</summary>
    public const int CompleteString = 19;
    /// <summary>RULE-UI-011: the sector code and the four sector values start at x 568.</summary>
    public const int SectorValueLeft = 568;
    public const int ScenarioY = 6;
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

    public static Rectangle Scenario => Entry(ScenarioY);
    public static Rectangle Date => Entry(DateY);
    public static Rectangle Score => Entry(ScoreY);
    public static Rectangle Cash => Entry(CashY);
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
        || StatusConsoleLayout.Date.Contains(point)
        || StatusConsoleLayout.Score.Contains(point)
        || StatusConsoleLayout.Cash.Contains(point)
        || Enumerable.Range(0, 5).Any(row => StatusConsoleLayout.SectorEntry(row).Contains(point));

    /// <summary>
    /// The SCORE row's explanation. The row shows the scenario standing score the Ranking screen
    /// ranks by, which decides victory only where it is the objective's own statistic; Kill 'Em
    /// All and Siege rate every survivor by the same count of inactive seats.
    /// </summary>
    public static IReadOnlyList<string> ScoreLines(ScenarioId? scenario)
    {
        if (scenario is not { } mode)
            return ["SCORE", "CURRENT SCENARIO STANDING USED FOR RANKING."];
        var lines = new List<string>
        {
            "SCORE",
            $"{ExecutableStrings.ScenarioTitle(mode)} RATES: {PlayerRankingTooltip.Basis(mode)}",
            "USED FOR RANKING."
        };
        if (mode is ScenarioId.KillEmAll or ScenarioId.Eliminate)
            lines.Add("SHARED BY EVERY SURVIVING OVERLORD.");
        return lines;
    }

    /// <summary>
    /// The date row's explanation: the calendar (FND-UI-040) and, in a timed scenario, the
    /// countdown beside it, or the completion caption that replaces it in the final view.
    /// </summary>
    public static IReadOnlyList<string> DateLines(
        ScenarioId? scenario, GameDuration duration, bool complete = false)
    {
        var lines = new List<string>
        {
            "DATE",
            "YEAR AND WEEK. EACH TURN IS ONE WEEK,",
            $"{MatchCalendar.WeeksPerYear} WEEKS A YEAR FROM WEEK 1 OF {MatchCalendar.FirstYear}."
        };
        if (complete)
        {
            lines.Add("");
            lines.Add($"{ExecutableStrings.Get(StatusConsoleLayout.CompleteString)}: THE MATCH HAS ENDED.");
            return lines;
        }
        if (scenario is not { } mode) return lines;
        lines.Add("");
        if (StatusConsolePresentation.ShowsCountdown(mode))
        {
            lines.Add("THE NUMBER ON THE RIGHT COUNTS THE TURNS");
            lines.Add("LEFT AFTER THE ONE BEING PLANNED.");
            lines.Add($"THE MATCH ENDS AFTER TURN {ScenarioCatalog.Turns(duration)},");
            lines.Add("OR SOONER IF ONE OVERLORD IS LEFT.");
        }
        else
            lines.Add($"{ExecutableStrings.ScenarioTitle(mode)} HAS NO TIME LIMIT.");
        return lines;
    }

    public static IReadOnlyList<string> At(
        Point point,
        ScenarioId? scenario,
        GameDuration duration,
        int? tolerance = null,
        ChaosRangeEstimate? chaosEstimate = null,
        IReadOnlyList<string>? chaosBreakdown = null,
        bool enemyGangsPresent = false,
        ToleranceParts? toleranceParts = null,
        bool complete = false)
    {
        if (scenario is { } mode && StatusConsoleLayout.Scenario.Contains(point))
            return ScenarioSetupTooltip.Lines(mode, duration);
        if (StatusConsoleLayout.Date.Contains(point))
            return DateLines(scenario, duration, complete);
        if (StatusConsoleLayout.Score.Contains(point))
            return ScoreLines(scenario);
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
                ? Tolerance(value, estimate, chaosBreakdown ?? [], enemyGangsPresent, toleranceParts)
                : ["TOLERANCE", "CHAOS ABOVE THIS VALUE TRIGGERS A POLICE CRACKDOWN."];
        if (StatusConsoleLayout.SectorEntry(3).Contains(point))
            // RULE-CONTROL-001
            return [
                "SUPPORT",
                "COMPLETED-SITE SUPPORT, SET BEFORE PLANNING.",
                "A CHALLENGER'S CONTROL MUST BEAT INCOME PLUS SUPPORT."
            ];
        if (StatusConsoleLayout.SectorEntry(4).Contains(point))
            // RULE-UPKEEP-001, RULE-SITE-001
            return [
                "SECTOR CASH",
                "OWNER-ONLY UPKEEP: $1 TAX PLUS COMPLETED-SITE CASH,",
                "SET BEFORE PLANNING. A SECTOR TAKEN THIS TURN STILL",
                "PAYS ITS NEW OWNER THE OLD SITES' CASH ONCE."
            ];
        return [];
    }

    public static IReadOnlyList<string> Tolerance(
        int tolerance,
        ChaosRangeEstimate chaosEstimate,
        IReadOnlyList<string> chaosBreakdown,
        bool enemyGangsPresent = false,
        ToleranceParts? parts = null)
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
            "CRACKDOWN THIS TURN: NO CHAOS CASH.",
            ""
        ];
        if (enemyGangsPresent) lines.Add(EnemyChaosWarning);
        lines.Add(chaosEstimate.Range.CanTriggerCrackdown(tolerance)
            ? "YOUR RANGE CAN TRIGGER A CRACKDOWN."
            : "YOUR RANGE CANNOT TRIGGER A CRACKDOWN.");
        // RULE-POLICE-002, RULE-CHAOS-002
        lines.Add("THE THIRD CRACKDOWN IN 5 TURNS MAKES");
        lines.Add("THE SECTOR NEUTRAL AND BRINGS POLICE");
        lines.Add("FOR 3-5 TURNS. POLICE DO NOT STOP PAY.");
        lines.Add("");
        // RULE-SITE-001, RULE-TOLERANCE-001, RULE-TOLERANCE-002: the value is the base plus the
        // completed sites, set before planning; the base moves while the orders resolve.
        if (parts is { } toleranceParts)
        {
            lines.Add($"TOLERANCE {tolerance}: BASE {toleranceParts.Base} + SITES {toleranceParts.Sites}.");
            lines.Add($"BASE MOVES 1 PER TURN TOWARD {toleranceParts.NormalBase}");
            lines.Add("(17 - INCOME); BRIBE +3, SNITCH -3,");
            lines.Add("THEN KEPT WITHIN 1..40.");
            if (toleranceParts.Base + toleranceParts.Sites != tolerance)
                lines.Add("CHANGES SINCE PLANNING COUNT NEXT TURN.");
            lines.Add("");
        }
        lines.Add("CHAOS RANGE BREAKDOWN:");
        lines.AddRange(chaosBreakdown);
        return lines;
    }

    public static IReadOnlyList<string> Tolerance(
        int tolerance,
        ChaosRange chaosRange,
        bool enemyGangsPresent = false) =>
        Tolerance(tolerance, new ChaosRangeEstimate(chaosRange, []), [], enemyGangsPresent);

    /// <summary>The two parts a sector's Tolerance is rebuilt from, and where the base returns to.</summary>
    public readonly record struct ToleranceParts(int Base, int Sites, int NormalBase)
    {
        public static ToleranceParts Of(MatchState state, MatchSectorState sector) => new(
            sector.BaseTolerance,
            ToleranceResolver.SiteAdjustment(state, sector),
            ToleranceResolver.NormalBaseTolerance(sector));
    }

    public static Rectangle Bounds(Point point, IReadOnlyList<string> lines) =>
        HoverTooltipLayout.Bounds(point, lines);
}

public static class StatusConsolePresentation
{
    /// <summary>FND-UI-040, FND-OBJECTIVE-003: the planning-entry countdown excludes this turn.</summary>
    public static int? RemainingTurns(ScenarioId scenario, GameDuration duration, int currentTurn) =>
        ShowsCountdown(scenario)
            ? ScenarioCatalog.TurnLimit(scenario, duration) - currentTurn
            : null;

    /// <summary>
    /// Whether the date row draws the countdown beside the calendar. The date-row tooltip asks the
    /// same question, so it explains a countdown exactly when one is drawn.
    /// </summary>
    public static bool ShowsCountdown(ScenarioId scenario) => ScenarioCatalog.Get(scenario).IsTimed;

    /// <summary>
    /// The status-console SCORE row: the scenario score the last evaluation stored, when the match
    /// started or the last turn ended (RULE-OBJECTIVE-002), the same one the ranking screen ranks
    /// by, so objective scenarios show their standing (Big Man points, sectors controlled, HQ
    /// sectors held, or the inactive-seat count Kill 'Em All and Eliminate share) rather than only
    /// the timed scenarios' scores.
    /// </summary>
    public static int Score(MatchState state, MatchPlayerState player) => player.ScenarioScore;

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
        var bribes = spends.Where(spend => spend.Kind == QueuedSpendKind.Bribe).Sum(spend => spend.Price);
        var equips = spends.Where(spend => spend.Kind == QueuedSpendKind.Equip).Sum(spend => spend.Price);
        var hires = spends.Where(spend => spend.Kind == QueuedSpendKind.Hire).Sum(spend => spend.Price);
        var unspent = checked(cash - bribes - equips - hires);
        var delta = ProjectedChange(projection.CashAdjustment);
        List<string> lines =
        [
            $"CASH  {CashSummary(cash, unspent, projection.CashAdjustment)}",
            "",
            $"{cash} - CASH: MONEY ON HAND RIGHT NOW.",
            "",
            $"[{unspent}] - UNSPENT: CASH LEFT AFTER QUEUED BRIBES, EQUIPS AND HIRES.",
            "",
            $"  CASH {cash} - BRIBES {bribes} - EQUIPS {equips} - HIRES {hires} = UNSPENT {unspent}",
            "",
            "  BRIBES PAY FIRST, THEN EQUIPS IN SUBMISSION ORDER.",
            // RULE-TURN-002, RULE-HIRE-001: hire_phase comes after the transactions and the Chaos
            // payout, and a hire costing more than the cash left then fails.
            "  HIRES PAY LAST, AFTER CHAOS INCOME.",
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
        // RULE-TURN-002, RULE-UPKEEP-001: tax and site cash arrive at the next turn start and Chaos
        // income after the transactions, so the delta comes too late for this turn's purchases.
        lines.Add("  THE DELTA CANNOT PAY FOR EQUIPS OR NEW GANGS:");
        lines.Add("  ONLY UNSPENT CASH FROM THE TURN START CAN.");
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
        MatchState state, MatchPlayerState player)
    {
        // The plan is ordered by phase and then by sequence, so Instant Bribes precede every
        // Transaction Equip and each group keeps the submission order the resolver uses.
        var purchases = state.Commands.ExecutionPlan()
            .Where(entry => entry.Command.Player == player.Id
                && entry.Command.Action is GangAction.Bribe or GangAction.Equip)
            .Select(entry =>
            {
                var gang = state.FindGang(entry.Command.Gang)!;
                var gangName = state.Definitions.Gang(gang.DefinitionId).Name;
                if (entry.Command.Action == GangAction.Bribe)
                    return new QueuedCashSpend(0, gang.Id, gangName, QueuedSpendKind.Bribe,
                        "BRIBE", CommandRules.ByAction[GangAction.Bribe].CashCost);
                var item = state.Definitions.Items[entry.Command.Target.Id];
                return new QueuedCashSpend(0, gang.Id, gangName, QueuedSpendKind.Equip,
                    item.Name, SpecialSiteRules.EquipmentCost(state, gang, item));
            });
        // RULE-TURN-002, RULE-HIRE-001: hire_phase pays the hires after every purchase. A hire
        // whose cost was already paid takes no more cash.
        var hires = player.PendingHires.Where(hire => !hire.InitialCostPaid).Select(hire =>
        {
            var definition = state.Definitions.Gang(hire.GangDefinitionId);
            return new QueuedCashSpend(0, null, definition.Name, QueuedSpendKind.Hire,
                "HIRE", HireRules.InitialCost(definition));
        });
        return purchases.Concat(hires)
            .Select((spend, index) => spend with { Position = index + 1 }).ToArray();
    }

    public static int UnspentCash(MatchState state, MatchPlayerState player) =>
        UnspentCash(player, QueuedCashSpends(state, player));

    public static int UnspentCash(MatchPlayerState player, IEnumerable<QueuedCashSpend> spends) =>
        checked(player.Cash - spends.Sum(entry => entry.Price));

    /// <summary>
    /// RULE-UI-011: a Support or Cash value as the console shows it, the value to the sector's
    /// owner and 0 to everyone else. A neutral sector has no owner, so it shows 0.
    /// </summary>
    public static int OwnerOnly(PlayerId? owner, PlayerId activePlayer, int value) =>
        owner == activePlayer ? value : 0;

    /// <summary>RULE-UI-011: Income, Tolerance, Support and Cash of a sector as the console shows them.</summary>
    public static (int Income, int Tolerance, int Support, int Cash) SectorValues(
        MatchState state, PlayerId activePlayer, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var sector = state.Sectors[sectorId];
        return (sector.Income, sector.Tolerance,
            OwnerOnly(sector.Owner, activePlayer, sector.Support),
            OwnerOnly(sector.Owner, activePlayer, SectorIncomeResolver.SectorCash(state, sector)));
    }

    /// <summary>
    /// RULE-UI-011: the Income row is string resource <c>0x11 + income</c> cut to two characters.
    /// Generation gives Income 3 to 7, strings 20 to 24; 0 to 2 reach strings 17 to 19, and a
    /// value past the strings the table holds is drawn as nothing.
    /// </summary>
    public static string IncomeWord(int income) => income switch
    {
        0 => "PU",
        1 => "CY",
        2 => "CO",
        3 => "LO",
        4 => "LM",
        5 => "MI",
        6 => "UM",
        7 => "UP",
        _ => string.Empty
    };

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

public enum QueuedSpendKind
{
    Bribe,
    Equip,
    Hire
}

/// <summary>A queued purchase. A hire has no gang yet, so its <paramref name="Gang"/> is null.</summary>
public sealed record QueuedCashSpend(
    int Position, GangId? Gang, string GangName, QueuedSpendKind Kind, string Description, int Price);

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
