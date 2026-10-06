using System.Globalization;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The rebuild's tooltips that read values a seat's view sets to neutral (docs/MULTIPLAYER.md,
/// "Planning on a view"): the rankings tooltip (DEV-UI-005), the Bribe and Snitch tooltips and the
/// console's Tolerance tooltip (DEV-UI-007). On the view taken at every planning entry of every
/// recorded run, each shows what the whole match shows wherever the seat knows it, and does not
/// change when every value the seat does not know is changed.
/// </summary>
public sealed partial class OriginalNewGameExperimentTests
{
    [Theory]
    [MemberData(nameof(MatchingRuns))]
    public void TooltipsOnASeatsViewShowOnlyWhatTheSeatKnows(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var entries = 0;
        StartMatch(recorded, out _,
            atPlanningEntry: (match, human, _) =>
            {
                if (match.Coordinator.Phase != TurnPhase.Command || match.Coordinator.ActivePlayer != human
                    || match.Outcome is not null || match.FindPlayer(human)!.Status != PlayerStatus.Active)
                    return;
                entries++;
                var view = SeatView.Project(match, human);
                SeatViewTooltipAssertions.ShowWhatTheWholeMatchShows(match, view, human);
                var shown = SeatViewTooltipAssertions.Tooltips(view, human);
                SeatViewTooltipAssertions.ChangeWhatTheSeatDoesNotKnow(view, human);
                Assert.Equal(shown, SeatViewTooltipAssertions.Tooltips(view, human));
            });
        Assert.True(entries > 0 || recorded.DoneCount == 0, "No planning entry was viewed.");
    }
}

internal static class SeatViewTooltipAssertions
{
    private static readonly GangAction[] ToleranceActions = [GangAction.Bribe, GangAction.Snitch];

    /// <summary>Every line of every tooltip checked here, on <paramref name="state"/>.</summary>
    public static IReadOnlyList<string> Tooltips(MatchState state, PlayerId seat)
    {
        var lines = new List<string>();
        var entries = PlayerRankingPresentation.Project(state);
        foreach (var entry in entries)
            lines.AddRange(PlayerRankingTooltip.Lines(state, entry, entries));
        foreach (var gang in state.FindPlayer(seat)!.Gangs.Where(gang => gang.IsActive))
        foreach (var action in ToleranceActions)
            lines.AddRange(CommandActionTooltips.Lines(action, state, gang));
        foreach (var sector in state.Sectors)
            lines.AddRange(ToleranceTooltip(state, seat, sector));
        return lines;
    }

    public static void ShowWhatTheWholeMatchShows(MatchState whole, MatchState view, PlayerId seat)
    {
        var wholeEntries = PlayerRankingPresentation.Project(whole);
        var viewEntries = PlayerRankingPresentation.Project(view);
        Assert.Equal(wholeEntries.Select(entry => entry.Player), viewEntries.Select(entry => entry.Player));
        var ownScore = whole.FindPlayer(seat)!.ScenarioScore.ToString("N0", CultureInfo.InvariantCulture);
        for (var index = 0; index < wholeEntries.Count; index++)
        {
            var player = wholeEntries[index].Player;
            var wholeLines = PlayerRankingTooltip.Lines(whole, wholeEntries[index], wholeEntries);
            var viewLines = PlayerRankingTooltip.Lines(view, viewEntries[index], viewEntries);
            var header = wholeLines.ToList().IndexOf("ALL SCORES:");
            Assert.Equal(header, viewLines.ToList().IndexOf("ALL SCORES:"));
            Assert.Equal(wholeLines[0], viewLines[0]);
            // The true place is one of the places the rail leaves open.
            var places = viewLines[1].Split(' ')[1].Split('-').Select(int.Parse).ToArray();
            Assert.InRange(wholeEntries[index].Standing + 1, places[0], places[^1]);
            for (var line = 2; line < header; line++)
                if (player == seat || !viewLines[line].Contains(PlayerRankingTooltip.NotShown, StringComparison.Ordinal))
                    Assert.Equal(wholeLines[line], viewLines[line]);
            var rows = viewLines.Skip(header + 1).Take(viewEntries.Count).ToArray();
            Assert.Single(rows, row => !row.EndsWith(PlayerRankingTooltip.NotShown, StringComparison.Ordinal));
            Assert.Single(rows, row => row.EndsWith(" " + ownScore, StringComparison.Ordinal));
        }

        foreach (var gang in whole.FindPlayer(seat)!.Gangs.Where(gang => gang.IsActive))
        {
            var sector = whole.Sectors[gang.SectorId];
            foreach (var action in ToleranceActions)
            {
                var wholeLines = CommandActionTooltips.Lines(action, whole, gang);
                var viewLines = CommandActionTooltips.Lines(action, view, view.FindGang(gang.Id));
                if (sector.Owner == seat)
                {
                    Assert.Equal(wholeLines, viewLines);
                    continue;
                }
                Assert.Equal(CommandActionTooltips.Lines(action), viewLines.Take(CommandActionTooltips.Lines(action).Count));
                Assert.Contains($"THIS TURN'S CHAOS TEST USES {sector.Tolerance}; NORMAL BASE " +
                    $"{ToleranceResolver.NormalBaseTolerance(sector)}.", viewLines);
            }
        }

        foreach (var sector in whole.Sectors)
        {
            var wholeLines = ToleranceTooltip(whole, seat, sector).ToList();
            var viewLines = ToleranceTooltip(view, seat, view.Sectors[sector.Id]).ToList();
            if (sector.Owner == seat)
            {
                Assert.Equal(wholeLines, viewLines);
                continue;
            }
            // Only the parts line differs: what precedes it, and the rule and the breakdown after it.
            var parts = wholeLines.FindIndex(line => line.StartsWith($"TOLERANCE {sector.Tolerance}", StringComparison.Ordinal));
            var rule = wholeLines.FindIndex(line => line.StartsWith("BASE MOVES", StringComparison.Ordinal));
            Assert.Equal(wholeLines.Take(parts), viewLines.Take(parts));
            Assert.Equal($"TOLERANCE {sector.Tolerance} = BASE + SITES. ONLY THE", viewLines[parts]);
            var viewRule = viewLines.FindIndex(line => line.StartsWith("BASE MOVES", StringComparison.Ordinal));
            Assert.Equal(wholeLines.Skip(rule), viewLines.Skip(viewRule));
        }
    }

    /// <summary>
    /// Changes every value of <paramref name="view"/> that stands in for one the seat does not know:
    /// the other seats' cash, Support and Big Man points, their scores (keeping the order and the
    /// equalities the rail shows), and the sites and base Tolerance of every sector the seat does not
    /// own.
    /// </summary>
    public static void ChangeWhatTheSeatDoesNotKnow(MatchState view, PlayerId seat)
    {
        var own = view.FindPlayer(seat)!.ScenarioScore;
        var stranger = view.Players.FirstOrDefault(player => player.Id != seat)?.Id;
        foreach (var player in view.Players.Where(player => player.Id != seat))
        {
            player.Cash += 1_000_003;
            player.Support += 17;
            player.BigManPoints += 5;
            if (player.Status == PlayerStatus.Active)
                player.ScenarioScore = own + 3 * (player.ScenarioScore - own);
        }
        foreach (var sector in view.Sectors.Where(sector => sector.Owner != seat))
        {
            sector.BaseTolerance += 4;
            // Each site complete, as another seat's (SiteControlRules.IsComplete).
            foreach (var site in sector.Sites)
            {
                site.Resistance = 0;
                site.InfluencedBy = sector.Owner ?? stranger ?? site.InfluencedBy;
            }
        }
    }

    private static IReadOnlyList<string> ToleranceTooltip(MatchState state, PlayerId seat, MatchSectorState sector)
    {
        var estimate = ChaosRangeProjection.Detail(state, seat, sector.Id);
        return StatusConsoleTooltip.Tolerance(sector.Tolerance, estimate,
            StatusConsolePresentation.ChaosBreakdown(state, estimate), parts: StatusConsoleTooltip.ToleranceParts.Of(state, sector));
    }
}
