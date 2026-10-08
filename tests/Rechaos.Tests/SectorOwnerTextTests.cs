using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// DEV-UI-005: the owner of a city sector in words, which SCR-UI-003 and SCR-UI-004 show by colour
/// alone, in a tooltip after the pointer rests on the sector and on the message line after a key
/// moves the selection.
/// </summary>
public sealed class SectorOwnerTextTests
{
    [Fact]
    public void TheTooltipNamesTheOwnerAndItsSeatOnTheOverlordBar()
    {
        var match = NativeSaveSerializerTests.CreateMatch();
        var owned = OwnedBy(match, new PlayerId(1));

        Assert.Equal(["SECTOR H8", "OWNER: TWO", "OVERLORD BAR SEAT 2"],
            SectorOwnerText.Tooltip(match, new PlayerId(0), owned));
        Assert.Equal("OWNER: TWO (YOU)", SectorOwnerText.Tooltip(match, new PlayerId(1), owned)[1]);
        Assert.Equal(["SECTOR B1", "NO OWNER"], SectorOwnerText.Tooltip(match, new PlayerId(0), 1));
    }

    [Fact]
    public void TheMessageLineNamesTheOwnerWithinTheLine()
    {
        var match = NativeSaveSerializerTests.CreateMatch(new string('W', 40));

        Assert.Equal("A1 OWNED BY YOU", SectorOwnerText.MessageLine(match, new PlayerId(0), 0));
        Assert.Equal("H8 OWNED BY TWO", SectorOwnerText.MessageLine(match, new PlayerId(0), 63));
        Assert.Equal("B1 HAS NO OWNER", SectorOwnerText.MessageLine(match, new PlayerId(0), 1));
        var clipped = SectorOwnerText.MessageLine(match, new PlayerId(1), 0);
        Assert.StartsWith("A1 OWNED BY WWW", clipped);
        Assert.True(CityStatusMessage.Fits(clipped));
    }

    [Fact]
    public void AKeyThatMovesTheSelectionNamesTheNewSectorsOwner()
    {
        var game = Game(ClientScreen.City, out _);
        DeviationBehaviourTests.Field("_cursor").SetValue(game, 1);

        DeviationBehaviourTests.Call(game, "UpdateCity", new KeyboardState(Keys.Left));

        Assert.Equal(0, DeviationBehaviourTests.Field("_cursor").GetValue(game));
        Assert.Equal("A1 OWNED BY YOU", DeviationBehaviourTests.Field("_message").GetValue(game));
    }

    [Fact]
    public void AKeyThatMovesTheSectorViewNamesTheNewSectorsOwner()
    {
        var game = Game(ClientScreen.Sector, out _);
        DeviationBehaviourTests.Field("_cursor").SetValue(game, 55);

        DeviationBehaviourTests.Call(game, "UpdateSector", new KeyboardState(Keys.Down));

        Assert.Equal("H8 OWNED BY TWO", DeviationBehaviourTests.Field("_message").GetValue(game));
    }

    [Fact]
    public void TheTooltipWaitsForThePointerToRestOnACityCell()
    {
        var game = Game(ClientScreen.City, out _);
        var cell = CityMapLayout.Destination(63).Center;

        Hover(game, cell, TimeSpan.FromSeconds(10));
        Assert.Null(Settled(game));
        Hover(game, cell, TimeSpan.FromSeconds(10) + HoverDwellTracker.Delay);
        Assert.Equal(63, Settled(game));

        // A press that starts a drag hides it, so it never covers a drop.
        DeviationBehaviourTests.Field("_draggedGangId").SetValue(game, new GangId(1));
        Hover(game, cell, TimeSpan.FromSeconds(20));
        Assert.Null(Settled(game));
    }

    [Fact]
    public void OnTheSectorViewTheOwnerStripAndTheDisplayCellsNameTheirOwners()
    {
        var game = Game(ClientScreen.Sector, out _);
        DeviationBehaviourTests.Field("_cursor").SetValue(game, 54);

        Assert.Equal(54, HoveredOwnerSector(game, SectorDetailLayout.OwnerStrip.Center));
        Assert.Equal(63, HoveredOwnerSector(game, SectorDetailLayout.CellOf(54, 63)!.Value.Center));
        Assert.Null(HoveredOwnerSector(game, new Point(300, 400)));
    }

    [Fact]
    public void OtherScreensShowNoOwnerTooltip()
    {
        var game = Game(ClientScreen.Finance, out _);

        Assert.Null(HoveredOwnerSector(game, CityMapLayout.Destination(0).Center));
    }

    private static int OwnedBy(MatchState match, PlayerId player) =>
        match.Sectors.First(sector => sector.Owner == player).Id;

    private static ChaosGame Game(ClientScreen screen, out MatchState match)
    {
        match = NativeSaveSerializerTests.CreateMatch();
        match.FinishUpkeep();
        var game = LeavePromptTests.GameFor(match, new MatchActions(new MatchReplayRecorder(match)), null);
        ((ScreenRouter)DeviationBehaviourTests.Field("_screens").GetValue(game)!).Show(screen);
        return game;
    }

    private static void Hover(ChaosGame game, Point point, TimeSpan time)
    {
        DeviationBehaviourTests.Field("_inputTime").SetValue(game, time);
        DeviationBehaviourTests.Call(game, "UpdateHoverPoint", (Point?)point);
    }

    private static int? Settled(ChaosGame game) =>
        ((HoverDwellTracker)DeviationBehaviourTests.Field("_sectorOwnerDwell").GetValue(game)!).SettledRegion;

    private static int? HoveredOwnerSector(ChaosGame game, Point point)
    {
        DeviationBehaviourTests.Field("_hoverPoint").SetValue(game, (Point?)point);
        return (int?)DeviationBehaviourTests.Call(game, "HoveredOwnerSector");
    }
}
