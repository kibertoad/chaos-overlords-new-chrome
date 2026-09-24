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
    private static readonly DateTimeOffset Deadline = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Repeat = TimeSpan.FromSeconds(60);

    [Fact]
    public void AnUntimedTurnIsNeverOverdue()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);

        Assert.False(watchdog.Advance(null, Deadline.AddHours(1), TimeSpan.Zero));
    }

    [Fact]
    public void AnOrdinarySealIsGivenTimeToLandBeforeAnyoneAsks()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);

        Assert.False(watchdog.Advance(Deadline, Deadline.AddSeconds(-1), TimeSpan.Zero));
        Assert.False(watchdog.Advance(
            Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk - TimeSpan.FromMilliseconds(1),
            TimeSpan.FromSeconds(10)));
        Assert.True(watchdog.Advance(
            Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public void AStillMissingSealIsAskedAboutAgainOncePerRepeat()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);
        var late = Deadline + OnlineOverdueSealWatchdog.FirstAsk;

        Assert.True(watchdog.Advance(Deadline, late, TimeSpan.FromSeconds(10)));
        Assert.False(watchdog.Advance(Deadline, late.AddSeconds(59), TimeSpan.FromSeconds(69)));
        Assert.True(watchdog.Advance(Deadline, late.AddSeconds(60), TimeSpan.FromSeconds(70)));
    }

    [Fact]
    public void ANewDeadlineStartsOver()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);
        Assert.True(watchdog.Advance(
            Deadline, Deadline + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(10)));
        var next = Deadline.AddMinutes(2);

        Assert.False(watchdog.Advance(next, next.AddSeconds(-30), TimeSpan.FromSeconds(11)));
        Assert.True(watchdog.Advance(
            next, next + OnlineOverdueSealWatchdog.FirstAsk, TimeSpan.FromSeconds(12)));
    }

    [Fact]
    public void StoppingForgetsTheRepeat()
    {
        var watchdog = new OnlineOverdueSealWatchdog(Repeat);
        var late = Deadline + OnlineOverdueSealWatchdog.FirstAsk;
        Assert.True(watchdog.Advance(Deadline, late, TimeSpan.FromSeconds(10)));

        watchdog.Stop();

        Assert.True(watchdog.Advance(Deadline, late.AddSeconds(1), TimeSpan.FromSeconds(11)));
    }
}
