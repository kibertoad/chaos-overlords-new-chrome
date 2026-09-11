using Microsoft.Xna.Framework;
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
}
