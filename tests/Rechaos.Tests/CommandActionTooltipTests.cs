using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CommandActionTooltipTests
{
    [Fact]
    public void EveryCommandOverlayEntryExplainsItself()
    {
        foreach (var action in CommandOverlayLayout.Actions)
        {
            var lines = CommandActionTooltips.Lines(action);

            Assert.True(lines.Count >= 2);
            Assert.Contains(lines, line => !string.IsNullOrWhiteSpace(line));
            Assert.All(lines, line => Assert.Equal(line.ToUpperInvariant(), line));
        }
    }

    [Fact]
    public void TooltipsAreNarrowEnoughForTheVirtualScreen()
    {
        foreach (var action in CommandOverlayLayout.Actions)
        {
            var lines = CommandActionTooltips.Lines(action);
            var bounds = HoverTooltipLayout.Bounds(new Point(320, 230), lines);

            Assert.True(bounds.Width <= VirtualInput.Width - 16);
            Assert.All(lines,
                line => Assert.True(line.Length * OriginalFontLayout.CellWidth + 16 <= bounds.Width));
        }
    }

    [Fact]
    public void TheFirstLineNamesTheActionAndTheIdleEntryReadsAsAnOrder()
    {
        Assert.Equal("INFLUENCE", CommandActionTooltips.Lines(GangAction.Influence)[0]);
        Assert.Equal("NO ORDER", CommandActionTooltips.Lines(GangAction.None)[0]);
    }

    [Theory]
    [InlineData(GangAction.Bribe, "$3")]
    [InlineData(GangAction.Bribe, "TOLERANCE BY 3")]
    [InlineData(GangAction.Snitch, "TOLERANCE BY 3")]
    [InlineData(GangAction.Heal, "RESTORES FORCE TO 10")]
    [InlineData(GangAction.Move, "SIX")]
    [InlineData(GangAction.Terminate, "UPKEEP")]
    [InlineData(GangAction.Equip, "CLICK ORDER")]
    [InlineData(GangAction.Equip, "CASH >= PRICE")]
    public void TooltipsQuoteTheRuleValuesThatDriveTheCommand(GangAction action, string expected)
    {
        Assert.Contains(CommandActionTooltips.Lines(action), line => line.Contains(expected));
    }

    [Theory]
    [InlineData(GangAction.Bribe)]
    [InlineData(GangAction.Snitch)]
    public void ToleranceChangingCommandsExplainHowLongTheirEffectLasts(GangAction action)
    {
        var lines = CommandActionTooltips.Lines(action);

        Assert.Contains(lines, line => line.Contains("GANGS CAN STACK IT"));
        Assert.Contains(lines, line => line.Contains("ON LATER TURNS"));
        // RULE-TOLERANCE-001, RULE-TOLERANCE-002
        Assert.Contains("ONE POINT TOWARD 17 - SECTOR INCOME.", lines);
        Assert.Contains("BASE TOLERANCE IS KEPT WITHIN 1..40.", lines);
        Assert.Contains(lines, line => line.Contains("NEXT TURN'S CHAOS TEST"));
    }

    // RULE-SITE-001, RULE-TOLERANCE-001: the console shows the base and the sites' part apart.
    [Fact]
    public void TheConsoleToleranceHoverShowsTheBaseAndTheSitesApart()
    {
        var parts = new StatusConsoleTooltip.ToleranceParts(Base: 15, Sites: -2, NormalBase: 12);

        var planned = StatusConsoleTooltip.Tolerance(
            13, new ChaosRangeEstimate(new ChaosRange(0, 0), []), [], parts: parts);
        var moved = StatusConsoleTooltip.Tolerance(
            10, new ChaosRangeEstimate(new ChaosRange(0, 0), []), [], parts: parts);

        Assert.Contains("TOLERANCE 13: BASE 15 + SITES -2.", planned);
        Assert.Contains("BASE MOVES 1 PER TURN TOWARD 12", planned);
        Assert.DoesNotContain("CHANGES SINCE PLANNING COUNT NEXT TURN.", planned);
        Assert.Contains("CHANGES SINCE PLANNING COUNT NEXT TURN.", moved);
    }

    [Fact]
    public void ToleranceChangingCommandsShowTheSelectedSectorsCurrentTemporaryShift()
    {
        var state = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)],
            allowSparsePlayerIds: true));
        var gang = state.Players[0].Gangs[0];
        var sector = state.Sectors[gang.SectorId];
        var normal = ToleranceResolver.NormalBaseTolerance(sector);
        sector.BaseTolerance = normal + 6;

        var lines = CommandActionTooltips.Lines(GangAction.Bribe, state, gang);

        Assert.Contains("CURRENT BRIBE/SNITCH SHIFT: +6.", lines);
        Assert.Contains($"BASE {normal + 6}; NORMAL BASE {normal}.", lines);
        Assert.Contains(lines, line => line.EndsWith($"THIS TURN'S CHAOS TEST USES {sector.Tolerance}."));
    }

    [Fact]
    public void RowsResolveOnlyInsideTheOneOffActionList()
    {
        var actions = CommandOverlayLayout.ActionsFor(recurring: false);
        for (var index = 0; index < actions.Count; index++)
            Assert.Equal(index, CommandActionTooltips.RowAt(
                CommandOverlayLayout.ActionRow(index).Center, recurring: false));

        Assert.Null(CommandActionTooltips.RowAt(new Point(0, 0), recurring: false));
        var gap = CommandOverlayLayout.ActionRow(0);
        Assert.Null(CommandActionTooltips.RowAt(
            new Point(gap.Center.X, gap.Bottom + 1), recurring: false));
    }

    [Fact]
    public void RecurringRowsAddressTheShorterRepeatableList()
    {
        var recurring = CommandOverlayLayout.ActionsFor(recurring: true);
        Assert.True(recurring.Count < CommandOverlayLayout.Actions.Count);

        var last = recurring.Count - 1;
        Assert.Equal(last, CommandActionTooltips.RowAt(
            CommandOverlayLayout.ActionRow(last).Center, recurring: true));
        Assert.Null(CommandActionTooltips.RowAt(
            CommandOverlayLayout.ActionRow(recurring.Count).Center, recurring: true));
    }

    [Fact]
    public void ATooltipAppearsOnlyAfterOneSecondOnTheSameRow()
    {
        var dwell = new HoverDwellTracker();
        Assert.Equal(TimeSpan.FromSeconds(1), HoverDwellTracker.Delay);

        dwell.Update(3, TimeSpan.FromSeconds(10));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(10.9));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(11));
        Assert.Equal(3, dwell.SettledRegion);
        dwell.Update(3, TimeSpan.FromSeconds(20));
        Assert.Equal(3, dwell.SettledRegion);
    }

    [Fact]
    public void LeavingOrChangingRowRestartsTheDwell()
    {
        var dwell = new HoverDwellTracker();
        dwell.Update(3, TimeSpan.FromSeconds(1));
        dwell.Update(3, TimeSpan.FromSeconds(4));
        Assert.Equal(3, dwell.SettledRegion);

        dwell.Update(4, TimeSpan.FromSeconds(4.1));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(5));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(5.1));
        Assert.Equal(4, dwell.SettledRegion);

        dwell.Update(null, TimeSpan.FromSeconds(6.2));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(6.3));
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(7.4));
        Assert.Equal(4, dwell.SettledRegion);

        dwell.Cancel();
        Assert.Null(dwell.SettledRegion);
        dwell.Update(4, TimeSpan.FromSeconds(8.5));
        Assert.Null(dwell.SettledRegion);
    }

    [Fact]
    public void TheDwellRejectsNegativeRegionsAndTimestamps()
    {
        var dwell = new HoverDwellTracker();
        Assert.Throws<ArgumentOutOfRangeException>(() => dwell.Update(-1, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => dwell.Update(0, TimeSpan.FromSeconds(-1)));
    }
}
