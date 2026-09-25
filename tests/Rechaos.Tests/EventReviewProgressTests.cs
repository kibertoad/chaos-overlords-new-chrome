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
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(14 * 166)));
        Assert.Equal(new Rectangle(0, 0, 48, 48),
            ItemRotationPresentation.Frame(TimeSpan.FromMilliseconds(15 * 166)));
        Assert.Equal(new Rectangle(5 * 48, 0, 48, 48), ItemRotationPresentation.Frame(5));
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
        // FMT-STATE-006, SCR-EVENT-001: site 4 is slot 1 of sector 1, labelled B1.
        var record = LastTurnEventPresentation.Record(state, notification, related);
        Assert.Equal(new LastTurnReportRecord(4, 1, 1, 0), record);
        Assert.Equal($"B1:{definition.Name}", LastTurnEventPresentation.Subject(state, record));
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

    // RULE-EVENT-006, RULE-EVENT-005: each completed site records its own report, so two sites of
    // one sector completed in one resolution give two pages. Every Control participant of the
    // winner has a result, and the sector gives one report.
    [Fact]
    public void TwoSitesCompletedInOneSectorGiveTwoReportsAndControlGivesOne()
    {
        const int turn = 4;
        const int sector = 5;
        GameEvent Resolved(long sequence, int gang, GangAction action, CommandTarget target,
            int previous, int result) =>
            new(sequence, turn, TurnPhase.Execution, ExecutionPhase.Instant,
                GameEventKind.CommandResolved, new PlayerId(0), new GangId(gang), action, target,
                Resolution: new CommandResolutionDetails(
                    CommandResolutionCode.Resolved, [], 1, previous, result));
        GameEvent[] events =
        [
            Resolved(0, 10, GangAction.Influence, CommandTarget.Site(sector * 3), 2, 0),
            Resolved(1, 11, GangAction.Influence, CommandTarget.Site(sector * 3 + 2), 3, 0),
            Resolved(2, 12, GangAction.Influence, CommandTarget.Site(sector * 3 + 2), 0, 0),
            Resolved(3, 10, GangAction.Control, CommandTarget.None, 1, 0),
            Resolved(4, 11, GangAction.Control, CommandTarget.None, 1, 0)
        ];
        var reports = events.Select(gameEvent => new GameNotification(
                gameEvent.Sequence, turn, TurnPhase.Execution, gameEvent.ExecutionPhase,
                gameEvent.Action == GangAction.Control
                    ? GameNotificationKind.Control
                    : GameNotificationKind.Influence,
                gameEvent.Gang, sector, gameEvent.Sequence))
            .ToArray();

        var projected = LastTurnEventProjection.Select(reports, events, completedTurn: turn);

        Assert.Equal(new long[] { 0, 1, 3 }, projected.Select(report => report.Sequence));
        Assert.Equal(new int?[] { sector * 3, sector * 3 + 2 },
            projected.Take(2).Select(report => LastTurnEventPresentation.InfluenceSiteId(
                report, events[report.RelatedEventSequence!.Value])));
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

    [Theory]
    [InlineData(GangAction.Bribe, GameNotificationKind.CommandResult)]
    [InlineData(GangAction.Equip, GameNotificationKind.Equipment)]
    public void CashFailedCommandsUseTheNativeEmptySafeArtwork(
        GangAction action,
        GameNotificationKind notificationKind)
    {
        var gameEvent = new GameEvent(42, 2, TurnPhase.Execution, ExecutionPhase.Instant,
            GameEventKind.CommandFailed, new PlayerId(0), new GangId(10), action,
            CommandTarget.None, Resolution: new CommandResolutionDetails(
                CommandResolutionCode.InsufficientCash, [], 0));
        var notification = new GameNotification(0, 2, TurnPhase.Execution,
            ExecutionPhase.Instant, notificationKind, new GangId(10), 7, 42);

        Assert.Equal(6, LastTurnEventPresentation.ArtworkIndex(notification, gameEvent));
    }

    [Fact]
    public void CashFailedHireUsesTheNativeEmptySafeArtwork()
    {
        var notification = new GameNotification(0, 2, TurnPhase.Hire, null,
            GameNotificationKind.HireInsufficientCash, SectorId: 7);

        Assert.Equal(6, LastTurnEventPresentation.ArtworkIndex(notification, null));
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
