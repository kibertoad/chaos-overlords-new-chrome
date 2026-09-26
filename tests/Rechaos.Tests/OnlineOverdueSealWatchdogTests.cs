using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The resynchronisation an online client asks for when its turn's clock ran out and no seal came.
/// </summary>
/// <remarks>
/// The countdown reached zero, the footer said "SEALING", and the turn sat there until the player
/// ended it by hand: nothing on the client was watching for a seal that never arrived unless every
/// seat had already finished.
/// </remarks>
public sealed class OnlineOverdueSealWatchdogTests
{
    private const int Turn = 3;
    private static readonly DateTimeOffset Deadline = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Repeat = TimeSpan.FromSeconds(60);

    /// <summary>A watchdog that has been watching <see cref="Deadline"/> since before it passed.</summary>
    private static OnlineOverdueSealWatchdog Watching()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);
        Assert.False(watchdog.Advance(Turn, Deadline, Deadline.AddSeconds(-30), TimeSpan.Zero));
        return watchdog;
    }

    [Fact]
    public void AnUntimedTurnIsNeverOverdue()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);

        Assert.False(watchdog.Advance(Turn, null, Deadline.AddHours(1), TimeSpan.Zero));
        Assert.False(watchdog.Advance(Turn, null, Deadline.AddHours(2), TimeSpan.FromHours(1)));
    }

    [Fact]
    public void AnOrdinarySealIsGivenTimeToLandBeforeAnyoneAsks()
    {
        var watchdog = Watching();

        Assert.False(watchdog.Advance(
            Turn, Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk - TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(40)));
        Assert.True(watchdog.Advance(
            Turn, Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(40)));
    }

    [Fact]
    public void AStillMissingSealIsAskedAboutAgainOncePerRepeat()
    {
        var watchdog = Watching();
        var late = Deadline + OnlineOverdueSealWatchdog.FirstAsk;

        Assert.True(watchdog.Advance(Turn, Deadline, late, TimeSpan.FromSeconds(40)));
        Assert.False(watchdog.Advance(Turn, Deadline, late.AddSeconds(59), TimeSpan.FromSeconds(99)));
        Assert.True(watchdog.Advance(Turn, Deadline, late.AddSeconds(60), TimeSpan.FromSeconds(100)));
    }

    [Fact]
    public void ANewDeadlineStartsOver()
    {
        var watchdog = Watching();
        Assert.True(watchdog.Advance(
            Turn, Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(40)));
        var next = Deadline.AddMinutes(2);

        Assert.False(watchdog.Advance(Turn + 1, next, next.AddSeconds(-30), TimeSpan.FromSeconds(41)));
        Assert.True(watchdog.Advance(
            Turn + 1, next, next + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(81)));
    }

    [Fact]
    public void StoppingForgetsTheRepeat()
    {
        var watchdog = Watching();
        var late = Deadline + OnlineOverdueSealWatchdog.FirstAsk;
        Assert.True(watchdog.Advance(Turn, Deadline, late, TimeSpan.FromSeconds(40)));

        watchdog.Stop();

        Assert.False(watchdog.Advance(Turn, Deadline, late.AddSeconds(1), TimeSpan.FromSeconds(41)));
        Assert.True(watchdog.Advance(
            Turn, Deadline, late.AddSeconds(11), TimeSpan.FromSeconds(41) + OnlineOverdueSealWatchdog.FirstAsk));
    }

    /// <summary>
    /// A connection that comes back long after the deadline is replaying the seal it missed; asking
    /// for a resync on the first frame cancelled that stream and rebuilt the match instead.
    /// </summary>
    [Fact]
    public void AWatchThatBeginsPastTheDeadlineGivesTheStreamTimeToCatchUp()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);
        var longPast = Deadline.AddMinutes(10);

        Assert.False(watchdog.Advance(Turn, Deadline, longPast, TimeSpan.FromMinutes(5)));
        Assert.False(watchdog.Advance(
            Turn, Deadline, longPast.AddSeconds(9), TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(9)));
        Assert.True(watchdog.Advance(
            Turn, Deadline, longPast.AddSeconds(10), TimeSpan.FromMinutes(5) + OnlineOverdueSealWatchdog.FirstAsk));
    }

    /// <summary>
    /// A resolved turn opens planning on the next one before its deadline has been delivered, so the
    /// deadline still on screen is the sealed turn's, long past.
    /// </summary>
    [Fact]
    public void TheSealedTurnsDeadlineIsNotTakenForTheNextTurnsSeal()
    {
        var watchdog = Watching();
        Assert.True(watchdog.Advance(
            Turn, Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(40)));
        var repeatDue = TimeSpan.FromSeconds(40) + Repeat;

        Assert.False(watchdog.Advance(Turn + 1, Deadline, Deadline.AddSeconds(80), repeatDue));
    }
}
