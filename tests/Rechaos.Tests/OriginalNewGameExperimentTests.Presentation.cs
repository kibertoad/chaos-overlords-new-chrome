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

    // RULE-SEARCH-002, FND-SEARCH-006: the probe writes a human's Search filter entries as the
    // Search panel does and keeps every site marker of the last city redraw before the dump:
    // definition, sector, ordinal and controlled flag, in drawing order, with the human it was
    // drawn for. The rebuild's city shows that human the same markers for the filter the probe set
    // for that human. EXP-TURN-045 selects every even site definition, so the ordinals skip the
    // sites left out, and the human's Headquarters is drawn as controlled.
    [Theory]
    [MemberData(nameof(MarkerRuns))]
    public void TheCityShowsTheOriginalsSiteMarkers(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = Replayed(recorded).Match;
        var drawn = recorded.CityMarkers!;
        var viewer = new PlayerId(drawn.Viewer);
        Assert.Contains(viewer, recorded.Humans);
        var filter = recorded.SearchFilter(viewer).Select(definition => (short)definition).ToHashSet();
        var markers = CitySiteMarkerProjection.Project(match, viewer, filter)
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

    public static TheoryData<string, int> HireStepRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].HireSteps.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-HIRE-003, FND-HIRE-008: after the dump the probe dragged offers onto sectors and pressed
    // their Reject crosses on the original's Hire dock, and kept hire_orders after each step. The
    // rebuild's console hits the same slot or sector at the points the probe pressed and released
    // (EXP-HIRE-001), takes each drop through the dock's placement check and QueueHire, and each
    // Reject through SnubHireOffer, and its pending hire and snub give the same orders after every
    // step. No step drops on a sector holding six of the player's gangs, which DEV-HIRE-001 refuses.
    [Theory]
    [MemberData(nameof(HireStepRuns))]
    public void TheHireDockSetsTheOriginalsOrders(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = Replayed(recorded).Match;
        var human = recorded.Humans[0];
        var player = match.FindPlayer(human)!;
        foreach (var step in recorded.HireSteps)
        {
            if (step.Slot >= 0)
            {
                // The press point is the centre of the Reject cross or of the portrait, and the
                // release point the centre of the sector's city map cell.
                var press = step.Sector == -2
                    ? new Microsoft.Xna.Framework.Point(488 + 66 * step.Slot, 443)
                    : new Microsoft.Xna.Framework.Point(472 + 66 * step.Slot, 405);
                var rejectSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.Reject, press);
                var portraitSlot = HitTest.IndexAt(HireDockLayout.SlotCount, HireDockLayout.PortraitHit, press);
                Assert.Equal(step.Sector == -2 ? step.Slot : -1, rejectSlot);
                if (step.Sector != -2)
                {
                    Assert.Equal(step.Slot, portraitSlot);
                    var release = new Microsoft.Xna.Framework.Point(
                        29 + 54 * (step.Sector % 8), 68 + 52 * (step.Sector / 8));
                    Assert.True(CityMapLayout.TrySectorAt(release, out var dropped));
                    Assert.Equal(step.Sector, dropped);
                }
            }
            TakeHireStep(match, human, step);
            var orders = Enumerable.Range(0, HireDockLayout.SlotCount).Select(slot =>
                player.PendingHires.FirstOrDefault(pending => pending.OfferSlot == slot) is { } pending
                    ? pending.TargetSectorId
                    : player.SnubbedHireOfferSlot == slot ? -2 : -1).ToArray();
            var expected = step.Orders.Skip(3 * human.Value).Take(3).ToArray();
            var label = step.Slot < 0 ? "exit" : step.Sector == -2 ? $"reject {step.Slot}" : $"drag {step.Slot} to {step.Sector}";
            Assert.True(expected.SequenceEqual(orders),
                $"after {label}: the original holds [{string.Join(", ", expected)}], the rebuild [{string.Join(", ", orders)}]");
            Assert.All(step.Orders.Where((_, index) => index / 3 != human.Value), order => Assert.Equal(-1, order));
        }
    }

    // RULE-HIRE-003: a Reject press is SnubHireOffer, a drop the dock accepts is QueueHire.
    private static void TakeHireStep(MatchState match, PlayerId human, RecordedHireStep step)
    {
        var player = match.FindPlayer(human)!;
        short Offer(int slot) => player.HireOfferSlots[slot].GangDefinitionId!.Value;
        if (step.Slot >= 0 && step.Sector == -2) match.SnubHireOffer(human, Offer(step.Slot));
        else if (step.Slot >= 0 && HireDropPlacement.Rejection(match, human, step.Sector) is null)
            match.QueueHire(human, Offer(step.Slot), step.Sector);
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
        var match = Replayed(recorded).Match;
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
                // The probe releases each press where it pressed, so ALL, NONE and Done act on
                // that release (FND-UI-062).
                if (handled.Press.Control is SiteSearchControl.All or SiteSearchControl.None or SiteSearchControl.Done)
                    open = !SiteSearchPanel.Release(selections, human, handled.Press.Control, rows);
            }
            Assert.Equal(human.Value, click.ActivePlayer);
            Assert.Equal(click.PanelOpen, open);
            var table = Enumerable.Range(0, MatchLimits.PlayerCount).SelectMany(player => rows.Select(site =>
                selections.IsSelected(new PlayerId(player), site) ? 1 : 0));
            Assert.True(click.Filters.SequenceEqual(table), $"after the click at ({click.X}, {click.Y})");
        }
    }

    public static TheoryData<string, int> EquipListRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].EquipLists.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-EQUIP-004: at the endpoint the probe called the original's Equip list builder for every
    // category of every living gang of the human (FND-EQUIP-008). The rebuild offers the same items,
    // in item record order, as its legal Equip commands of the gang in that category, and gives the
    // gang the Tech Level the builder was passed.
    [Theory]
    [MemberData(nameof(EquipListRuns))]
    public void TheEquipListOffersTheOriginalsItems(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = Replayed(recorded).Match;
        var human = recorded.Humans[0];
        var gangs = match.Players[human.Value].Gangs;
        // The probe builds lists for every living gang, so the recorded slots are the living ones.
        Assert.Equal(
            Enumerable.Range(0, gangs.Count).Where(slot => gangs[slot].IsActive),
            recorded.EquipLists.Select(list => list.Slot).Distinct());
        foreach (var lists in recorded.EquipLists.GroupBy(list => list.Slot))
        {
            var gang = gangs[lists.Key];
            var equips = CommandOptionCatalog.LegalCommands(match, human, gang.Id)
                .Where(command => command.Action == GangAction.Equip)
                .ToArray();
            foreach (var list in lists)
            {
                Assert.Equal(list.TechLevel, match.Definitions.Gangs[gang.DefinitionId].TechLevel);
                var offered = equips
                    .Where(command => EquipmentCommandLayout.CategoryForItemType(match.Definitions.Items[command.Target.Id].Type) == list.Category)
                    .Select(command => command.Target.Id)
                    .ToArray();
                Assert.True(list.Items.SequenceEqual(offered),
                    $"slot {list.Slot} category {list.Category}: the original lists [{string.Join(" ", list.Items)}], the rebuild [{string.Join(" ", offered)}]");
            }
        }
    }

    public static TheoryData<string, int> AttackListRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].AttackLists.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-ATTACK-002: at the endpoint the probe called the Attack picker's roster builder for every
    // other player and every living gang of the human, with the gang's sector (FND-ATTACK-006). The
    // rebuild's picker shows the same opponent's gangs, by roster slot, in the same cells. Its
    // options are the gang's legal Attack orders, which also refuse an undetected target when an
    // order is submitted (DEV-ATTACK-002); the picker never offers one either way.
    [Theory]
    [MemberData(nameof(AttackListRuns))]
    public void TheAttackPickerOffersTheOriginalsTargets(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = Replayed(recorded).Match;
        var human = recorded.Humans[0];
        var gangs = match.Players[human.Value].Gangs;
        // The probe builds a list for every other player and every living gang, so the recorded
        // slots are the living ones and each has one list per opponent.
        Assert.Equal(
            Enumerable.Range(0, gangs.Count).Where(slot => gangs[slot].IsActive),
            recorded.AttackLists.Select(list => list.Slot).Distinct());
        foreach (var lists in recorded.AttackLists.GroupBy(list => list.Slot))
        {
            var gang = gangs[lists.Key];
            Assert.Equal(Enumerable.Range(0, 6).Where(player => player != human.Value), lists.Select(list => list.Opponent));
            var options = AttackTargetRoster.Order(match, CommandOptionCatalog.LegalCommands(match, human, gang.Id)
                .Where(command => command.Action == GangAction.Attack));
            foreach (var list in lists)
            {
                Assert.Equal(list.Sector, gang.SectorId);
                var roster = match.Players[list.Opponent].Gangs.ToList();
                var offered = AttackPicker.TargetCells(match, options, new PlayerId(list.Opponent))
                    .Select(cell => roster.FindIndex(target => target.Id.Value == options[cell].Target.Id))
                    .ToArray();
                Assert.True(list.Targets.SequenceEqual(offered),
                    $"slot {list.Slot} opponent {list.Opponent}: the original lists [{string.Join(" ", list.Targets)}], the rebuild [{string.Join(" ", offered)}]");
            }
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
    // listed in slot order. EXP-UI-023 ends with one player active and draws only its splash.
    // TheRebuildStartsTheSameMatch requires these rows of every run that ends after the awards.
    [Theory]
    [MemberData(nameof(EndgameRuns))]
    public void TheEndgameListsThePlayersInTheOriginalsOrder(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = Replayed(recorded).Match;
        var drawn = recorded.EndgameRows!;
        Assert.Equal(3, drawn.Arguments.Count);
        // The mode comes from the renderer's arguments, so a drawing whose names the probe missed
        // fails on the names it lacks and is not taken for the other mode.
        if (drawn.DrawsSplash)
        {
            Assert.True(drawn.Kinds.SequenceEqual(["splash"]) && drawn.Players.SequenceEqual([drawn.Arguments[2]]),
                $"the splash of player {drawn.Arguments[2]} listed [{string.Join(", ", drawn.Players)}] as [{string.Join(", ", drawn.Kinds)}]");
            Assert.Equal(drawn.Arguments[2], EndgameNoticePresentation.Survivor(match)?.Player.Value);
            return;
        }
        Assert.DoesNotContain("splash", drawn.Kinds);
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
