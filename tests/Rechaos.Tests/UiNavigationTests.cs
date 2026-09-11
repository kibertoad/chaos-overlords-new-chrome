using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class UiNavigationTests
{
    [Fact]
    public void PointerButtonEdgeFiresOnlyOnReleasedToPressedTransition()
    {
        Assert.True(PointerButtonEdges.Pressed(ButtonState.Pressed, ButtonState.Released));
        Assert.False(PointerButtonEdges.Pressed(ButtonState.Pressed, ButtonState.Pressed));
        Assert.False(PointerButtonEdges.Pressed(ButtonState.Released, ButtonState.Pressed));
        Assert.False(PointerButtonEdges.Pressed(ButtonState.Released, ButtonState.Released));
    }

    [Fact]
    public void AttackTargetPanelUsesAcquisitionGridApertures()
    {
        Assert.Equal(EquipmentCommandLayout.Panel, AttackCommandLayout.Panel);
        Assert.Equal(new Rectangle(130, 142, 64, 64), AttackCommandLayout.ActorPortrait);
        Assert.Equal(new Rectangle(240, 141, 64, 64), AttackCommandLayout.TargetPortrait(0));
        Assert.Equal(new Rectangle(202, 141, 32, 32), AttackCommandLayout.Opponent(0));
        Assert.Equal(new Rectangle(202, 289, 32, 32), AttackCommandLayout.Opponent(4));
        Assert.Equal(new Rectangle(130, 207, 20, 20), AttackCommandLayout.ActorItem(0));
        Assert.Equal(new Rectangle(284, 207, 20, 20), AttackCommandLayout.TargetItem(0, 2));
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
        Assert.Equal(new Rectangle(130, 142, 64, 64), InfluenceCommandLayout.Portrait);
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
    public void SetupPlanningTimerButtonsMatchOriginalArtworkRows()
    {
        Assert.Equal(
        [
            new Rectangle(192, 330, 108, 27), new Rectangle(192, 359, 108, 27),
            new Rectangle(192, 388, 108, 27), new Rectangle(192, 417, 108, 27)
        ], PlanningTimerLayout.SetupChoices);
        Assert.Equal(new Rectangle(520, 336, 60, 3), PlanningTimerLayout.Bar);
    }

    [Fact]
    public void SetupPushButtonsUseRecoveredReleaseHitRectangles()
    {
        Assert.Equal(new Rectangle(370, 328, 92, 24), SetupButtonLayout.AddPlayer);
        Assert.Equal(new Rectangle(468, 328, 92, 24), SetupButtonLayout.RemovePlayer);
        Assert.Equal(new Rectangle(370, 375, 92, 45), SetupButtonLayout.Start);
        Assert.Equal(new Rectangle(468, 375, 92, 45), SetupButtonLayout.Back);
        Assert.Equal(new Rectangle(220, 0, 92, 24),
            SetupButtonLayout.PressedSource(SetupPushButton.AddPlayer));
        Assert.Equal(new Rectangle(220, 93, 92, 45),
            SetupButtonLayout.PressedSource(SetupPushButton.Back));
        Assert.Equal(SetupButtonLayout.Start,
            SetupButtonLayout.Destination(SetupPushButton.Start));
        Assert.Equal(SetupPushButton.AddPlayer, SetupButtonLayout.HitTest(new Point(370, 328)));
        Assert.Equal(SetupPushButton.Back, SetupButtonLayout.HitTest(new Point(559, 419)));
        Assert.Null(SetupButtonLayout.HitTest(new Point(560, 419)));
        Assert.Null(SetupButtonLayout.HitTest(new Point(559, 420)));
    }

    [Fact]
    public void HirePriceSitsBesideRejectControl()
    {
        Assert.Equal(new Rectangle(438, 436, 33, 24), HireDockLayout.PriceCell(0));
        Assert.Equal(new Rectangle(471, 436, 33, 24), HireDockLayout.Reject(0));
        Assert.Equal(new Point(449, 440), HireDockLayout.Price(0));
        Assert.Equal(new Point(581, 440), HireDockLayout.Price(2));
        Assert.Equal("06", HireDockLayout.PriceText(6));
        Assert.Equal("12", HireDockLayout.PriceText(12));
        Assert.Equal("123", HireDockLayout.PriceText(123));
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
        router.Show(ClientScreen.SectorGangs);
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
        router.Show(ClientScreen.GiveTarget);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Give, router.Current);
        Assert.True(router.Back());
        Assert.Equal(ClientScreen.Items, router.Current);
        router.Show(ClientScreen.Sell);
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
    public void RouterReportsOnlyActualScreenChanges()
    {
        var router = new ScreenRouter();
        var changes = new List<(ClientScreen From, ClientScreen To)>();
        router.Changed += (from, to) => changes.Add((from, to));

        router.Show(ClientScreen.Title);
        router.Show(ClientScreen.Help);
        router.Show(ClientScreen.Help);
        router.Back();

        Assert.Equal(
            [(ClientScreen.Title, ClientScreen.Help), (ClientScreen.Help, ClientScreen.Title)],
            changes);
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
    public void SiegePylonsArePairedInsideEveryCitySector()
    {
        for (var sectorId = 0; sectorId < MatchLimits.SectorCount; sectorId++)
        {
            var sector = CityMapLayout.Destination(sectorId);
            var pylons = SiegePylonLayout.ForSector(sectorId);

            Assert.Equal(2, pylons.Count);
            Assert.All(pylons, pylon => Assert.True(sector.Contains(pylon)));
            Assert.False(pylons[0].Intersects(pylons[1]));
        }

        Assert.Equal(
            [new Rectangle(10, 56, 6, 14), new Rectangle(42, 56, 6, 14)],
            SiegePylonLayout.ForSector(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => SiegePylonLayout.ForSector(64));
    }

    [Fact]
    public void OptionsExposeBothOriginalAudioScalesAsDistinctHitTargets()
    {
        Assert.Equal(OriginalSoundtrackPolicy.MaximumVolumeLevel + 1,
            OptionsLayout.MusicLevels.Count);
        Assert.Equal(OptionsLayout.MusicLevels.Count, OptionsLayout.SoundEffectLevels.Count);
        Assert.Equal(new Rectangle(132, 78, 28, 28), OptionsLayout.MusicLevels[0]);
        Assert.Equal(new Rectangle(472, 78, 28, 28), OptionsLayout.MusicLevels[^1]);
        Assert.Equal(new Rectangle(132, 140, 28, 28), OptionsLayout.SoundEffectLevels[0]);
        Assert.All(OptionsLayout.MusicLevels.Concat(OptionsLayout.SoundEffectLevels),
            level => Assert.True(OptionsLayout.Panel.Contains(level)));
        var levels = OptionsLayout.MusicLevels.Concat(OptionsLayout.SoundEffectLevels).ToArray();
        Assert.All(levels.SelectMany((left, index) =>
                levels.Skip(index + 1).Select(right => (left, right))),
            pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.WarnIfIdleGangs));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.BaseStatistics));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.DetailedCombat));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.SlidePanels));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.ColorDepth));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.Done));
    }

    [Fact]
    public void EveryOptionsEntryHasAnExplanatoryHoverTooltip()
    {
        Rectangle[] entries =
        [
            OptionsLayout.Music,
            OptionsLayout.SoundEffects,
            OptionsLayout.BaseStatistics,
            OptionsLayout.DetailedCombat,
            OptionsLayout.SlidePanels,
            OptionsLayout.WarnIfIdleGangs,
            OptionsLayout.ColorDepth,
            OptionsLayout.Done
        ];

        Assert.All(entries, entry =>
        {
            var lines = OptionsTooltip.At(entry.Center);
            Assert.True(lines.Count >= 2);
            var bounds = OptionsTooltip.Bounds(entry.Center, lines);
            Assert.True(bounds.Left >= 0 && bounds.Top >= 0);
            Assert.True(bounds.Right <= VirtualInput.Width);
            Assert.True(bounds.Bottom <= VirtualInput.Height);
        });
        Assert.Contains("ALWAYS ABOVE 16-BIT",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.ColorDepth.Center)));
        Assert.Contains("IMMEDIATELY",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.SlidePanels.Center)));
        Assert.Empty(OptionsTooltip.At(Point.Zero));
        Assert.Equal(Rectangle.Empty, OptionsTooltip.Bounds(Point.Zero, []));
    }

    [Fact]
    public void RecurringCommandMenuOmitsOneOffActions()
    {
        var actions = CommandOverlayLayout.ActionsFor(recurring: true);

        Assert.Contains(GangAction.None, actions);
        Assert.Contains(GangAction.Research, actions);
        Assert.DoesNotContain(GangAction.Attack, actions);
        Assert.DoesNotContain(GangAction.Equip, actions);
        Assert.DoesNotContain(GangAction.Give, actions);
        Assert.DoesNotContain(GangAction.Move, actions);
        Assert.DoesNotContain(GangAction.Sell, actions);
        Assert.DoesNotContain(GangAction.Terminate, actions);
        Assert.Equal(CommandOverlayLayout.Actions, CommandOverlayLayout.ActionsFor(recurring: false));
    }

    [Fact]
    public void ResearchPickerReportsCompletedProgressAgainstDifficulty()
    {
        Assert.Equal("0/8", EquipmentCommandLayout.ResearchProgress(8, 8));
        Assert.Equal("3/8", EquipmentCommandLayout.ResearchProgress(8, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EquipmentCommandLayout.ResearchProgress(8, 9));
    }

    [Fact]
    public void PanelSlideIsBoundedAndOnlyAppliesToPanelScreens()
    {
        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);

        slide.Begin(ClientScreen.Gang, start);

        Assert.Equal(PanelSlideTransition.StartOffset, slide.Offset(ClientScreen.Gang, start));
        Assert.Equal(172,
            slide.Offset(ClientScreen.Gang, start + PanelSlideTransition.Duration / 2));
        Assert.Equal(0, slide.Offset(ClientScreen.Gang, start + PanelSlideTransition.Duration));
        Assert.Equal(0, slide.Offset(ClientScreen.City, start));
        slide.Begin(ClientScreen.City, start);
        Assert.Equal(0, slide.Offset(ClientScreen.City, start));
    }

    [Fact]
    public void PanelSlideOnlyRunsForForwardDetailTransitions()
    {
        Assert.True(PanelSlideTransition.ShouldAnimate(ClientScreen.Sector, ClientScreen.Gang));
        Assert.True(PanelSlideTransition.ShouldAnimate(ClientScreen.City, ClientScreen.Sector));
        Assert.False(PanelSlideTransition.ShouldAnimate(ClientScreen.Gang, ClientScreen.Sector));
        Assert.False(PanelSlideTransition.ShouldAnimate(ClientScreen.Site, ClientScreen.Sector));
        Assert.False(PanelSlideTransition.ShouldAnimate(ClientScreen.City, ClientScreen.Commands));
        Assert.False(PanelSlideTransition.ShouldAnimate(ClientScreen.Sector, ClientScreen.Commands));

        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);
        slide.Begin(ClientScreen.Gang, ClientScreen.Sector, start);
        Assert.Equal(0, slide.Offset(ClientScreen.Sector, start));
        slide.Begin(ClientScreen.Sector, ClientScreen.Gang, start);
        Assert.Equal(PanelSlideTransition.StartOffset,
            slide.Offset(ClientScreen.Gang, start));
    }

    [Fact]
    public void DisablingPanelSlidesCancelsEveryInFlightEntrance()
    {
        var slide = new PanelSlideTransition();
        var start = TimeSpan.FromSeconds(4);
        slide.Begin(ClientScreen.City, ClientScreen.Sector, start);

        Assert.Equal(0, slide.Offset(ClientScreen.Sector, start, enabled: false));
        Assert.Equal(0, slide.Offset(ClientScreen.Sector, start, enabled: true));

        slide.Begin(ClientScreen.Sector, ClientScreen.Gang, start);
        Assert.Equal(0, slide.Offset(ClientScreen.Gang, start, enabled: false));
        Assert.Equal(0, slide.Offset(ClientScreen.Gang, start, enabled: true));
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
        Assert.Equal(new Rectangle(164, 17, 70, 110), OriginalSpriteLayout.GangCardFrame);
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
        Assert.Equal(new Rectangle(32, 42, 406, 418), SectorDetailLayout.Workspace);
        Assert.Equal(new Rectangle(85, 417, 116, 4), SectorDetailLayout.SiteControlBar(2));
        Assert.Equal(Color.Lime,
            SectorDetailLayout.SiteControlColor(null, new PlayerId(0)));
        Assert.Equal(Color.Lime,
            SectorDetailLayout.SiteControlColor(new PlayerId(0), new PlayerId(0)));
        Assert.Equal(new Color(190, 0, 220),
            SectorDetailLayout.SiteControlColor(new PlayerId(1), new PlayerId(0)));
        Assert.Equal(new Rectangle(4, 394, 28, 66), SectorDetailLayout.Back);
    }

    [Fact]
    public void SectorGangCardsExposeDetailAndBothActionControls()
    {
        Assert.Equal(new Rectangle(251, 80, 70, 110), SectorGangCardLayout.Frame(0));
        Assert.Equal(new Rectangle(325, 80, 70, 110), SectorGangCardLayout.Frame(1));
        Assert.Equal(new Rectangle(254, 82, 64, 3), SectorGangCardLayout.ForceBar(0));
        Assert.Equal(new Rectangle(254, 87, 30, 15), SectorGangCardLayout.OneOffAction(0));
        Assert.Equal(new Rectangle(287, 87, 30, 15), SectorGangCardLayout.RepeatingAction(0));
        Assert.Equal(new Rectangle(254, 103, 64, 64), SectorGangCardLayout.Portrait(0));
        Assert.Equal(new Rectangle(296, 168, 21, 21), SectorGangCardLayout.ItemSlot(0, 2));
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
        Assert.Equal(new Rectangle(130, 142, 64, 64), GangInformationLayout.Portrait);
        Assert.Equal(new Rectangle(254, 87, 63, 15), SectorGangCardLayout.AssignedCommand(0));
        Assert.Equal(new Rectangle(132, 140, 120, 64), SiteInformationLayout.Portrait);
        Assert.Equal(170, SiteInformationLayout.DataY(0));
        Assert.Equal(188, SiteInformationLayout.DataY(1));
        Assert.Equal(245, SiteInformationLayout.StatisticY(0));
        Assert.Equal(272, SiteInformationLayout.StatisticY(2));
        Assert.Equal(308, SiteInformationLayout.StatisticY(6));
        Assert.Equal(new Rectangle(138, 142, 48, 48), ItemInformationLayout.Portrait);
        Assert.Equal(new Rectangle(152, 156, 20, 20), ItemInformationLayout.CompactPortrait);
        Assert.Equal("RANGE", ItemInformationLayout.TypeLabel(2));
        Assert.Equal("ARMOR", ItemInformationLayout.TypeLabel(3));
        Assert.Equal(244, ItemInformationLayout.StatisticY(0));
        Assert.Equal(271, ItemInformationLayout.StatisticY(2));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatPanelLayout.Panel);
        Assert.Equal(new Rectangle(132, 136, 56, 64), CombatPanelLayout.Sector);
        Assert.Equal(new Rectangle(253, 255, 67, 64), CombatPanelLayout.LeftAction);
        Assert.Equal(new Rectangle(324, 255, 67, 64), CombatPanelLayout.RightAction);
        Assert.Equal(new Rectangle(255, 249, 63, 3), CombatPanelLayout.ForceBar(false));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatResultsLayout.Panel);
        Assert.Equal(new Rectangle(132, 174, 56, 64), CombatResultsLayout.Sector);
        Assert.Equal(new Rectangle(208, 141, 96, 179), CombatResultsLayout.FriendlyPanel);
        Assert.Equal(new Rectangle(313, 289, 32, 32), CombatResultsLayout.Opponent(4));
        Assert.Equal(EquipmentCommandLayout.Cancel, CombatResultsLayout.Detail);
        Assert.Equal(EquipmentCommandLayout.Panel, LastTurnEventsLayout.Panel);
        Assert.Equal(new Rectangle(198, 133, 242, 158), LastTurnEventsLayout.Artwork);
        Assert.Equal(new Rectangle(135, 151, 25, 21), LastTurnEventsLayout.Previous);
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
        Assert.Equal(new Rectangle(360, 38, 32, 32), PlayerPortraitLayout.SetupTop(0));
        Assert.Equal(new Rectangle(540, 38, 32, 32), PlayerPortraitLayout.SetupTop(5));
        Assert.Equal(new Rectangle(8, 4, 32, 32), PlayerPortraitLayout.CityTop(0));
        Assert.Equal(new Rectangle(368, 4, 32, 32), PlayerPortraitLayout.CityTop(5));
        Assert.Equal(new Rectangle(397, 89, 64, 64), PlayerPortraitLayout.SetupLarge(0));
        Assert.Equal(new Rectangle(480, 163, 64, 64), PlayerPortraitLayout.SetupLarge(3));
        Assert.Equal(new Rectangle(399, 257, 12, 18), PlayerPortraitLayout.Previous(4));
        Assert.Equal(new Rectangle(530, 257, 12, 18), PlayerPortraitLayout.Next(5));
        Assert.Equal(new Rectangle(480, 301, 64, 8), PlayerPortraitLayout.Name(5));
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
    public void LastTurnReportsExcludeRoutineEconomyAndMovementNotifications()
    {
        var economy = new GameNotification(0, 2, TurnPhase.Upkeep, null,
            GameNotificationKind.Economy);
        var movement = new GameNotification(1, 2, TurnPhase.Execution, ExecutionPhase.Movement,
            GameNotificationKind.Movement, new GangId(12), 7);
        var control = new GameNotification(2, 2, TurnPhase.Execution, ExecutionPhase.Control,
            GameNotificationKind.Control, new GangId(12), 7, 42);
        var capture = new GameEvent(42, 2, TurnPhase.Execution, ExecutionPhase.Control,
            GameEventKind.CommandResolved, new PlayerId(0), new GangId(12), GangAction.Control,
            CommandTarget.Sector(7), Resolution: new CommandResolutionDetails(
                CommandResolutionCode.Resolved, [], 1));

        Assert.False(NotificationPresentation.IsLastTurnReport(economy, null));
        Assert.False(NotificationPresentation.IsLastTurnReport(movement, null));
        Assert.True(NotificationPresentation.IsLastTurnReport(control, capture));
        Assert.Equal("SECTOR CONTROL ATTAINED.", NotificationPresentation.LastTurnStatus(control));
    }

    [Fact]
    public void LastTurnReportsExcludeGangLossButRetainNamedPlayerElimination()
    {
        var notification = new GameNotification(0, 2, TurnPhase.Execution, ExecutionPhase.Combat,
            GameNotificationKind.Elimination, new GangId(20), 7, 42);
        var gangLoss = new GameEvent(42, 2, TurnPhase.Execution, ExecutionPhase.Combat,
            GameEventKind.CommandResolved, new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)));
        var playerLoss = new GameEvent(42, 2, TurnPhase.PlayerElimination, null,
            GameEventKind.PlayerEliminated, new PlayerId(1), null, GangAction.None,
            CommandTarget.None, Elimination:
            new EliminationDetails(new PlayerId(1), 1));

        Assert.False(NotificationPresentation.IsLastTurnReport(notification, gangLoss));
        Assert.True(NotificationPresentation.IsLastTurnReport(notification, playerLoss));
        Assert.Equal("PLAYER ELIMINATED.", NotificationPresentation.LastTurnStatus(notification));
    }

    [Fact]
    public void AttackTargetGridHasSixNonOverlappingCards()
    {
        var cards = Enumerable.Range(0, AttackCommandLayout.VisibleTargets)
            .Select(AttackCommandLayout.TargetCard).ToArray();

        Assert.Equal(new Rectangle(240, 141, 64, 90), cards[0]);
        Assert.Equal(new Rectangle(372, 231, 64, 90), cards[^1]);
        Assert.All(cards.SelectMany((left, index) => cards.Skip(index + 1)
            .Select(right => (left, right))), pair => Assert.False(pair.left.Intersects(pair.right)));
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
        Assert.Equal(new Rectangle(67, 90, 64, 64), GangArtLayout.DetailPortrait);
        Assert.Equal(new Rectangle(558, 58, 56, 56), GangArtLayout.SelectedEquipmentPortrait);
        Assert.Equal(new Rectangle(18, 108, 20, 20), GangArtLayout.CombatPortrait(0, false));
        Assert.Equal(new Rectangle(42, 372, 20, 20), GangArtLayout.CombatPortrait(11, true));
        Assert.Throws<ArgumentOutOfRangeException>(() => GangArtLayout.CombatPortrait(12, false));
    }

    [Fact]
    public void SiteSearchPanelFitsAllTwentyTwoSiteTypesInTwoColumns()
    {
        var rows = Enumerable.Range(0, SiteSearchLayout.MaximumSites)
            .Select(SiteSearchLayout.Site).ToArray();

        Assert.Equal(new Rectangle(200, 143, 115, 14), rows[0]);
        Assert.Equal(new Rectangle(319, 303, 115, 14), rows[^1]);
        Assert.All(rows.SelectMany((left, index) => rows.Skip(index + 1)
            .Select(right => (left, right))), pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SiteSearchLayout.Site(22));
    }

    [Fact]
    public void HireDockMatchesOriginalThreeCellStripAndRetainsHiredSlot()
    {
        Assert.Equal(new Rectangle(438, 370, 66, 90), HireDockLayout.Cell(0));
        Assert.Equal(new Rectangle(571, 371, 64, 64), HireDockLayout.Portrait(2));
        HireOfferSlotState[] offers =
        [
            HireOfferSlotState.Available(1),
            HireOfferSlotState.Available(2),
            HireOfferSlotState.Available(3)
        ];
        var cells = HireDockLayout.Project(offers, new PendingHireState(2, 12, 1));
        Assert.Equal(new HireDockEntry(1, false), cells[0]);
        Assert.Equal(new HireDockEntry(2, true), cells[1]);
        Assert.Equal(new HireDockEntry(3, false), cells[2]);
        Assert.Equal(new Rectangle(570, 436, 33, 24), HireDockLayout.PriceCell(2));
        Assert.Equal(new Rectangle(603, 436, 33, 24), HireDockLayout.Reject(2));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireDockLayout.Cell(3));
    }

    [Fact]
    public void HireDockCursorUsesPhysicalSlotsAndSkipsVacancies()
    {
        HireOfferSlotState[] offers =
        [
            HireOfferSlotState.Vacant(1),
            HireOfferSlotState.Available(2),
            HireOfferSlotState.Available(3)
        ];

        Assert.Equal(1, HireDockLayout.MoveCursor(offers, -1, 1));
        Assert.Equal(2, HireDockLayout.MoveCursor(offers, 1, 1));
        Assert.Equal(1, HireDockLayout.MoveCursor(offers, 2, 1));
        Assert.Equal(2, HireDockLayout.MoveCursor(offers, 1, -1));
        Assert.Equal(1, HireDockLayout.MoveCursor(offers, 0, 0));
        Assert.Equal(-1, HireDockLayout.MoveCursor(
            [HireOfferSlotState.Vacant(1), HireOfferSlotState.Vacant(2), HireOfferSlotState.Vacant(3)],
            1, 1));
    }

    [Fact]
    public void HireComparisonMatchesOriginalThreeColumnPanel()
    {
        Assert.Equal(new Rectangle(104, 125, 344, 209), HireComparisonLayout.Panel);
        Assert.Equal(EquipmentCommandLayout.Ok, HireComparisonLayout.Ok);
        Assert.Equal(new Rectangle(348, 139, 32, 32), HireComparisonLayout.Portrait(2));
        Assert.Equal(369, HireComparisonLayout.StatRight(2));
        Assert.Equal(311, HireComparisonLayout.StatY(15));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.Portrait(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.StatRight(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.StatY(16));
        Assert.True(HireComparisonLayout.IsBestValue(0, 10, [10, 5, 10]));
        Assert.False(HireComparisonLayout.IsBestValue(0, 5, [10, 5, 10]));
        Assert.True(HireComparisonLayout.IsBestValue(1, 2, [2, 4, 7]));
        Assert.False(HireComparisonLayout.IsBestValue(1, 7, [2, 4, 7]));
    }
}
