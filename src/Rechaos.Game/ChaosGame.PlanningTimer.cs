using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum PlanningTimeLimit
{
    None,
    ThirtySeconds,
    TwoMinutes,
    FiveMinutes
}

public static class PlanningTimerPolicy
{
    public const int BarWidth = 60;

    public static IReadOnlyList<PlanningTimeLimit> Choices { get; } =
        Enum.GetValues<PlanningTimeLimit>();

    public static string Label(PlanningTimeLimit limit) => limit switch
    {
        PlanningTimeLimit.None => "NONE",
        PlanningTimeLimit.ThirtySeconds => "30 SECONDS",
        PlanningTimeLimit.TwoMinutes => "2 MINUTES",
        PlanningTimeLimit.FiveMinutes => "5 MINUTES",
        _ => throw new ArgumentOutOfRangeException(nameof(limit))
    };

    public static TimeSpan? Duration(PlanningTimeLimit limit) => limit switch
    {
        PlanningTimeLimit.None => null,
        PlanningTimeLimit.ThirtySeconds => TimeSpan.FromSeconds(30),
        PlanningTimeLimit.TwoMinutes => TimeSpan.FromMinutes(2),
        PlanningTimeLimit.FiveMinutes => TimeSpan.FromMinutes(5),
        _ => throw new ArgumentOutOfRangeException(nameof(limit))
    };

    public static int VisibleBarWidth(TimeSpan duration, TimeSpan remaining)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        var ratio = Math.Clamp(remaining.TotalMilliseconds / duration.TotalMilliseconds, 0, 1);
        return (int)Math.Floor(BarWidth * ratio);
    }

    public static int? WarningSoundSlot(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) return null;
        if (remaining <= TimeSpan.FromSeconds(1)) return 8;
        return remaining < TimeSpan.FromSeconds(10) ? 7 : null;
    }

    public static int WarningBucket(TimeSpan remaining) =>
        Math.Max(0, (int)Math.Ceiling(remaining.TotalSeconds));
}

public static class PlanningTimerLayout
{
    public static Rectangle Bar => new(520, 336, PlanningTimerPolicy.BarWidth, 3);
    public static IReadOnlyList<Rectangle> SetupChoices { get; } =
    [
        new(192, 330, 108, 27), new(192, 359, 108, 27),
        new(192, 388, 108, 27), new(192, 417, 108, 27)
    ];
}

public enum PlanningTimerSignal
{
    None,
    LongWarning,
    FinalWarning,
    Expired
}

public sealed class PlanningTimer
{
    private TimeSpan _duration;
    private TimeSpan _deadline;
    private int _lastPlanningWarningBucket = int.MaxValue;

    public bool IsActive { get; private set; }

    public void Start(PlanningTimeLimit limit, TimeSpan now)
    {
        Stop();
        if (PlanningTimerPolicy.Duration(limit) is not { } duration) return;
        _duration = duration;
        _deadline = now + duration;
        IsActive = true;
    }

    public void Stop()
    {
        IsActive = false;
        _duration = TimeSpan.Zero;
        _deadline = TimeSpan.Zero;
        _lastPlanningWarningBucket = int.MaxValue;
    }

    public PlanningTimerSignal Advance(TimeSpan now)
    {
        if (!IsActive) return PlanningTimerSignal.None;
        var remaining = _deadline - now;
        if (remaining <= TimeSpan.Zero)
        {
            Stop();
            return PlanningTimerSignal.Expired;
        }

        if (PlanningTimerPolicy.WarningSoundSlot(remaining) is not { } slot)
            return PlanningTimerSignal.None;
        var bucket = PlanningTimerPolicy.WarningBucket(remaining);
        if (bucket == _lastPlanningWarningBucket) return PlanningTimerSignal.None;
        _lastPlanningWarningBucket = bucket;
        return slot == 7 ? PlanningTimerSignal.LongWarning : PlanningTimerSignal.FinalWarning;
    }

    public int VisibleBarWidth(TimeSpan now) => IsActive
        ? PlanningTimerPolicy.VisibleBarWidth(_duration, _deadline - now)
        : PlanningTimerPolicy.BarWidth;
}

public sealed partial class ChaosGame
{
    private readonly PlanningTimer _planningTimer = new();
    private PlanningTimeLimit _selectedPlanningTimeLimit = PlanningTimeLimit.None;

    private void SelectPlanningTimeLimit(PlanningTimeLimit limit)
    {
        if (!Enum.IsDefined(limit)) throw new ArgumentOutOfRangeException(nameof(limit));
        if (_selectedPlanningTimeLimit != limit) PlayGeneralSound(3);
        _selectedPlanningTimeLimit = limit;
        SavePreferences();
        _message = $"TURN TIME LIMIT {PlanningTimerPolicy.Label(limit)}";
    }

    private void CyclePlanningTimeLimit()
    {
        var choices = PlanningTimerPolicy.Choices;
        var index = (int)_selectedPlanningTimeLimit;
        SelectPlanningTimeLimit(choices[(index + 1) % choices.Count]);
    }

    private void StartPlanningTimer(TimeSpan now)
    {
        _planningTimer.Stop();
        if (_debugPhaseStepping || _state?.Coordinator.ActivePlayer is not { } playerId
            || _state.Coordinator.Phase != TurnPhase.Command
            || _state.FindPlayer(playerId)?.Setup.Controller != PlayerController.Human)
            return;

        _planningTimer.Start(_selectedPlanningTimeLimit, now);
    }

    private void StopPlanningTimer() => _planningTimer.Stop();

    private bool UpdatePlanningTimer(TimeSpan now)
    {
        if (!_planningTimer.IsActive) return false;
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || _state.Coordinator.Phase != TurnPhase.Command
            || _state.FindPlayer(playerId)?.Setup.Controller != PlayerController.Human
            || _screens.Current is ClientScreen.Title or ClientScreen.Setup
                or ClientScreen.Handoff or ClientScreen.Endgame)
        {
            StopPlanningTimer();
            return false;
        }

        switch (_planningTimer.Advance(now))
        {
            case PlanningTimerSignal.LongWarning:
                PlayGeneralSound(7);
                return false;
            case PlanningTimerSignal.FinalWarning:
                PlayGeneralSound(8);
                return false;
            case PlanningTimerSignal.None:
                return false;
            case PlanningTimerSignal.Expired:
                _idleGangWarningOpen = false;
                _message = "TURN TIME LIMIT EXPIRED";
                FinishPlanningTurn();
                return true;
            default:
                throw new InvalidOperationException("Unknown planning timer signal.");
        }
    }

    private void DrawPlanningTimer(SpriteBatch batch, Texture2D pixel)
    {
        if (!_planningTimer.IsActive
            || _screens.Current is ClientScreen.Options or ClientScreen.Help
            || _idleGangWarningOpen)
            return;

        var width = _planningTimer.VisibleBarWidth(_inputTime);
        batch.Draw(pixel, PlanningTimerLayout.Bar, Color.Black);
        if (width > 0)
            batch.Draw(pixel, new Rectangle(
                PlanningTimerLayout.Bar.X, PlanningTimerLayout.Bar.Y,
                width, PlanningTimerLayout.Bar.Height), Color.Lime);
    }
}
