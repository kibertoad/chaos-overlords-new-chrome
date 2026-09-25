using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class UiNavigationTests
{
    [Theory]
    [InlineData(0, 3, -1, 0)]
    [InlineData(0, 3, 1, 1)]
    [InlineData(1, 3, -1, 0)]
    [InlineData(1, 3, 1, 2)]
    [InlineData(2, 3, 1, 2)]
    public void RecoveredPageNavigationStopsAtFirstAndLastPage(
        int current, int count, int delta, int expected) =>
        Assert.Equal(expected, BoundedPageNavigation.Move(current, count, delta));

    [Fact]
    public void PageNavigationRejectsEmptyOrInvalidState()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedPageNavigation.Move(0, 0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedPageNavigation.Move(-1, 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => BoundedPageNavigation.Move(2, 2, -1));
    }

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
        Assert.Equal(new Rectangle(130, 141, 64, 64), AttackCommandLayout.ActorPortrait);
        Assert.Equal(new Rectangle(240, 140, 64, 64), AttackCommandLayout.TargetPortrait(0));
        Assert.Equal(new Rectangle(202, 140, 32, 32), AttackCommandLayout.Opponent(0));
        Assert.Equal(new Rectangle(202, 284, 32, 32), AttackCommandLayout.Opponent(4));
        Assert.Equal(new Rectangle(130, 206, 20, 20), AttackCommandLayout.ActorItem(0));
        Assert.Equal(new Rectangle(284, 206, 20, 20), AttackCommandLayout.TargetItem(0, 2));
        Assert.Equal(new Rectangle(0, 240, 20, 20), OriginalSpriteLayout.ItemPortrait(12));
    }

    [Fact]
    public void GangInformationStatisticsFollowTemplateRows()
    {
        Assert.Equal([243, 252, 270, 279, 288, 297, 306],
            Enumerable.Range(0, 7).Select(GangInformationLayout.StatisticY));
        Assert.Equal(276, GangInformationLayout.LeftValueLeft);
        Assert.Equal(372, GangInformationLayout.RightValueLeft);
    }

    [Fact]
    public void InfluencePickerUsesOriginalStaggeredSiteLayout()
    {
        Assert.Equal(new Rectangle(130, 141, 64, 64), InfluenceCommandLayout.Portrait);
        Assert.Equal(new Rectangle(209, 140, 120, 64), InfluenceCommandLayout.Site(0));
        Assert.Equal(new Rectangle(312, 197, 120, 64), InfluenceCommandLayout.Site(1));
        Assert.Equal(new Rectangle(209, 254, 120, 64), InfluenceCommandLayout.Site(2));
    }

    [Fact]
    public void GivePickerUsesNativeItemSelectionTargets()
    {
        Assert.Equal(new Rectangle(207, 139, 52, 52), EquipmentGiveLayout.ItemHit(0));
        Assert.Equal(new Rectangle(207, 203, 52, 52), EquipmentGiveLayout.ItemHit(1));
        Assert.Equal(new Rectangle(207, 267, 52, 52), EquipmentGiveLayout.ItemHit(2));
        Assert.Equal(new Rectangle(208, 140, 50, 51), EquipmentGiveLayout.Item(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => EquipmentGiveLayout.ItemHit(3));
    }

    [Fact]
    public void StatusConsoleValuesFollowTemplateRows()
    {
        Assert.Equal(579, StatusConsoleLayout.ValueRight);
        Assert.Equal(new Rectangle(476, 95, 44, 9), StatusConsoleLayout.CashLabel);
        Assert.Equal(new Rectangle(476, 41, 108, 9), StatusConsoleLayout.Cash);
        Assert.Equal(12, StatusConsoleLayout.CashValueMaxCharacters);
        Assert.Equal([60, 69, 78, 87, 96],
            Enumerable.Range(0, 5).Select(StatusConsoleLayout.SectorValueY));
    }

    [Fact]
    public void EveryStatusConsoleStatisticHasAnExplanatoryHoverTooltip()
    {
        Rectangle[] entries =
        [
            StatusConsoleLayout.Score,
            StatusConsoleLayout.Cash,
            .. Enumerable.Range(0, 5).Select(StatusConsoleLayout.SectorEntry)
        ];

        Assert.All(entries, entry =>
        {
            var lines = StatusConsoleTooltip.At(entry.Center);
            Assert.True(lines.Count >= 2);
            var bounds = StatusConsoleTooltip.Bounds(entry.Center, lines);
            Assert.NotEqual(Rectangle.Empty, bounds);
            Assert.True(bounds.X >= 0 && bounds.Y >= 0
                && bounds.Right <= VirtualInput.Width && bounds.Bottom <= VirtualInput.Height);
        });
        var income = string.Join(' ', StatusConsoleTooltip.At(
            StatusConsoleLayout.SectorEntry(1).Center));
        Assert.Contains("CHAOS DICE", income);
        Assert.Contains("NOT PASSIVE CASH", income);
        Assert.Contains("$1 SECTOR TAX", income);
        var sectorCash = string.Join(' ', StatusConsoleTooltip.At(
            StatusConsoleLayout.SectorEntry(4).Center));
        Assert.Contains("OWNER-ONLY", sectorCash);
        Assert.Equal(7, StatusConsolePresentation.SectorCash(
            new PlayerId(1), new PlayerId(1), 7));
        Assert.Equal(0, StatusConsolePresentation.SectorCash(
            new PlayerId(1), new PlayerId(0), 7));
        Assert.Equal("+1", StatusConsolePresentation.ProjectedChange(1));
        Assert.Equal("-3", StatusConsolePresentation.ProjectedChange(-3));
        Assert.Equal("0", StatusConsolePresentation.ProjectedChange(0));
        Assert.Equal("20 [20] (+1)", StatusConsolePresentation.CashSummary(20, 20, 1));
        Assert.Equal("5 [-3] (0)", StatusConsolePresentation.CashSummary(5, -3, 0));
        Assert.Equal("120[95](-12)", StatusConsolePresentation.CashSummary(120, 95, -12));
        Assert.Empty(StatusConsoleTooltip.At(Point.Zero));
    }

    [Fact]
    public void GangSiteAndItemEffectsExplainTheirRulesAndScope()
    {
        Assert.Equal(30, ItemInformationLayout.DescriptionColumns);
        foreach (var row in Enumerable.Range(0, 7))
        {
            var gangLeft = InformationEffectTooltips.GangAt(
                new Point(220, GangInformationLayout.StatisticY(row)));
            var gangRight = InformationEffectTooltips.GangAt(
                new Point(320, GangInformationLayout.StatisticY(row)));
            var siteLeft = InformationEffectTooltips.SiteAt(
                new Point(244, SiteInformationLayout.StatisticY(row)));
            var itemRight = InformationEffectTooltips.ItemAt(
                new Point(320, ItemInformationLayout.StatisticY(row)));

            Assert.True(gangLeft.Count >= 3);
            Assert.True(gangRight.Count >= 3);
            Assert.Contains("SECTOR", siteLeft[^1]);
            Assert.Contains("EQUIPPED", itemRight[^1]);
        }

        Assert.StartsWith("FORCE", InformationEffectTooltips.GangAt(new Point(220, 216))[0]);
        Assert.StartsWith("RESISTANCE", InformationEffectTooltips.SiteAt(new Point(300, 169))[0]);
        Assert.StartsWith("COST", InformationEffectTooltips.ItemAt(new Point(220, 216))[0]);
        Assert.Contains("ADDED TO COMBAT", InformationEffectTooltips.Describe(
            InformationEffect.Blade, "SCOPE")[1]);
        Assert.Contains("ADDED TO COMBAT", InformationEffectTooltips.Describe(
            InformationEffect.Range, "SCOPE")[1]);
        Assert.Contains("INFLUENCE ACTION", InformationEffectTooltips.Describe(
            InformationEffect.Influence, "SCOPE")[1]);
        Assert.Contains("OWNER'S DEFENSE", InformationEffectTooltips.Describe(
            InformationEffect.Control, "SCOPE")[1]);
        var strength = InformationEffectTooltips.Describe(
            InformationEffect.Strength, "SCOPE")[1];
        Assert.Contains("STRENGTH/BLADE TYPES", strength);
        Assert.DoesNotContain("MELEE", strength);
        var fighting = InformationEffectTooltips.Describe(
            InformationEffect.Fighting, "SCOPE")[1];
        Assert.Contains("UNARMED", fighting);
        Assert.DoesNotContain("BARE", fighting);
        Assert.Empty(InformationEffectTooltips.GangAt(Point.Zero));
    }

    // SCR-SETUP-001, FND-SETUP-013: the right column of the left panel.
    [Fact]
    public void SetupPlanningTimerButtonsMatchOriginalArtworkRows()
    {
        Assert.Equal(
        [
            new Rectangle(194, 337, 110, 24), new Rectangle(194, 364, 110, 24),
            new Rectangle(194, 391, 110, 24), new Rectangle(194, 418, 110, 24)
        ], PlanningTimerLayout.SetupChoices);
        Assert.Equal(new Rectangle(520, 336, 60, 3), PlanningTimerLayout.Bar);
    }

    [Fact]
    public void SetupSelectionBordersUseInsetButtonFacesInsteadOfBroadHitRows()
    {
        Assert.Equal(new Rectangle(80, 109, 108, 31), SetupSelectionLayout.Scenario(0));
        Assert.Equal(new Rectangle(192, 250, 108, 31), SetupSelectionLayout.Scenario(9));
        Assert.Equal(new Rectangle(80, 285, 50, 23), SetupSelectionLayout.Duration(0));
        Assert.Equal(new Rectangle(248, 285, 52, 23), SetupSelectionLayout.Duration(3));
        Assert.Equal(new Rectangle(80, 364, 108, 23), SetupSelectionLayout.AiMentality(1));
        Assert.Equal(new Rectangle(192, 418, 108, 23), SetupSelectionLayout.PlanningTime(3));
        Assert.Equal(new Rectangle(183, 112, 3, 11),
            OriginalSelectionLightLayout.Scenario(0));
        Assert.Equal(new Rectangle(297, 253, 3, 11),
            OriginalSelectionLightLayout.Scenario(9));
        Assert.Equal(new Rectangle(126, 288, 3, 11),
            OriginalSelectionLightLayout.Duration(0));
        Assert.Equal(new Rectangle(297, 288, 3, 11),
            OriginalSelectionLightLayout.Duration(3));
        Assert.Equal(new Rectangle(183, 421, 3, 11),
            OriginalSelectionLightLayout.AiMentality(3));
        Assert.Equal(new Rectangle(297, 340, 3, 11),
            OriginalSelectionLightLayout.PlanningTime(0));
        Assert.Equal(new Rectangle(523, 38, 3, 11),
            OriginalSelectionLightLayout.EndgameTab(EndgameLayout.Stats));
        Assert.Equal(new Rectangle(541, 129, 3, 11),
            OriginalSelectionLightLayout.CityEvents);
        Assert.Equal(new Rectangle(593, 129, 3, 11),
            OriginalSelectionLightLayout.CityComlinkView);
        Assert.Throws<ArgumentOutOfRangeException>(() => SetupSelectionLayout.Scenario(10));
        Assert.Throws<ArgumentOutOfRangeException>(() => SetupSelectionLayout.Duration(4));
    }

    [Fact]
    public void SetupScenarioButtonsFollowTheirBakedVisualLabels()
    {
        Assert.Equal(
            [
                ScenarioId.KillEmAll, ScenarioId.Big40,
                ScenarioId.Siege, ScenarioId.Eliminate,
                ScenarioId.BigMan, ScenarioId.Armageddon,
                ScenarioId.Greed, ScenarioId.Power,
                ScenarioId.Acceptance, ScenarioId.Dominance
            ],
            SetupScenarioButtons.VisualOrder);
        Assert.Equal(0, SetupScenarioButtons.ButtonForScenario(ScenarioId.KillEmAll));
        Assert.Equal(6, SetupScenarioButtons.ButtonForScenario(ScenarioId.Greed));
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
        Assert.Equal(new Rectangle(220, 138, 64, 62),
            SetupPlayerCardArtLayout.ArrowOverlaySource);
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
        Assert.Equal(new Rectangle(472, 437, 32, 13), HireDockLayout.Reject(0));
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
    public void NextPlayerPortraitFillsTheNativeHandoffAperture()
    {
        Assert.Equal(new Rectangle(266, 148, 108, 164), HandoffLayout.Panel);
        Assert.Equal(new Rectangle(280, 170, 80, 77), HandoffLayout.Portrait);
        Assert.Equal(new Rectangle(266, 246, 108, 66), HandoffLayout.Ready);
        Assert.True(HandoffLayout.Panel.Contains(HandoffLayout.Portrait));
    }

    [Fact]
    public void DroppingOwnGangOnDisplayedEnemySelectsAttackTarget()
    {
        var owner = new PlayerId(0);
        MatchGangState[] gangs =
        [
            new(new GangId(10), owner, 0, 0, 5),
            new(new GangId(20), new PlayerId(1), 1, 0, 5)
        ];

        Assert.Null(SectorGangDropTarget.EnemyAt(
            gangs, owner, SectorGangCardLayout.Frame(0).Center));
        Assert.Equal(new GangId(20), SectorGangDropTarget.EnemyAt(
            gangs, owner, SectorGangCardLayout.Frame(1).Center));
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

        Assert.Equal(new Rectangle(4, 3, 54, 52), CityMapLayout.Source(0));
        Assert.Equal(new Rectangle(375, 360, 54, 52), CityMapLayout.Source(63));
        Assert.Equal(new Rectangle(2, 44, 432, 416), CityMapLayout.Bounds);
        Assert.Equal(new Rectangle(5, 4, 52, 50), CityMapLayout.OwnershipSource(0));
        Assert.Equal(new Rectangle(7, 48, 52, 50), CityMapLayout.OwnershipDestination(0));
        Assert.Equal(new Rectangle(376, 361, 52, 50), CityMapLayout.OwnershipSource(63));
        Assert.Equal(new Rectangle(378, 405, 52, 50), CityMapLayout.OwnershipDestination(63));
        Assert.Equal(0, CityMapLayout.OwnershipSheet(null));
        Assert.Equal(6, CityMapLayout.OwnershipSheet(new PlayerId(5)));
        Assert.False(CityMapLayout.TrySectorAt(new Point(432, 456), out _));
    }

    [Fact]
    public void ObjectivePylonOverlayUsesNativeAtlasCellAndScenarioSectors()
    {
        Assert.Equal(new Rectangle(344, 15, 54, 52),
            OriginalSpriteLayout.ObjectiveSectorPylons);
        Assert.True(ObjectiveSectorMarkerPresentation.IsMarked(
            ScenarioId.Siege, 9, isImportant: true));
        Assert.False(ObjectiveSectorMarkerPresentation.IsMarked(
            ScenarioId.Siege, 9, isImportant: false));
        Assert.Equal([27, 28, 35, 36], Enumerable.Range(0, MatchLimits.SectorCount)
            .Where(sectorId => ObjectiveSectorMarkerPresentation.IsMarked(
                ScenarioId.BigMan, sectorId, isImportant: false)));
        Assert.False(ObjectiveSectorMarkerPresentation.IsMarked(
            ScenarioId.Greed, 27, isImportant: true));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ObjectiveSectorMarkerPresentation.IsMarked(
                ScenarioId.BigMan, MatchLimits.SectorCount, isImportant: false));
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
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.EventSiteImages));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.AdvancedAi));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.IntroOnlyOnce));
        Assert.True(OptionsLayout.Panel.Contains(OptionsLayout.ExportDiagnostics));
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
            OptionsLayout.EventSiteImages,
            OptionsLayout.AdvancedAi,
            OptionsLayout.IntroOnlyOnce,
            OptionsLayout.ExportDiagnostics,
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
        Assert.Contains("ORIGINAL HOST-LOBBY ART",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.ColorDepth.Center)));
        Assert.Contains("IMMEDIATELY",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.SlidePanels.Center)));
        Assert.Contains("NATIVE STRETCH AND ORDERED DITHER",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.EventSiteImages.Center)));
        Assert.Contains("DOES NOT CHANGE A MATCH ALREADY IN PROGRESS",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.AdvancedAi.Center)));
        Assert.Contains("FALLBACK COMMANDS FOR GANGS ORIGINAL AI LEAVES IDLE",
            string.Join(' ', OptionsTooltip.At(OptionsLayout.AdvancedAi.Center)));
        var diagnosticsTooltip = string.Join(' ',
            OptionsTooltip.At(OptionsLayout.ExportDiagnostics.Center));
        Assert.Contains("DOES NOT INCLUDE REPLAYABLE MATCH STATE", diagnosticsTooltip);
        Assert.Contains("REPORT BUG", diagnosticsTooltip);
        Assert.Contains("CLIENT PROBLEMS A MATCH REPLAY CANNOT SHOW", diagnosticsTooltip);
        Assert.Empty(OptionsTooltip.At(Point.Zero));
        Assert.Equal(Rectangle.Empty, OptionsTooltip.Bounds(Point.Zero, []));
    }

    [Fact]
    public void RecurringCommandMenuOmitsOneOffActions()
    {
        var actions = CommandOverlayLayout.ActionsFor(recurring: true);

        Assert.Equal([
            GangAction.Chaos, GangAction.Control, GangAction.Heal, GangAction.Hide,
            GangAction.Influence, GangAction.Research, GangAction.None
        ], actions);
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
        Assert.Equal(new Rectangle(114, 299, 64, 64), OriginalSpriteLayout.HiredStamp);
        Assert.Equal(new Rectangle(178, 299, 64, 64), OriginalSpriteLayout.SnubbedStamp);
        Assert.Equal(new Rectangle(492, 67, 20, 20), OriginalSpriteLayout.AssignedGangStatus);
        Assert.Equal(new Rectangle(492, 87, 20, 20), OriginalSpriteLayout.ContestedAssignedGangStatus);
        Assert.Equal(new Rectangle(492, 107, 20, 20), OriginalSpriteLayout.IdleGangStatus);
        Assert.Equal(new Rectangle(492, 127, 20, 20), OriginalSpriteLayout.ContestedIdleGangStatus);
        Assert.Equal(new Rectangle(492, 227, 20, 20), OriginalSpriteLayout.IncomingGangStatus);
        Assert.Equal(new Rectangle(492, 207, 20, 20), OriginalSpriteLayout.GangStatus(7));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.GangStatus(9));
        Assert.Equal(new Rectangle(150, 386, 40, 40), OriginalSpriteLayout.SetupDragFrame);
        Assert.Equal(new Rectangle(120, 211, 30, 47), OriginalSpriteLayout.SectorBackArrow);
        Assert.Equal(new Rectangle(0, 626, 20, 20), OriginalSpriteLayout.ActivePlayerMarker(0));
        Assert.Equal(new Rectangle(220, 626, 20, 20), OriginalSpriteLayout.ActivePlayerMarker(11));
        Assert.Equal(new Rectangle(480, 480, 32, 32), OriginalSpriteLayout.OverlordPortrait(15));
        Assert.Equal(new Rectangle(480, 480, 32, 30), SetupPlayerCardArtLayout.PortraitSource(15));
        Assert.Equal(new Rectangle(0, 0, 120, 64), OriginalSpriteLayout.SitePortrait(0));
        Assert.Equal(new Rectangle(0, 21 * 64, 120, 64), OriginalSpriteLayout.SitePortrait(21));
        Assert.Equal(new Rectangle(0, 0, 64, 64), OriginalSpriteLayout.GangPortrait(0));
        Assert.Equal(new Rectangle(576, 512, 64, 64), OriginalSpriteLayout.GangPortrait(89));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.SitePortrait(22));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.GangPortrait(90));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.ActivePlayerMarker(12));
    }

    [Fact]
    public void GangStatusMarkerOccupiesLowerRightOfSectorCell()
    {
        Assert.Equal(new Rectangle(38, 67, 20, 20), GangStatusMarkerLayout.Destination(0));
        Assert.Equal(new Rectangle(409, 424, 20, 20), GangStatusMarkerLayout.Destination(63));
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
        Assert.Equal(new Rectangle(93, 417, 100, 3), SectorDetailLayout.SiteControlBar(2));
        Assert.Equal(new Color(0, 247, 0),
            SectorDetailLayout.SiteControlColor(null, new PlayerId(0)));
        Assert.Equal(new Color(0, 247, 0),
            SectorDetailLayout.SiteControlColor(new PlayerId(0), new PlayerId(0)));
        Assert.Equal(new Color(190, 0, 220),
            SectorDetailLayout.SiteControlColor(new PlayerId(1), new PlayerId(0)));
        Assert.Equal(new PlayerId(1), SectorDetailLayout.SiteControlOwner(
            influencedBy: null, sectorOwner: new PlayerId(1), resistance: 0));
        Assert.Null(SectorDetailLayout.SiteControlOwner(
            influencedBy: null, sectorOwner: new PlayerId(1), resistance: 4));
        Assert.Equal(0, SectorDetailLayout.SiteControlWidth(10, 10));
        Assert.Equal(30, SectorDetailLayout.SiteControlWidth(10, 7));
        Assert.Equal(100, SectorDetailLayout.SiteControlWidth(0, 0));
        Assert.Equal(new Rectangle(4, 394, 28, 66), SectorDetailLayout.Back);
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
        Assert.Equal(new Rectangle(256, 70, 158, 22), CommandOverlayLayout.ActionRow(0));
        Assert.Equal(new Rectangle(104, 124, 344, 209), EquipmentCommandLayout.Panel);
        Assert.Equal(new Rectangle(251, 149, 181, 9), EquipmentCommandLayout.ItemRow(0));
        Assert.Equal(new Rectangle(207, 140, 34, 34), EquipmentCommandLayout.Category(0));
        Assert.Equal(new Rectangle(207, 248, 34, 34), EquipmentCommandLayout.Category(3));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(0));
        Assert.Equal(0, EquipmentCommandLayout.CategoryForItemType(1));
        Assert.Equal(1, EquipmentCommandLayout.CategoryForItemType(2));
        Assert.Equal(2, EquipmentCommandLayout.CategoryForItemType(3));
        Assert.Equal(3, EquipmentCommandLayout.CategoryForItemType(4));
        Assert.Equal(new Rectangle(130, 141, 64, 64), GangInformationLayout.Portrait);
        Assert.Equal(new Rectangle(259, 88, 64, 9), SectorGangCardLayout.AssignedCommand(0));
        Assert.Equal(new Rectangle(156, 139, 120, 64), SiteInformationLayout.Portrait);
        Assert.Equal(169, SiteInformationLayout.DataY(0));
        Assert.Equal(187, SiteInformationLayout.DataY(1));
        Assert.Equal(244, SiteInformationLayout.StatisticY(0));
        Assert.Equal(271, SiteInformationLayout.StatisticY(2));
        Assert.Equal(307, SiteInformationLayout.StatisticY(6));
        Assert.Equal(new Rectangle(162, 141, 48, 48), ItemInformationLayout.Portrait);
        Assert.Equal(new Rectangle(176, 155, 20, 20), ItemInformationLayout.CompactPortrait);
        Assert.Equal("RANGE", ItemInformationLayout.TypeLabel(2));
        Assert.Equal("ARMOR", ItemInformationLayout.TypeLabel(3));
        Assert.Equal(243, ItemInformationLayout.StatisticY(0));
        Assert.Equal(270, ItemInformationLayout.StatisticY(2));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatPanelLayout.Panel);
        Assert.Equal(new Rectangle(135, 135, 54, 52), CombatPanelLayout.Sector);
        Assert.Equal(new Point(156, 190), CombatPanelLayout.SectorCodeText);
        Assert.Equal(EquipmentCommandLayout.Ok, CombatPanelLayout.Cancel);
        Assert.Equal(new Rectangle(253, 254, 67, 64), CombatPanelLayout.LeftAction);
        Assert.Equal(new Rectangle(324, 254, 67, 64), CombatPanelLayout.RightAction);
        Assert.Equal(new Rectangle(254, 172, 64, 64), CombatPanelLayout.GangPortrait(false));
        Assert.Equal(new Rectangle(327, 172, 64, 64), CombatPanelLayout.GangPortrait(true));
        Assert.Equal(new Rectangle(254, 254, 64, 64), CombatPanelLayout.Animation(false));
        Assert.Equal(new Rectangle(327, 254, 64, 64), CombatPanelLayout.Animation(true));
        Assert.Equal(new Rectangle(262, 172, 48, 64), CombatPanelLayout.PolicePortrait(false));
        Assert.Equal(new Rectangle(335, 172, 48, 64), CombatPanelLayout.PolicePortrait(true));
        Assert.Equal(new Rectangle(256, 238, 60, 3), CombatPanelLayout.ForceBar(false, 0));
        Assert.Equal(new Rectangle(256, 245, 60, 3), CombatPanelLayout.ForceBar(false, 1));
        Assert.Equal(new Rectangle(329, 238, 60, 3), CombatPanelLayout.ForceBar(true, 0));
        Assert.Equal(new Rectangle(329, 245, 60, 3), CombatPanelLayout.ForceBar(true, 1));
        Assert.Equal(EquipmentCommandLayout.Panel, CombatResultsLayout.Panel);
        Assert.Equal(new Rectangle(135, 191, 54, 52), CombatResultsLayout.Sector);
        Assert.Equal(new Rectangle(202, 140, 94, 179), CombatResultsLayout.FriendlyPanel);
        Assert.Equal(new Rectangle(207, 153, 40, 40), CombatResultsLayout.Force(0, enemy: false));
        Assert.Equal(new Rectangle(394, 257, 40, 40), CombatResultsLayout.Force(5, enemy: true));
        Assert.Equal(new Rectangle(306, 284, 32, 32), CombatResultsLayout.Opponent(4));
        Assert.Equal(new Point(138, 137), CombatResultsLayout.PageText);
        Assert.Equal(new Point(156, 246), CombatResultsLayout.SectorCodeText);
        Assert.Equal(EquipmentCommandLayout.Panel, LastTurnEventsLayout.Panel);
        Assert.Equal(new Rectangle(138, 137, 47, 7), LastTurnEventsLayout.Page);
        Assert.Equal(new Rectangle(198, 132, 242, 158), LastTurnEventsLayout.Artwork);
        Assert.Equal(new Rectangle(296, 186, 48, 48), LastTurnEventsLayout.ResearchItem);
        Assert.Equal(new Rectangle(135, 157, 26, 23), LastTurnEventsLayout.Previous);
        Assert.Equal(new Rectangle(225, 298, 43, 7), LastTurnEventsLayout.DateValue);
        Assert.Equal(new Rectangle(305, 298, 135, 7), LastTurnEventsLayout.ObjectValue);
        Assert.Equal(new Rectangle(239, 307, 201, 7), LastTurnEventsLayout.StatusValue);
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
        Assert.Equal(new Rectangle(16, 4, 32, 32), PlayerPortraitLayout.CityTop(0));
        Assert.Equal(new Rectangle(376, 4, 32, 32), PlayerPortraitLayout.CityTop(5));
        Assert.Equal(new Rectangle(48, 4, 20, 20), PlayerPortraitLayout.CityActiveMarker(0));
        Assert.Equal(new Rectangle(408, 4, 20, 20), PlayerPortraitLayout.CityActiveMarker(5));
        Assert.Equal(new Rectangle(397, 89, 64, 64), PlayerPortraitLayout.SetupLarge(0));
        Assert.Equal(new Rectangle(480, 163, 64, 64), PlayerPortraitLayout.SetupLarge(3));
        Assert.Equal(new Rectangle(397, 92, 64, 60),
            SetupPlayerCardArtLayout.PortraitDestination(0));
        Assert.Equal(new Rectangle(480, 166, 64, 60),
            SetupPlayerCardArtLayout.PortraitDestination(3));
        Assert.Equal(new Rectangle(399, 257, 12, 18), PlayerPortraitLayout.Previous(4));
        Assert.Equal(new Rectangle(530, 257, 12, 18), PlayerPortraitLayout.Next(5));
        Assert.Equal(new Rectangle(480, 301, 64, 8), PlayerPortraitLayout.Name(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlayerPortraitLayout.SetupTop(6));
    }

    [Fact]
    public void ActivePlayerMarkerCyclesAllTwelveRecoveredFrames()
    {
        Assert.Equal(0, ActivePlayerMarkerPresentation.Frame(TimeSpan.Zero));
        Assert.Equal(11, ActivePlayerMarkerPresentation.Frame(TimeSpan.FromMilliseconds(11 * 80)));
        Assert.Equal(0, ActivePlayerMarkerPresentation.Frame(TimeSpan.FromMilliseconds(12 * 80)));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ActivePlayerMarkerPresentation.Frame(TimeSpan.FromMilliseconds(-1)));
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

    [Theory]
    [InlineData(GameNotificationKind.HireInsufficientCash, "INSUFFICIENT CASH TO HIRE.")]
    [InlineData(GameNotificationKind.HireSectorFull, "UNABLE TO HIRE, SECTOR AT CAPACITY.")]
    [InlineData(GameNotificationKind.HireGangLimit, "UNABLE TO HIRE, MAX GANGS REACHED.")]
    public void FailedHiresAreExplicitLastTurnReports(
        GameNotificationKind kind,
        string expectedStatus)
    {
        var gameEvent = new GameEvent(42, 2, TurnPhase.Hire, null,
            GameEventKind.HireFailed, new PlayerId(0), null, GangAction.None,
            CommandTarget.Sector(7), Hire: new HireResolutionDetails(2, 7, 5));
        var notification = new GameNotification(0, 2, TurnPhase.Hire, null,
            kind, SectorId: 7, RelatedEventSequence: 42);

        Assert.True(NotificationPresentation.IsLastTurnReport(notification, gameEvent));
        Assert.Equal(expectedStatus, NotificationPresentation.LastTurnStatus(notification));
    }

    [Theory]
    [InlineData(GangAction.Bribe, GameNotificationKind.CommandResult,
        "INSUFFICIENT CASH TO BRIBE.")]
    [InlineData(GangAction.Equip, GameNotificationKind.Equipment,
        "INSUFFICIENT CASH TO EQUIP.")]
    public void CashDependentCommandFailuresAreExplicitLastTurnReports(
        GangAction action,
        GameNotificationKind notificationKind,
        string expectedStatus)
    {
        var gameEvent = new GameEvent(42, 2, TurnPhase.Execution, ExecutionPhase.Instant,
            GameEventKind.CommandFailed, new PlayerId(0), new GangId(10), action,
            CommandTarget.None, Resolution: new CommandResolutionDetails(
                CommandResolutionCode.InsufficientCash, [], 0));
        var notification = new GameNotification(0, 2, TurnPhase.Execution,
            ExecutionPhase.Instant, notificationKind, new GangId(10), 7, 42);

        Assert.True(NotificationPresentation.IsLastTurnReport(notification, gameEvent));
        Assert.Equal(expectedStatus,
            NotificationPresentation.LastTurnStatus(notification, gameEvent));
    }

    [Theory]
    [InlineData(-3, "HIRE SHORTFALL: $3")]
    [InlineData(0, "")]
    [InlineData(6, "")]
    public void HireReservationWarningExplainsOnlyFinancialRisk(
        int projectedCash,
        string expected) =>
        Assert.Equal(expected,
            HireReservationWarning.For(projectedCash));

    [Fact]
    public void HireWarningFitsCityStatusLineForLargestDebt()
    {
        var warning = HireReservationWarning.For(int.MinValue);

        Assert.True(CityStatusMessage.Fits(warning));
        Assert.Equal("HIRE SHORTFALL: $2147483648", warning);
        Assert.Throws<ArgumentException>(() =>
            CityStatusMessage.RequireFit(new string('X', CityStatusMessage.MaxCharacters + 1)));
    }

    [Fact]
    public void HireWarningProjectionUsesExecutionAndContractButNotNextUpkeep()
    {
        var projection = new FinanceProjection(
            GangUpkeep: -8,
            NewContracts: -5,
            ProjectedGangCount: 2,
            Equipment: 7,
            CityOfficials: -3,
            SectorTax: 2,
            SiteProtection: 4,
            ChaosEstimate: 1,
            CashAdjustment: -2);

        Assert.Equal(10,
            HireReservationWarning.ProjectedBalanceAtHire(10, projection));
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
        Assert.Equal("PLAYER HAS BEEN ELIMINATED.",
            NotificationPresentation.LastTurnStatus(notification));
    }

    [Fact]
    public void AttackTargetGridHasSixNonOverlappingCards()
    {
        var cards = Enumerable.Range(0, AttackCommandLayout.VisibleTargets)
            .Select(AttackCommandLayout.TargetCard).ToArray();

        Assert.Equal(new Rectangle(240, 140, 64, 90), cards[0]);
        Assert.Equal(new Rectangle(372, 230, 64, 90), cards[^1]);
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

        Assert.Equal(new Rectangle(206, 146, 114, 15), rows[0]);
        Assert.Equal(new Rectangle(322, 296, 114, 15), rows[^1]);
        Assert.All(rows.SelectMany((left, index) => rows.Skip(index + 1)
            .Select(right => (left, right))), pair => Assert.False(pair.left.Intersects(pair.right)));
        Assert.Throws<ArgumentOutOfRangeException>(() => SiteSearchLayout.Site(22));
    }

    [Fact]
    public void HireComparisonMatchesOriginalThreeColumnPanel()
    {
        Assert.Equal(new Rectangle(128, 124, 320, 209), HireComparisonLayout.Panel);
        Assert.Equal(new Rectangle(0, 0, 320, 209), HireComparisonLayout.BackgroundSource);
        Assert.Equal(new Rectangle(161, 293, 49, 22), HireComparisonLayout.Ok);
        Assert.Equal(new Rectangle(372, 138, 32, 32), HireComparisonLayout.Portrait(2));
        Assert.Equal(394, HireComparisonLayout.StatRight(2));
        Assert.Equal(310, HireComparisonLayout.StatY(15));
        Assert.Equal(new Rectangle(382, 172, 12, 7), HireComparisonLayout.ValueCell(2, 0));
        Assert.Contains("ADDED TO COMBAT", InformationEffectTooltips.HireAt(
            new Point(244, HireComparisonLayout.StatY(12)))[1]);
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.Portrait(3));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.StatRight(-1));
        Assert.Throws<ArgumentOutOfRangeException>(() => HireComparisonLayout.StatY(16));
    }

}
