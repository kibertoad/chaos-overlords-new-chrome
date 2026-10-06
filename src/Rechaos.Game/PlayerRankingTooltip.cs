using System.Globalization;
using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Explains a Ranking-screen portrait: what the scenario rates overlords on, how the hovered
/// overlord's score is built from their holdings, and where every active overlord stands.
/// The score is the one stored when the last turn ended, or at setup during the first turn
/// (RULE-OBJECTIVE-002); the breakdown restates the inputs that feed it as they stand now, which
/// the next evaluation will score.
/// </summary>
/// <remarks>
/// On a seat's view (<see cref="MatchState.ViewedBy"/>) the seat knows its own score and holdings,
/// who owns each sector and who is still in the match, and of the other seats' scores only where
/// the rail places them (<see cref="SeatKnowledge.KnowsTotals"/>). The view's scores for the other
/// seats only reproduce the rail, so the tooltip names those scores and the hidden holdings as not
/// shown, and gives the place of a portrait that shares its height with others as the range of
/// places it can hold.
/// </remarks>
public static class PlayerRankingTooltip
{
    private const int NameWidth = 16;

    /// <summary>The tooltip for the portrait under <paramref name="point"/>, or none.</summary>
    public static IReadOnlyList<string> At(Point point, MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        return At(point, state, PlayerRankingPresentation.Project(state));
    }

