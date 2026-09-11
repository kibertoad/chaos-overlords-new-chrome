using Rechaos.Game;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResultProjectionTests
{
    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(0, 2, false)]
    [InlineData(2, 2, false)]
    public void OnlyTheImmediatelyCompletedTurnIsPresented(
        int eventTurn,
        int currentTurn,
        bool expected)
    {
        Assert.Equal(expected,
            CombatResultProjection.IsFromLastCompletedTurn(eventTurn, currentTurn));
    }

    [Fact]
    public void InvalidTurnValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(0, 0));
    }

    [Fact]
    public void AnimationProgressIsIndependentForEachHotSeatPlayer()
    {
        var progress = new CombatPresentationProgress();
        var first = new PlayerId(0);
        var second = new PlayerId(1);

        progress.MarkSeen(first, 17);

        Assert.Equal(17, progress.LastSeen(first));
        Assert.Equal(-1, progress.LastSeen(second));
        progress.MarkSeen(second, 9);
        Assert.Equal(9, progress.LastSeen(second));
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.MarkSeen(first, 16));
    }

    [Fact]
    public void LoadResetSuppressesHistoricalEventsForEveryPlayer()
    {
        var progress = new CombatPresentationProgress();
        PlayerId[] players = [new(0), new(1), new(2)];

        progress.ResetTo(players, 42);

        Assert.All(players, player => Assert.Equal(42, progress.LastSeen(player)));
        progress.Clear();
        Assert.All(players, player => Assert.Equal(-1, progress.LastSeen(player)));
    }
}
