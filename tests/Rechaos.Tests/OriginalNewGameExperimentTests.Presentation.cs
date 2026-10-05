using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> EndgameRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].EndgameRows is not null && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    public static TheoryData<string, int> MarkerRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].CityMarkers is not null) data.Add(experiment, run);
        return data;
    }

    // RULE-SEARCH-002, FND-SEARCH-006: the probe writes the human's Search filter entries as the
    // Search panel does and keeps every site marker of the last city redraw before the dump:
    // definition, sector, ordinal and controlled flag, in drawing order. The rebuild's city shows
    // the same markers for the same filter. EXP-TURN-045 selects every even site definition, so the
    // ordinals skip the sites left out, and the human's Headquarters is drawn as controlled.
    [Theory]
    [MemberData(nameof(MarkerRuns))]
    public void TheCityShowsTheOriginalsSiteMarkers(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var drawn = recorded.CityMarkers!;
        // The probe writes the first human's filter, so only that human's redraw is compared.
        Assert.Equal(recorded.Humans[0].Value, drawn.Viewer);
        var filter = recorded.SearchFilter.Select(definition => (short)definition).ToHashSet();
        var markers = CitySiteMarkerProjection.Project(match, new PlayerId(drawn.Viewer), filter)
            .Select(marker => $"{marker.SiteDefinitionId},{marker.SectorId},{marker.VisibleSlot},{(marker.Controlled ? 1 : 0)}")
            .ToArray();
        Assert.Equal(drawn.Markers.Select(marker => string.Join(",", marker)), markers);
    }

    // RULE-MOVE-002, EXP-TURN-043: the gang sectors compared above cannot tell the repair from a
    // Move that never ran, so this checks that the replay reaches the repair. In turn 24 player 2
    // orders Moves that would put seven of its gangs in sector 62, and the one mover sent back,
    // from sector 54, resolves in its own sector.
    [Fact]
    public void TheMoveRepairSendsTheMoverBack()
    {
        var match = ReplayedMatch("EXP-TURN-043", 0);
        var moves = match.Events
            .Where(e => e.Turn == 24 && e.Player == new PlayerId(2) && e.Action == GangAction.Move)
            .ToArray();
        var sentBack = Assert.Single(moves, e => e.Kind == GameEventKind.CommandResolved
            && e.Resolution is { PreviousValue: 54, ResultValue: 54 });
        Assert.Contains(moves, e => e.Kind == GameEventKind.CommandQueued
            && e.Gang == sentBack.Gang && e.Target == CommandTarget.Sector(62));
    }

    public static TheoryData<string, int> SearchClickRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].SearchClicks.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-SEARCH-001, FND-SEARCH-002: after the dump the probe pressed the console's Search control
    // and the panel's controls, and kept the whole filter table after each press. The rebuild's
    // console opens the panel at the same point, its panel hits the same control for each press, and
    // its selection, empty when the match starts, holds the same bytes after each: row n is site
    // definition n, and only the active player's entries change.
    [Theory]
    [MemberData(nameof(SearchClickRuns))]
    public void TheSearchPanelChangesTheOriginalsFilters(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var human = recorded.Humans[0];
        var rows = SiteSearchPanel.Rows(match.Definitions);
        Assert.Equal(Enumerable.Range(0, SiteSearchLayout.MaximumSites).Select(row => (short)row), rows);
        var selections = new SiteSearchSelectionState();
        var doubleClicks = new IndexedDoubleClickTracker();
        var open = false;
        var time = TimeSpan.Zero;
        foreach (var click in recorded.SearchClicks)
        {
            var point = new Microsoft.Xna.Framework.Point(click.X, click.Y);
            // No recorded press is a double-click (RULE-SEARCH-001), so the presses are a second
            // apart, outside the double-click window.
            time += TimeSpan.FromSeconds(1);
            if (!open)
            {
                Assert.Equal(CityConsoleAction.Search, CityConsoleLayout.ActionAt(point));
                open = true;
            }
            else
            {
                var handled = SiteSearchPanel.Press(selections, human, point, rows, doubleClicks, time);
                Assert.False(handled.OpensDetails);
                if (handled.Press.Control == SiteSearchControl.Done) open = false;
            }
            Assert.Equal(human.Value, click.ActivePlayer);
            Assert.Equal(click.PanelOpen, open);
            var table = Enumerable.Range(0, MatchLimits.PlayerCount).SelectMany(player => rows.Select(site =>
                selections.IsSelected(new PlayerId(player), site) ? 1 : 0));
            Assert.True(click.Filters.SequenceEqual(table), $"after the click at ({click.X}, {click.Y})");
        }
    }

    public static TheoryData<string, int> FinanceRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Finance.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-AWARDS-002, FND-AWARDS-005: the probe keeps the player of each name the endgame's first
    // drawing lists, in drawing order, and whether the row is ranked, eliminated or the victory
    // splash. The rebuild's endgame lists the same players in the same order and places.
    // EXP-TURN-038 has two players tied at standing 0 and EXP-TURN-039 two tied at standing 1,
    // listed in slot order.
    [Theory]
    [MemberData(nameof(EndgameRuns))]
    public void TheEndgameListsThePlayersInTheOriginalsOrder(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var drawn = recorded.EndgameRows!;
        if (drawn.Kinds is ["splash"])
        {
            Assert.Equal(drawn.Players[0], EndgameNoticePresentation.Survivor(match)?.Player.Value);
            return;
        }
        Assert.Null(EndgameNoticePresentation.Survivor(match));
        var rows = EndgamePresentation.Rows(match);
        Assert.Equal(drawn.Players, rows.Select(row => row.Player.Value));
        Assert.Equal(drawn.Kinds, rows.Select(row =>
            match.FindPlayer(row.Player)!.Status == PlayerStatus.Eliminated ? "eliminated" : "ranked"));
        // RULE-OBJECTIVE-002: the original draws a ranked row at the player's stored standing, so a
        // tie shares a place and the standing after it is skipped. The rebuild's place is that
        // standing plus one, and 0 for an eliminated row.
        Assert.Equal(
            drawn.Players.Zip(drawn.Kinds, (player, kind) =>
                kind == "ranked" ? recorded.Term("scenario_standing", player) + 1 : 0),
            rows.Select(row => row.Place));
    }

    // RULE-FINANCE-001, FND-FINANCE-003: before a Done press the probe opens the Financial panel and
    // keeps the nine numbers it draws: upkeep, gang count, recruits, equipment, officials, tax,
    // protection, Chaos and total. The rebuild projects the same panel from the same orders and
    // hires. EXP-TURN-044 opens the City variant and the Sector variant of the gangs' sector in every
    // turn, with Equip, a hire, Bribe and Sell, and in its last turn a Chaos order and a Move out of
    // that sector, whose destination is opened as well.
    [Theory]
    [MemberData(nameof(FinanceRuns))]
    public void TheFinancialPanelShowsTheOriginalsNumbers(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var compared = 0;
        StartMatch(recorded, out _, (match, human, turn) =>
        {
            foreach (var panel in recorded.Finance.Where(panel => panel.Turn == turn))
            {
                var projection = FinanceProjection.Project(
                    match, match.Players[human.Value], panel.Sector < 0 ? null : panel.Sector);
                int[] drawn =
                [
                    projection.GangUpkeep, projection.ProjectedGangCount, projection.NewContracts,
                    projection.Equipment, projection.CityOfficials, projection.SectorTax,
                    projection.SiteProtection, projection.ChaosEstimate, projection.CashAdjustment,
                ];
                Assert.True(panel.Values.SequenceEqual(drawn),
                    $"turn {turn}, sector {panel.Sector}: the original drew [{string.Join(",", panel.Values)}], the rebuild [{string.Join(",", drawn)}]");
                compared++;
            }
        });
        Assert.Equal(recorded.Finance.Count, compared);
    }
}