    /// <summary>
    /// The tooltip for the portrait under <paramref name="point"/> among standings the caller has
    /// already projected, so the panel that draws the portraits does not rank the match twice a frame.
    /// </summary>
    public static IReadOnlyList<string> At(
        Point point, MatchState state, IReadOnlyList<PlayerRankingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entries);
        var hovered = entries.FirstOrDefault(entry =>
            PlayerRankingLayout.Portrait(entry).Contains(point));
        return hovered is null ? [] : Lines(state, hovered, entries);
    }

    public static IReadOnlyList<string> Lines(
        MatchState state, PlayerRankingEntry entry, IReadOnlyList<PlayerRankingEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entries);
        var player = state.FindPlayer(entry.Player)
            ?? throw new ArgumentException("Player is not in the match.", nameof(entry));
        var shared = entries.Any(other => other.Player != entry.Player && other.Score == entry.Score);
        var lines = new List<string>
        {
            player.Setup.Name.ToUpperInvariant(),
            state.ViewedBy is null
                ? $"PLACE {entry.Standing + 1} OF {entries.Count}{(shared ? " (TIED)" : "")}"
                : $"PLACE {ViewPlace(entry, entries)} OF {entries.Count}{(shared ? " (SAME HEIGHT)" : "")}",
            $"{ScenarioCatalog.Get(state.Setup.Scenario).Name} RATES: {Basis(state.Setup.Scenario)}",
            SeatKnowledge.KnowsTotals(state, entry.Player)
                ? ScoreLine(state, entry.Score)
                : $"SCORE: {NotShown}",
            "NOW:"
        };
        lines.AddRange(Breakdown(state, player));
        lines.Add("");
        lines.Add("ALL SCORES:");
        var places = entries.ToDictionary(candidate => candidate.Player, candidate => (state.ViewedBy is null
            ? (candidate.Standing + 1).ToString(CultureInfo.InvariantCulture)
            : ViewPlace(candidate, entries)) + ".");
        var placeWidth = places.Values.Max(place => place.Length);
        lines.AddRange(entries
            .OrderBy(candidate => candidate.Standing)
            .ThenBy(candidate => candidate.Player.Value)
            .Select(candidate => StandingRow(state, candidate, places[candidate.Player].PadRight(placeWidth),
                candidate.Player == entry.Player)));
        // SCR-OBJECTIVE-001: the height on the rail follows the score, not the place.
        lines.Add("");
        lines.Add("THE LEADER TOPS ITS RAIL. EACH OTHER");
        lines.Add("PORTRAIT SITS LOWER BY HOW FAR ITS");
        lines.Add("SCORE TRAILS; EQUAL SCORES, EQUAL HEIGHT.");
        if (state.ViewedBy is not null)
        {
            // SeatView.Project: the rail spreads the scores over 140 pixels, and that is all a
            // seat learns of the other seats' scores.
            lines.Add("YOU SEE ONLY YOUR OWN SCORE. A RAIL");
            lines.Add("PLACES THE OTHERS TO 1/140 OF THE");
            lines.Add("SPREAD FROM HIGHEST TO LOWEST, SO");
            lines.Add("ONE HEIGHT MAY HOLD UNEQUAL SCORES.");
        }
        return lines;
    }

    /// <summary>What the tooltip says on a seat's view in place of a value the view does not hold.</summary>
    public const string NotShown = "NOT SHOWN";

    // On a view the scores order the portraits only as the rail does, so a portrait that shares
    // its height with others may hold any of their places: "2-3" for two level at second place.
    private static string ViewPlace(PlayerRankingEntry entry, IReadOnlyList<PlayerRankingEntry> entries)
    {
        var first = entries.Count(other => other.Score > entry.Score) + 1;
        var last = first + entries.Count(other => other.Score == entry.Score) - 1;
        return first == last
            ? first.ToString(CultureInfo.InvariantCulture)
            : $"{first}-{last}";
    }

    /// <summary>
    /// The stored score and when it was stored: no turn has ended during turn 1, so the score
    /// there is the one recorded at setup.
    /// </summary>
    private static string ScoreLine(MatchState state, long score) =>
        $"SCORE: {Number(score)} " + (state.Coordinator.Turn == 1 ? "AT MATCH START" : "AT LAST TURN'S END");

    /// <summary>What <paramref name="scenario"/> rates overlords on, as the standing score states it.</summary>
    public static string Basis(ScenarioId scenario) => scenario switch
    {
        ScenarioId.Greed => "CASH ON HAND",
        ScenarioId.Power => "SECTORS CONTROLLED",
        ScenarioId.Big40 => "SECTORS CONTROLLED (GOAL 40)",
        ScenarioId.Armageddon => "SECTORS CONTROLLED (GOAL 64)",
        ScenarioId.Acceptance => "SUPPORT",
        ScenarioId.Dominance => "WEIGHTED CASH, SUPPORT, SECTORS",
        // RULE-OBJECTIVE-002: Kill 'Em All and Eliminate (4 and 7) count the inactive seats,
        // Siege (6) the headquarters sectors held.
        ScenarioId.KillEmAll or ScenarioId.Eliminate => "OVERLORD SEATS NO LONGER ACTIVE",
        ScenarioId.Siege => "HQ SECTORS CONTROLLED (OF 6)",
        ScenarioId.BigMan => "BIG MAN POINTS (GOAL 40)",
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };

    // Every seat knows who owns each sector and who is still in the match (SCR-UI-003), so the
    // sector, headquarters and seat counts are shown for every player. Cash, and the Support of
    // the completed sites in a player's sectors, only the player itself knows
    // (SeatKnowledge.KnowsTotals, SeatKnowledge.KnowsSites).
    private static IEnumerable<string> Breakdown(MatchState state, MatchPlayerState player)
    {
        var holdings = MatchOutcomeEvaluator.Project(state, player);
        var sectors = holdings.ControlledSectors;
        var known = SeatKnowledge.KnowsTotals(state, player.Id);
        var support = EndgameRankingEvaluator.CompletedSiteSupport(state, player);
        switch (state.Setup.Scenario)
        {
            case ScenarioId.Greed:
                yield return $"  CASH: {(known ? Money(player.Cash) : NotShown)}";
                break;
            case ScenarioId.Power or ScenarioId.Big40 or ScenarioId.Armageddon:
                yield return $"  SECTORS: {sectors} OF {MatchLimits.SectorCount}";
                break;
            case ScenarioId.Acceptance:
                yield return $"  SUPPORT: {(known ? Number(support) : NotShown)}";
                break;
            case ScenarioId.Dominance when !known:
                var viewWeights = ScenarioCatalog.Weights(state.Setup.Duration);
                yield return HiddenWeightedRow("CASH", viewWeights.Cash);
                yield return HiddenWeightedRow("SUPPORT", viewWeights.Support);
                yield return WeightedRow("SECTORS", Number(sectors), sectors, viewWeights.ControlledSector);
                yield return $"  TOTAL {NotShown}";
                break;
            case ScenarioId.Dominance:
                var weights = ScenarioCatalog.Weights(state.Setup.Duration);
                yield return WeightedRow("CASH", Money(player.Cash), player.Cash, weights.Cash);
                yield return WeightedRow("SUPPORT", Number(support), support, weights.Support);
                yield return WeightedRow("SECTORS", Number(sectors), sectors, weights.ControlledSector);
                var total = (long)player.Cash * weights.Cash + (long)support * weights.Support
                    + (long)sectors * weights.ControlledSector;
                yield return $"  TOTAL {Number(total)} / 10 = {Number(total / 10)}";
                break;
            case ScenarioId.KillEmAll or ScenarioId.Eliminate:
                yield return $"  {MatchLimits.PlayerCount} SEATS - {holdings.OpponentsAlive + 1} ACTIVE OVERLORDS";
                yield return "  SHARED BY EVERY SURVIVING OVERLORD";
                break;
            case ScenarioId.Siege:
                yield return $"  HQ SECTORS HELD: {HeadquartersHeld(state, player.Id)} OF 6 (GOAL)";
                break;
            case ScenarioId.BigMan:
                yield return "  +1 PER CENTRAL SECTOR HELD EACH TURN";
                break;
        }
    }

    private static string WeightedRow(string label, string shown, long value, int weight) =>
        $"  {label,-8}{shown,10} X {weight,-5}= {Number(value * weight)}";

    private static string HiddenWeightedRow(string label, int weight) =>
        $"  {label,-8}{NotShown,10} X {weight}";

    private static string StandingRow(MatchState state, PlayerRankingEntry entry, string place, bool hovered)
    {
        var name = state.FindPlayer(entry.Player)!.Setup.Name.ToUpperInvariant();
        if (name.Length > NameWidth) name = name[..NameWidth];
        var score = SeatKnowledge.KnowsTotals(state, entry.Player) ? Number(entry.Score) : NotShown;
        return $"{(hovered ? ">" : " ")} {place} {name,-NameWidth} {score,10}";
    }

    private static int HeadquartersHeld(MatchState state, PlayerId player) =>
        OriginalCityGenerator.HeadquartersCandidates.Count(sector => state.Sectors[sector].Owner == player);

    private static string Money(long value) =>
        value < 0 ? "-$" + Number(-value) : "$" + Number(value);

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
