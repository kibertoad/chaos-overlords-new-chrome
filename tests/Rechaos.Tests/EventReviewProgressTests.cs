using Microsoft.Xna.Framework;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class EventReviewProgressTests
{
    [Fact]
    public void ReviewCompletesOnlyAfterEveryPageWasVisited()
    {
        Assert.False(EventReviewProgress.IsComplete(3, new HashSet<int> { 0, 2 }));
        Assert.True(EventReviewProgress.IsComplete(3, new HashSet<int> { 0, 1, 2 }));
    }

    [Fact]
    public void EmptyReportSetIsAlreadyComplete()
    {
        Assert.True(EventReviewProgress.IsComplete(0, new HashSet<int>()));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            EventReviewProgress.IsComplete(-1, new HashSet<int>()));
    }

    [Fact]
    public void ResearchReportUsesTheResolvedItem()
    {
        var related = new GameEvent(4, 2, TurnPhase.Execution, ExecutionPhase.Instant,
            GameEventKind.CommandResolved, new PlayerId(0), new GangId(10),
            GangAction.Research, CommandTarget.Item(12));
        var notification = new GameNotification(3, 2, TurnPhase.Execution,
            ExecutionPhase.Instant, GameNotificationKind.Research,
            new GangId(10), RelatedEventSequence: related.Sequence);

        Assert.Equal(12, LastTurnEventPresentation.ResearchItemId(notification, related));
        Assert.True(LastTurnEventsLayout.Artwork.Contains(LastTurnEventsLayout.ResearchItem));
        Assert.Equal(new Rectangle(0, 0, 48, 48),
            ItemRotationPresentation.Frame(TimeSpan.Zero));
        Assert.Equal(new Rectangle(14 * 48, 0, 48, 48),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(14 * 80)));
        Assert.Equal(new Rectangle(0, 0, 48, 48),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(15 * 80)));
    }

    [Fact]
    public void InfluenceReportUsesSiteObjectCooperationTextAndForegroundArtwork()
    {
        var state = OriginalMatchFactory.Create(
            BundledOriginalData.Load(),
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996,
            [
                new MatchPlayerSetup(
                    new PlayerId(0), "PLAYER 1", PlayerController.Human, 0)
            ], allowSparsePlayerIds: true));
        const int siteId = 4;
        var related = new GameEvent(7, 3, TurnPhase.Execution, ExecutionPhase.Instant,
            GameEventKind.CommandResolved, new PlayerId(0), new GangId(0),
            GangAction.Influence, CommandTarget.Site(siteId));
        var notification = new GameNotification(6, 3, TurnPhase.Execution,
            ExecutionPhase.Instant, GameNotificationKind.Influence,
            new GangId(0), RelatedEventSequence: related.Sequence);
        var definition = state.Definitions.Sites.Single(value =>
            value.Id == state.FindSite(siteId)!.DefinitionId);

        Assert.Equal(siteId, LastTurnEventPresentation.InfluenceSiteId(notification, related));
        Assert.Equal($"04:{definition.Name}",
            LastTurnEventPresentation.InfluenceSiteObject(state, notification, related));
        Assert.Equal(4, LastTurnEventPresentation.ArtworkIndex(notification, related));
        Assert.Equal("SITE COOPERATION ACHIEVED.",
            NotificationPresentation.LastTurnStatus(notification));
        Assert.Equal(new Rectangle(12, definition.Id * 64 + 1, 94, 62),
            LastTurnEventPresentation.SiteBackgroundSource(definition.Id));
    }

    [Fact]
    public void ReviewedReportsAreArchivedPerPlayerAsASnapshot()
    {
        var archive = new LastTurnEventArchive();
        var player = new PlayerId(0);
        var reports = new List<GameNotification>
        {
            new(6, 3, TurnPhase.Execution, ExecutionPhase.Instant,
                GameNotificationKind.Influence, new GangId(0))
        };

        archive.Store(player, 4, reports);
        reports.Clear();

        Assert.Single(archive.For(player, 4));
        Assert.Empty(archive.For(new PlayerId(1), 4));
        Assert.Empty(archive.For(player, 5));
        archive.Store(player, 5, reports);
        archive.Remove(player);
        Assert.Empty(archive.For(player, 5));
        archive.Clear();
        Assert.Empty(archive.For(player, 5));
    }

    [Fact]
    public void NativeProjectionKeepsOnlyFirstThirtyTwoReportsFromCompletedTurn()
    {
        var reports = Enumerable.Range(0, MatchLimits.LastTurnReportsPerPlayer + 3)
            .Select(index => new GameNotification(index, 4, TurnPhase.Execution,
                ExecutionPhase.Chaos, GameNotificationKind.Crackdown, SectorId: index % 64))
            .Prepend(new GameNotification(100, 3, TurnPhase.Execution,
                ExecutionPhase.Chaos, GameNotificationKind.Crackdown, SectorId: 63))
            .ToArray();

        var projected = LastTurnEventProjection.Select(reports, [], completedTurn: 4);

        Assert.Equal(MatchLimits.LastTurnReportsPerPlayer, projected.Count);
        Assert.Equal(0, projected[0].Sequence);
        Assert.Equal(MatchLimits.LastTurnReportsPerPlayer - 1, projected[^1].Sequence);
    }

    [Theory]
    [InlineData(GangAction.Bribe, CommandResolutionCode.TargetEvaded)]
    [InlineData(GangAction.Equip, CommandResolutionCode.ItemUnavailable)]
    [InlineData(GangAction.Move, CommandResolutionCode.InsufficientCash)]
    public void LastTurnReportsExcludeFailuresOutsideTheNativeCashCases(
        GangAction action,
        CommandResolutionCode code)
    {
        var gameEvent = new GameEvent(42, 2, TurnPhase.Execution, ExecutionPhase.Instant,
            GameEventKind.CommandFailed, new PlayerId(0), new GangId(10), action,
            CommandTarget.None, Resolution: new CommandResolutionDetails(code, [], 0));
        var notification = new GameNotification(0, 2, TurnPhase.Execution,
            ExecutionPhase.Instant, GameNotificationKind.CommandResult,
            new GangId(10), 7, 42);

        Assert.False(NotificationPresentation.IsLastTurnReport(notification, gameEvent));
    }

    [Fact]
    public void ObjectiveUpdatesAreNotNativeLastTurnReports()
    {
        var notification = new GameNotification(0, 2, TurnPhase.PlayerElimination, null,
            GameNotificationKind.Objective);

        Assert.False(NotificationPresentation.IsLastTurnReport(notification, null));
    }

    [Fact]
    public void NativeEventSiteDitherMatchesOriginalBitmapResource146()
    {
        for (var y = 0; y < LastTurnEventPresentation.NativeDitherPatternSize; y++)
        for (var x = 0; x < LastTurnEventPresentation.NativeDitherPatternSize; x++)
        {
            var expected = (x, y) switch
            {
                (0 or 4, 0 or 2 or 4 or 6) => true,
                (2 or 6, 1 or 3 or 5 or 7) => true,
                _ => false
            };
            Assert.Equal(expected, LastTurnEventPresentation.NativeDitherKeepsPixel(x, y));
        }
    }
}
