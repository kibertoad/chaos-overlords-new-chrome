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
    public void OriginalPortraitLayoutsCoverAllDefinitionSlots()
    {
        Assert.Equal(new Rectangle(116, 0, 48, 64), OriginalSpriteLayout.PolicePatrolCar);
        Assert.Equal(new Rectangle(0, 0, 120, 64), OriginalSpriteLayout.SitePortrait(0));
        Assert.Equal(new Rectangle(0, 21 * 64, 120, 64), OriginalSpriteLayout.SitePortrait(21));
        Assert.Equal(new Rectangle(0, 0, 64, 64), OriginalSpriteLayout.GangPortrait(0));
        Assert.Equal(new Rectangle(576, 512, 64, 64), OriginalSpriteLayout.GangPortrait(89));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.SitePortrait(22));
        Assert.Throws<ArgumentOutOfRangeException>(() => OriginalSpriteLayout.GangPortrait(90));
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
    }
}
