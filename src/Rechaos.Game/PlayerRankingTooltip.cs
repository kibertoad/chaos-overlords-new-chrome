using System.Globalization;
using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// Explains a Ranking-screen portrait: what the scenario rates overlords on, how the hovered
/// overlord's score is built from their holdings, and where every active overlord stands.
/// The totals are the canonical <see cref="EndgameRankingEvaluator"/> scores; the breakdown only
/// restates the inputs that feed them.
/// </summary>
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
        var tied = entries.Any(other => other.Player != entry.Player && other.Score == entry.Score);
        var lines = new List<string>
        {
            player.Setup.Name.ToUpperInvariant(),
            $"PLACE {entry.Standing + 1} OF {entries.Count}{(tied ? " (TIED)" : "")}",
            $"{ScenarioCatalog.Get(state.Setup.Scenario).Name} RATES: {Basis(state.Setup.Scenario)}",
            $"SCORE: {Number(entry.Score)}"
        };
        lines.AddRange(Breakdown(state, player));
        lines.Add("");
        lines.Add("ALL SCORES:");
        lines.AddRange(entries
            .OrderBy(candidate => candidate.Standing)
            .ThenBy(candidate => candidate.Player.Value)
            .Select(candidate => StandingRow(state, candidate, candidate.Player == entry.Player)));
        // SCR-OBJECTIVE-001: the height on the rail follows the score, not the place.
        lines.Add("");
        lines.Add("THE LEADER TOPS ITS RAIL. EACH OTHER");
        lines.Add("PORTRAIT SITS LOWER BY HOW FAR ITS");
        lines.Add("SCORE TRAILS; EQUAL SCORES, EQUAL HEIGHT.");
        return lines;
    }

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

    private static IEnumerable<string> Breakdown(MatchState state, MatchPlayerState player)
    {
        var holdings = MatchOutcomeEvaluator.Project(state, player);
        var sectors = holdings.ControlledSectors;
        switch (state.Setup.Scenario)
        {
            case ScenarioId.Greed:
                yield return $"  CASH: {Money(player.Cash)}";
                break;
            case ScenarioId.Power or ScenarioId.Big40 or ScenarioId.Armageddon:
                yield return $"  SECTORS: {sectors} OF {MatchLimits.SectorCount}";
                break;
            case ScenarioId.Acceptance:
                yield return $"  SUPPORT: {Number(player.Support)}";
                break;
            case ScenarioId.Dominance:
                var weights = ScenarioCatalog.Weights(state.Setup.Duration);
                yield return WeightedRow("CASH", Money(player.Cash), player.Cash, weights.Cash);
                yield return WeightedRow("SUPPORT", Number(player.Support), player.Support, weights.Support);
                yield return WeightedRow("SECTORS", Number(sectors), sectors, weights.ControlledSector);
                var total = (long)player.Cash * weights.Cash + (long)player.Support * weights.Support
                    + (long)sectors * weights.ControlledSector;
                yield return $"  TOTAL {Number(total)} / 10 = SCORE";
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

    private static string StandingRow(MatchState state, PlayerRankingEntry entry, bool hovered)
    {
        var name = state.FindPlayer(entry.Player)!.Setup.Name.ToUpperInvariant();
        if (name.Length > NameWidth) name = name[..NameWidth];
        return $"{(hovered ? ">" : " ")} {entry.Standing + 1}. {name,-NameWidth} {Number(entry.Score),10}";
    }

    private static int HeadquartersHeld(MatchState state, PlayerId player) =>
        OriginalCityGenerator.HeadquartersCandidates.Count(sector => state.Sectors[sector].Owner == player);

    private static string Money(long value) =>
        value < 0 ? "-$" + Number(-value) : "$" + Number(value);

    private static string Number(long value) => value.ToString("N0", CultureInfo.InvariantCulture);
}
