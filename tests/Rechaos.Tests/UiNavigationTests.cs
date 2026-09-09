using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class UiNavigationTests
{
    [Fact]
    public void AttackTargetPanelUsesOriginalAcquisitionApertures()
    {
        Assert.Equal(EquipmentCommandLayout.Panel, AttackCommandLayout.Panel);
        Assert.Equal(new Rectangle(129, 141, 64, 64), AttackCommandLayout.ActorPortrait);
        Assert.Equal(new Rectangle(240, 141, 64, 64), AttackCommandLayout.TargetPortrait);
        Assert.Equal(new Rectangle(200, 141, 32, 32), AttackCommandLayout.Opponent(0));
        Assert.Equal(new Rectangle(200, 289, 32, 32), AttackCommandLayout.Opponent(4));
        Assert.Equal(new Rectangle(129, 207, 20, 20), AttackCommandLayout.ActorItem(0));
        Assert.Equal(new Rectangle(284, 207, 20, 20), AttackCommandLayout.TargetItem(2));
        Assert.Equal(new Rectangle(0, 240, 20, 20), OriginalSpriteLayout.ItemPortrait(12));
    }

    [Fact]
    public void GangInformationStatisticsFollowTemplateRows()
    {
        Assert.Equal([244, 253, 271, 280, 289, 298, 307],
            Enumerable.Range(0, 7).Select(GangInformationLayout.StatisticY));
        Assert.Equal(287, GangInformationLayout.LeftValueRight);
        Assert.Equal(383, GangInformationLayout.RightValueRight);
    }

    [Fact]
    public void InfluencePickerUsesOriginalStaggeredSiteLayout()
    {
        Assert.Equal(new Rectangle(130, 141, 64, 64), InfluenceCommandLayout.Portrait);
        Assert.Equal(new Rectangle(209, 141, 120, 64), InfluenceCommandLayout.Site(0));
        Assert.Equal(new Rectangle(312, 198, 120, 64), InfluenceCommandLayout.Site(1));
        Assert.Equal(new Rectangle(209, 255, 120, 64), InfluenceCommandLayout.Site(2));
    }

    [Fact]
    public void StatusConsoleValuesFollowTemplateRows()
    {
        Assert.Equal(579, StatusConsoleLayout.ValueRight);
        Assert.Equal([60, 69, 78, 87, 96],
            Enumerable.Range(0, 5).Select(StatusConsoleLayout.SectorValueY));
    }

    [Fact]
    public void HirePriceSitsBesideRejectControl()
    {
        Assert.Equal(new Point(442, 443), HireDockLayout.Price(0));
        Assert.Equal(new Point(574, 443), HireDockLayout.Price(2));
    }

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
    public void IndexedDoubleClickSupportsGangAndHirePortraits()
    {
        var clicks = new IndexedDoubleClickTracker();
        Assert.False(clicks.Register(4, TimeSpan.FromSeconds(1)));
        Assert.True(clicks.Register(4, TimeSpan.FromSeconds(1.4)));
        Assert.False(clicks.Register(5, TimeSpan.FromSeconds(2)));
        clicks.Cancel();
        Assert.False(clicks.Register(5, TimeSpan.FromSeconds(2.2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => clicks.Register(-1, TimeSpan.Zero));
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
        Assert.Equal(new Rectangle(61, 48, 54, 52), SectorDetailLayout.Cell(0, 0));
        Assert.Equal(new Rectangle(169, 152, 54, 52), SectorDetailLayout.Cell(2, 2));
        Assert.Equal(18, SectorDetailLayout.SectorAt(27, 0, 0));
        Assert.Equal(27, SectorDetailLayout.SectorAt(27, 1, 1));
        Assert.Equal(36, SectorDetailLayout.SectorAt(27, 2, 2));
        Assert.Null(SectorDetailLayout.SectorAt(0, 0, 0));
        Assert.True(SectorDetailLayout.TrySectorAt(
            SectorDetailLayout.Cell(2, 1).Center, 27, out var right));
        Assert.Equal(28, right);
        Assert.False(SectorDetailLayout.TrySectorAt(
            SectorDetailLayout.Cell(0, 0).Center, 0, out _));
        Assert.Equal(new Rectangle(147, 120, 20, 20), SectorDetailLayout.Marker(27, 27));
        Assert.Null(SectorDetailLayout.Marker(27, 29));
        Assert.Equal(new Rectangle(83, 358, 120, 64), SectorDetailLayout.SitePortrait(2));
        Assert.Equal(new Rectangle(85, 417, 116, 4), SectorDetailLayout.SiteControlBar(2));
        Assert.Equal(new Rectangle(4, 394, 28, 66), SectorDetailLayout.Back);
    }

    [Fact]
    public void SectorGangCardsExposeDetailAndBothActionControls()
    {
        Assert.Equal(new Rectangle(251, 80, 70, 118), SectorGangCardLayout.Frame(0));
        Assert.Equal(new Rectangle(325, 80, 70, 118), SectorGangCardLayout.Frame(1));
        Assert.Equal(new Rectangle(254, 82, 64, 3), SectorGangCardLayout.ForceBar(0));
        Assert.Equal(new Rectangle(254, 87, 30, 15), SectorGangCardLayout.OneOffAction(0));
        Assert.Equal(new Rectangle(287, 87, 30, 15), SectorGangCardLayout.RepeatingAction(0));
        Assert.Equal(new Rectangle(254, 103, 64, 64), SectorGangCardLayout.Portrait(0));
        Assert.Equal(new Rectangle(296, 168, 21, 22), SectorGangCardLayout.ItemSlot(0, 2));
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
        Assert.Equal(new Rectangle(104, 125, 344, 209), EquipmentCommandLayout.Panel);
        Assert.Equal(new Rectangle(248, 154, 184, 11), EquipmentCommandLayout.ItemRow(0));
        Assert.Equal(new Rectangle(207, 141, 34, 34), EquipmentCommandLayout.Category(0));
        Assert.Equal(new Rectangle(207, 249, 34, 34), EquipmentCommandLayout.Category(3));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(0));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(1));
        Assert.Equal(1, EquipmentCommandLayout.CategoryForItemType(2));
        Assert.Equal(2, EquipmentCommandLayout.CategoryForItemType(3));
        Assert.Equal(3, EquipmentCommandLayout.CategoryForItemType(4));
        Assert.Equal(new Rectangle(130, 143, 64, 62), GangInformationLayout.Portrait);
        Assert.Equal(new Rectangle(254, 87, 63, 15), SectorGangCardLayout.AssignedCommand(0));
        Assert.Equal(new Rectangle(132, 141, 120, 64), SiteInformationLayout.Portrait);
        Assert.Equal(170, SiteInformationLayout.DataY(0));
        Assert.Equal(188, SiteInformationLayout.DataY(1));
        Assert.Equal(245, SiteInformationLayout.StatisticY(0));
        Assert.Equal(272, SiteInformationLayout.StatisticY(2));
        Assert.Equal(308, SiteInformationLayout.StatisticY(6));
        Assert.Equal(new Rectangle(132, 141, 56, 50), ItemInformationLayout.Portrait);
        Assert.Equal("RANGE", ItemInformationLayout.TypeLabel(2));
        Assert.Equal("ARMOR", ItemInformationLayout.TypeLabel(3));
        Assert.Equal(244, ItemInformationLayout.StatisticY(0));
        Assert.Equal(271, ItemInformationLayout.StatisticY(2));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatPanelLayout.Panel);
        Assert.Equal(new Rectangle(132, 136, 56, 64), CombatPanelLayout.Sector);
        Assert.Equal(new Rectangle(253, 255, 67, 64), CombatPanelLayout.LeftAction);
        Assert.Equal(new Rectangle(324, 255, 67, 64), CombatPanelLayout.RightAction);
        Assert.Equal(new Rectangle(255, 249, 63, 3), CombatPanelLayout.ForceBar(false));
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
        Assert.Equal(new Rectangle(360, 32, 32, 32), PlayerPortraitLayout.SetupTop(0));
        Assert.Equal(new Rectangle(540, 32, 32, 32), PlayerPortraitLayout.SetupTop(5));
        Assert.Equal(new Rectangle(8, 4, 32, 32), PlayerPortraitLayout.CityTop(0));
        Assert.Equal(new Rectangle(368, 4, 32, 32), PlayerPortraitLayout.CityTop(5));
        Assert.Equal(new Rectangle(485, 175, 64, 64), PlayerPortraitLayout.SetupLarge(3));
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
