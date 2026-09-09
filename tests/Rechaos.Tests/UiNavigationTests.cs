using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class UiNavigationTests
{
    [Fact]
    public void RouterStartsAtTitleAndBackReturnsThere()
    {
        var router = new ScreenRouter();

        Assert.Equal(ClientScreen.Title, router.Current);
        Assert.False(router.Back());
        router.Show(ClientScreen.Setup);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.City);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.Endgame);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.Handoff);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Title, router.Current);
        router.Show(ClientScreen.City);
        router.Show(ClientScreen.Events);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Commands);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Hire);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Sector);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Gang);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Finance);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Ranking);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Items);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Items);
        router.Show(ClientScreen.Give);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Items, router.Current);
        router.Show(ClientScreen.CombatSummary);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
        router.Show(ClientScreen.Search);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.City, router.Current);
    }

    [Fact]
    public void CityMapLayoutMapsEveryAtlasTileAndOwnershipLayer()
    {
        for (var sector = 0; sector < MatchLimits.SectorCount; sector++)
        {
            var source = CityMapLayout.Source(sector);
            var destination = CityMapLayout.Destination(sector);
            Assert.Equal(source.X + CityMapLayout.Left, destination.X);
            Assert.Equal(source.Y + CityMapLayout.Top, destination.Y);
            Assert.True(CityMapLayout.TrySectorAt(destination.Center, out var mapped));
            Assert.Equal(sector, mapped);
        }

        Assert.Equal(new Rectangle(0, 0, 54, 52), CityMapLayout.Source(0));
        Assert.Equal(new Rectangle(378, 364, 54, 52), CityMapLayout.Source(63));
        Assert.Equal(0, CityMapLayout.OwnershipSheet(null));
        Assert.Equal(6, CityMapLayout.OwnershipSheet(new PlayerId(5)));
        Assert.False(CityMapLayout.TrySectorAt(new Point(434, 460), out _));
    }

    [Fact]
    public void CitySectorDoubleClickRequiresSameSectorInsideBoundedWindow()
    {
        var clicks = new CitySectorClickTracker();
        Assert.False(clicks.Register(9, TimeSpan.FromSeconds(1)));
        Assert.False(clicks.Register(10, TimeSpan.FromSeconds(1.2)));
        Assert.True(clicks.Register(10, TimeSpan.FromSeconds(1.6)));
        Assert.False(clicks.Register(10, TimeSpan.FromSeconds(2.2)));
        clicks.Cancel();
        Assert.False(clicks.Register(10, TimeSpan.FromSeconds(2.3)));
        Assert.True(clicks.Register(10, TimeSpan.FromSeconds(2.8)));
        Assert.Throws<ArgumentOutOfRangeException>(() => clicks.Register(64, TimeSpan.Zero));
    }

    [Fact]
    public void OriginalPortraitLayoutsCoverAllDefinitionSlots()
    {
        Assert.Equal(new Rectangle(116, 0, 48, 64), OriginalSpriteLayout.PolicePatrolCar);
        Assert.Equal(new Rectangle(120, 300, 60, 60), OriginalSpriteLayout.HiredStamp);
        Assert.Equal(new Rectangle(492, 67, 20, 20), OriginalSpriteLayout.AssignedGangStatus);
        Assert.Equal(new Rectangle(492, 107, 20, 20), OriginalSpriteLayout.IdleGangStatus);
        Assert.Equal(new Rectangle(492, 147, 20, 20), OriginalSpriteLayout.IncomingGangStatus);
        Assert.Equal(new Rectangle(164, 17, 70, 118), OriginalSpriteLayout.GangCardFrame);
        Assert.Equal(new Rectangle(120, 211, 30, 47), OriginalSpriteLayout.SectorBackArrow);
        Assert.Equal(new Rectangle(480, 480, 32, 32), OriginalSpriteLayout.OverlordPortrait(15));
        Assert.Equal(new Rectangle(0, 0, 120, 64), OriginalSpriteLayout.SitePortrait(0));
        Assert.Equal(new Rectangle(0, 21 * 64, 120, 64), OriginalSpriteLayout.SitePortrait(21));
        Assert.Equal(new Rectangle(0, 0, 64, 64), OriginalSpriteLayout.GangPortrait(0));
        Assert.Equal(new Rectangle(576, 512, 64, 64), OriginalSpriteLayout.GangPortrait(89));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.SitePortrait(22));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.GangPortrait(90));
    }

    [Fact]
    public void GangStatusMarkerOccupiesLowerRightOfSectorCell()
    {
        Assert.Equal(new Rectangle(34, 64, 20, 20), GangStatusMarkerLayout.Destination(0));
        Assert.Equal(new Rectangle(412, 428, 20, 20), GangStatusMarkerLayout.Destination(63));
    }

    [Fact]
    public void SectorDetailProjectsClippedThreeByThreeNeighborhood()
    {
        Assert.Equal(new Rectangle(64, 4, 54, 52), SectorDetailLayout.Cell(0, 0));
        Assert.Equal(new Rectangle(172, 108, 54, 52), SectorDetailLayout.Cell(2, 2));
        Assert.Equal(18, SectorDetailLayout.SectorAt(27, 0, 0));
        Assert.Equal(27, SectorDetailLayout.SectorAt(27, 1, 1));
        Assert.Equal(36, SectorDetailLayout.SectorAt(27, 2, 2));
        Assert.Null(SectorDetailLayout.SectorAt(0, 0, 0));
        Assert.True(SectorDetailLayout.TrySectorAt(
            SectorDetailLayout.Cell(2, 1).Center, 27, out var right));
        Assert.Equal(28, right);
        Assert.False(SectorDetailLayout.TrySectorAt(
            SectorDetailLayout.Cell(0, 0).Center, 0, out _));
        Assert.Equal(new Rectangle(150, 76, 20, 20), SectorDetailLayout.Marker(27, 27));
        Assert.Null(SectorDetailLayout.Marker(27, 29));
        Assert.Equal(new Rectangle(85, 304, 120, 64), SectorDetailLayout.SitePortrait(2));
        Assert.Equal(new Rectangle(87, 363, 116, 4), SectorDetailLayout.SiteControlBar(2));
        Assert.Equal(new Rectangle(4, 394, 28, 66), SectorDetailLayout.Back);
    }

    [Fact]
    public void SectorGangCardsExposeDetailAndBothActionControls()
    {
        Assert.Equal(new Rectangle(251, 4, 70, 134), SectorGangCardLayout.Frame(0));
        Assert.Equal(new Rectangle(325, 4, 70, 134), SectorGangCardLayout.Frame(1));
        Assert.Equal(new Rectangle(254, 26, 30, 18), SectorGangCardLayout.OneOffAction(0));
        Assert.Equal(new Rectangle(287, 26, 30, 18), SectorGangCardLayout.RepeatingAction(0));
        Assert.Equal(new Rectangle(254, 46, 64, 64), SectorGangCardLayout.Portrait(0));
        Assert.Equal(new Rectangle(296, 112, 21, 22), SectorGangCardLayout.ItemSlot(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => SectorGangCardLayout.Frame(2));
    }

    [Fact]
    public void CommandOverlayUsesOriginalActionFirstOrdering()
    {
        Assert.Equal(
        [
            GangAction.Attack, GangAction.Bribe, GangAction.Chaos, GangAction.Control,
            GangAction.Equip, GangAction.Give, GangAction.Heal, GangAction.Hide,
            GangAction.Influence, GangAction.Move, GangAction.Research, GangAction.Sell,
            GangAction.Snitch, GangAction.None, GangAction.Terminate
        ], CommandOverlayLayout.Actions);
        Assert.True(CommandOverlayLayout.OpensTargetPicker(GangAction.Equip));
        Assert.True(CommandOverlayLayout.OpensTargetPicker(GangAction.Move));
        Assert.False(CommandOverlayLayout.OpensTargetPicker(GangAction.Chaos));
        Assert.Equal(new Rectangle(256, 61, 158, 22), CommandOverlayLayout.ActionRow(0));
    }

    [Theory]
    [InlineData(AiDifficulty.Goon, "GOON")]
    [InlineData(AiDifficulty.Criminal, "CRIMINAL")]
    [InlineData(AiDifficulty.CrimeLord, "CRIME LORD")]
    [InlineData(AiDifficulty.HomicidalManiac, "HOMICIDAL")]
    public void DifficultyTooltipNamesEveryOriginalLevelAndPromisesFairPlay(
        AiDifficulty difficulty,
        string label)
    {
        Assert.Equal(label, DifficultyPresentation.Label(difficulty));
        Assert.Contains(DifficultyPresentation.Tooltip(difficulty),
            line => line.Contains("NO BONUSES", StringComparison.Ordinal));
    }

    [Fact]
    public void PlayerPortraitLayoutUsesOriginalTopStripsAndSelectionSlots()
    {
        Assert.Equal(new Rectangle(361, 33, 32, 32), PlayerPortraitLayout.SetupTop(0));
        Assert.Equal(new Rectangle(541, 33, 32, 32), PlayerPortraitLayout.SetupTop(5));
        Assert.Equal(new Rectangle(8, 4, 32, 32), PlayerPortraitLayout.CityTop(0));
        Assert.Equal(new Rectangle(368, 4, 32, 32), PlayerPortraitLayout.CityTop(5));
        Assert.Equal(new Rectangle(482, 174, 64, 64), PlayerPortraitLayout.SetupLarge(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPortraitLayout.SetupTop(6));
    }

    [Fact]
    public void EventPresentationUsesDistinctPoliceAndControlLossLabels()
    {
        Assert.Equal("T5 CONTROL LOST SECTOR 8", NotificationPresentation.Describe(
            new GameNotification(0, 5, TurnPhase.Execution, ExecutionPhase.Chaos,
                GameNotificationKind.ControlLost, SectorId: 7)));
        Assert.Equal("T3 POLICE ATTACK GANG 12 SECTOR 2", NotificationPresentation.Describe(
            new GameNotification(0, 3, TurnPhase.Execution, ExecutionPhase.Combat,
                GameNotificationKind.Police, new GangId(12), 1)));
    }

    [Fact]
    public void SectorGangPortraitStripHasStableNonOverlappingHitRegions()
    {
        var portraits = Enumerable.Range(0, SectorGangView.MaximumPortraits)
            .Select(SectorGangView.Portrait).ToArray();

        Assert.Equal(new Rectangle(18, 370, 36, 36), portraits[0]);
        Assert.Equal(new Rectangle(378, 370, 36, 36), portraits[^1]);
        Assert.All(portraits.SelectMany((left, index) => portraits.Skip(index + 1)
            .Select(right => (left, right))), pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.Equal(new Rectangle(18, 107, 36, 36), SectorGangView.SearchPortrait(0));
        Assert.Equal(new Rectangle(18, 347, 36, 36),
            SectorGangView.SearchPortrait(SectorGangView.MaximumSearchRows - 1));
        Assert.Equal(new Rectangle(67, 90, 64, 64), GangArtLayout.DetailPortrait);
        Assert.Equal(new Rectangle(558, 58, 56, 56), GangArtLayout.SelectedEquipmentPortrait);
        Assert.Equal(new Rectangle(18, 108, 20, 20), GangArtLayout.CombatPortrait(0, false));
        Assert.Equal(new Rectangle(42, 372, 20, 20), GangArtLayout.CombatPortrait(11, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangArtLayout.CombatPortrait(12, false));
    }

    [Fact]
    public void HireDockMatchesOriginalThreeCellStripAndRetainsHiredSlot()
    {
        Assert.Equal(new Rectangle(438, 370, 66, 90), HireDockLayout.Cell(0));
        Assert.Equal(new Rectangle(571, 371, 64, 64), HireDockLayout.Portrait(2));
        var cells = HireDockLayout.Project([1, 3], new PendingHireState(2, 12), 1);
        Assert.Equal(new HireDockEntry(1, false), cells[0]);
        Assert.Equal(new HireDockEntry(2, true), cells[1]);
        Assert.Equal(new HireDockEntry(3, false), cells[2]);
        Assert.Equal(new Rectangle(570, 436, 66, 24), HireDockLayout.Reject(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireDockLayout.Cell(3));
    }

    [Fact]
    public void HireComparisonMatchesOriginalThreeColumnPanel()
    {
        Assert.Equal(new Rectangle(0, 0, 344, 209), HireComparisonLayout.Panel);
        Assert.Equal(new Rectangle(32, 168, 50, 24), HireComparisonLayout.Ok);
        Assert.Equal(new Rectangle(246, 10, 32, 32), HireComparisonLayout.Portrait(2));
        Assert.Equal(new Vector2(248, 184), HireComparisonLayout.StatPosition(2, 15));
        Assert.True(HireComparisonLayout.IsBestValue(0, 10, [10, 5, 10]));
        Assert.False(HireComparisonLayout.IsBestValue(0, 5, [10, 5, 10]));
        Assert.True(HireComparisonLayout.IsBestValue(1, 2, [2, 4, 7]));
        Assert.False(HireComparisonLayout.IsBestValue(1, 7, [2, 4, 7]));
    }
}
