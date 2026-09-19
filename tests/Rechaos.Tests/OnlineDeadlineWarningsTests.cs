using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The warnings an online turn's countdown sounds, which the server's deadline drives.
/// </summary>
/// <remarks>
/// A hot-seat match warns a player twice as their planning time runs out. An online match used to
/// warn them not at all: the local clock is deliberately never armed there, so the countdown is the
/// server's deadline and nothing was listening to it.
/// </remarks>
public sealed class OnlineDeadlineWarningsTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void AnUntimedTurnIsNeverWarnedAbout()
    {
        var warnings = new OnlineDeadlineWarnings();

        Assert.Equal(PlanningTimerSignal.None, warnings.Advance(1, null, Now));
    }

    [Fact]
    public void EachWarningSoundsOnceAsTheDeadlineApproaches()
    {
        var warnings = new OnlineDeadlineWarnings();
        var deadline = Now + TimeSpan.FromSeconds(30);

        Assert.Equal(PlanningTimerSignal.None, warnings.Advance(1, deadline, Now));
        Assert.Equal(
            PlanningTimerSignal.LongWarning,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(9)));
        Assert.Equal(
            PlanningTimerSignal.None,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(8)));
        Assert.Equal(
            PlanningTimerSignal.FinalWarning,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(1)));
        Assert.Equal(PlanningTimerSignal.None, warnings.Advance(1, deadline, deadline));
    }

    /// <summary>
    /// A clock the server restarts is a fresh countdown, and is warned about again.
    /// </summary>
    /// <remarks>
    /// The case that makes this more than bookkeeping: an absence vote pauses the turn and the
    /// deadline is re-armed in full when it closes. A player who heard the last ten seconds of the
    /// old clock has a whole new turn's worth of time, and needs telling when that runs out too.
    /// </remarks>
    [Fact]
    public void ARestartedClockWarnsAgain()
    {
        var warnings = new OnlineDeadlineWarnings();
        var deadline = Now + TimeSpan.FromSeconds(30);
        Assert.Equal(
            PlanningTimerSignal.FinalWarning,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(1)));

        var restarted = deadline + TimeSpan.FromMinutes(5);

        Assert.Equal(
            PlanningTimerSignal.LongWarning,
            warnings.Advance(1, restarted, restarted - TimeSpan.FromSeconds(9)));
    }

    [Fact]
    public void ANewTurnWarnsAgain()
    {
        var warnings = new OnlineDeadlineWarnings();
        var deadline = Now + TimeSpan.FromSeconds(30);
        warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(1));

        Assert.Equal(
            PlanningTimerSignal.LongWarning,
            warnings.Advance(2, deadline, deadline - TimeSpan.FromSeconds(9)));
    }

    /// <summary>
    /// A player who arrives inside the last second hears the last second, and not the earlier one.
    /// </summary>
    /// <remarks>
    /// Warnings are owed for where the clock is, not for every step it went through: a frame that
    /// lands past both boundaries — a stall, a window regaining focus — should not play two sounds
    /// on top of each other, nor back-fill the warning whose moment has gone.
    /// </remarks>
    [Fact]
    public void ALateFirstLookWarnsOnlyForTheTimeThatIsLeft()
    {
        var warnings = new OnlineDeadlineWarnings();
        var deadline = Now + TimeSpan.FromSeconds(30);

        Assert.Equal(
            PlanningTimerSignal.FinalWarning,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(0.5)));
        Assert.Equal(PlanningTimerSignal.None, warnings.Advance(1, deadline, deadline));
    }

    /// <summary>A turn already sealed is not hurried; the next deadline starts clean.</summary>
    [Fact]
    public void StoppingForgetsTheDeadlineBeingWatched()
    {
        var warnings = new OnlineDeadlineWarnings();
        var deadline = Now + TimeSpan.FromSeconds(30);
        warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(1));

        warnings.Stop();

        Assert.Equal(
            PlanningTimerSignal.LongWarning,
            warnings.Advance(1, deadline, deadline - TimeSpan.FromSeconds(9)));
    }
}
