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
}
