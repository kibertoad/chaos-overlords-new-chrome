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
    public const int RefreshCountdown = 6;

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
        if (remaining >= duration) return BarWidth;
        if (remaining <= TimeSpan.Zero) return 0;

        var durationMilliseconds = duration.Ticks / TimeSpan.TicksPerMillisecond;
        var elapsedMilliseconds = (duration - remaining).Ticks / TimeSpan.TicksPerMillisecond;
        var elapsedPercent = elapsedMilliseconds * 100 / durationMilliseconds;
        return checked(BarWidth - (int)(elapsedPercent * BarWidth / 100));
    }

    public static int? WarningSoundSlot(TimeSpan remaining)
    {
        if (remaining < TimeSpan.Zero) return null;
        if (remaining <= TimeSpan.FromSeconds(1)) return 8;
        return remaining < TimeSpan.FromSeconds(10) ? 7 : null;
    }

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
    private int _refreshCountdown;
    private TimeSpan? _pausedRemaining;

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
        _refreshCountdown = 0;
        _pausedRemaining = null;
    }

    public void Pause(TimeSpan now)
    {
        if (!IsActive || _pausedRemaining is not null) return;
        var remaining = _deadline - now;
        _pausedRemaining = remaining > TimeSpan.Zero ? remaining : TimeSpan.Zero;
    }

    public void Resume(TimeSpan now)
    {
        if (!IsActive || _pausedRemaining is not { } remaining) return;
        _deadline = now + remaining;
        _pausedRemaining = null;
    }

    public PlanningTimerSignal Advance(TimeSpan now)
    {
        if (!IsActive) return PlanningTimerSignal.None;
        if (_pausedRemaining is not null) return PlanningTimerSignal.None;
        var remaining = _deadline - now;
        if (remaining <= TimeSpan.Zero)
        {
            Stop();
            return PlanningTimerSignal.Expired;
        }

        if (_refreshCountdown > 0) _refreshCountdown--;
        if (_refreshCountdown != 0) return PlanningTimerSignal.None;
        _refreshCountdown = PlanningTimerPolicy.RefreshCountdown;

        if (PlanningTimerPolicy.WarningSoundSlot(remaining) is not { } slot)
            return PlanningTimerSignal.None;
        return slot == 7 ? PlanningTimerSignal.LongWarning : PlanningTimerSignal.FinalWarning;
    }

    public int VisibleBarWidth(TimeSpan now) => IsActive
        ? PlanningTimerPolicy.VisibleBarWidth(_duration, _pausedRemaining ?? _deadline - now)
        : PlanningTimerPolicy.BarWidth;
}

/// <summary>
/// The warnings for an online turn's countdown, which the server owns.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="PlanningTimer"/> is deliberately never armed online — see
/// <see cref="ChaosGame.StartPlanningTimer"/> — so the only clock there is the server's turn
/// deadline. That left the online countdown silent: a player planning a turn heard nothing as their
/// time ran out, while the same match played hot-seat warned them twice. This watches the deadline
/// rather than running one, so nothing it does can seal a turn or disagree with the server about
/// when the turn ends.
/// </para>
/// <para>
/// Each warning sounds once per deadline. A turn whose clock is restarted — an absence vote
/// closing, a desync pause lifting — is a new deadline and is warned about again, because the
/// player really does have a fresh countdown to hear out.
/// </para>
/// </remarks>
public sealed class OnlineDeadlineWarnings
{
    private int _turn = -1;
    private DateTimeOffset? _deadline;

    /// <summary>The highest warning slot already sounded for <see cref="_deadline"/>.</summary>
    private int _sounded;

    /// <summary>
    /// The warning owed for this frame, if any.
    /// </summary>
    /// <remarks>
    /// Never <see cref="PlanningTimerSignal.Expired"/>: a client does not end an online turn, and
    /// answering the deadline locally is exactly what the online path must not do.
    /// </remarks>
    public PlanningTimerSignal Advance(int turn, DateTimeOffset? deadline, DateTimeOffset now)
    {
        if (turn != _turn || deadline != _deadline)
        {
            _turn = turn;
            _deadline = deadline;
            _sounded = 0;
        }
        if (deadline is not { } dueAt) return PlanningTimerSignal.None;
        if (PlanningTimerPolicy.WarningSoundSlot(dueAt - now) is not { } slot
            || slot <= _sounded)
        {
            return PlanningTimerSignal.None;
        }
        _sounded = slot;
        return slot == 7 ? PlanningTimerSignal.LongWarning : PlanningTimerSignal.FinalWarning;
    }

    /// <summary>Forgets the deadline being watched, so the next one warns from the start.</summary>
    public void Stop()
    {
        _turn = -1;
        _deadline = null;
        _sounded = 0;
    }
}

public sealed partial class ChaosGame
{
    private readonly PlanningTimer _planningTimer = new();
    private readonly OnlineDeadlineWarnings _onlineDeadlineWarnings = new();
    private PlanningTimeLimit _selectedPlanningTimeLimit = PlanningTimeLimit.None;

    private void SelectPlanningTimeLimit(PlanningTimeLimit limit)
    {
        if (!Enum.IsDefined(limit)) throw new ArgumentOutOfRangeException(nameof(limit));
        if (_selectedPlanningTimeLimit != limit)
            PlayGeneralSound(GeneralSoundSlot.AcceptedSelection);
        _selectedPlanningTimeLimit = limit;
        SavePreferences();
        _message = string.Empty;
    }

    private void CyclePlanningTimeLimit()
    {
        var choices = PlanningTimerPolicy.Choices;
        var index = (int)_selectedPlanningTimeLimit;
        SelectPlanningTimeLimit(choices[(index + 1) % choices.Count]);
    }

    /// <summary>
    /// Arms the planning clock for the local player, in a hot-seat match.
    /// </summary>
    /// <remarks>
    /// Online matches have their own clock — the server's turn deadline, shown in the city footer —
    /// and a second one running against it would submit a player's turn early for reasons nothing
    /// on screen explains.
    /// </remarks>
    private void StartPlanningTimer(TimeSpan now)
    {
        _planningTimer.Stop();
        if (_session is not null) return;
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
                or ClientScreen.Online or ClientScreen.Lobby
                or ClientScreen.Handoff or ClientScreen.Endgame)
        {
            StopPlanningTimer();
            return false;
        }

        switch (_planningTimer.Advance(now))
        {
            case PlanningTimerSignal.LongWarning:
                PlayGeneralSound(GeneralSoundSlot.CountdownWarning);
                return false;
            case PlanningTimerSignal.FinalWarning:
                PlayGeneralSound(GeneralSoundSlot.FinalSecondWarning);
                return false;
            case PlanningTimerSignal.None:
                return false;
            case PlanningTimerSignal.Expired:
                _idleGangWarningOpen = false;
                _message = string.Empty;
                // Online this never fires, because the clock is not armed there. It still goes
                // through the online path rather than straight to the local resolution, so that
                // arming it later cannot silently resolve a turn on one client alone.
                if (_session is not null) SubmitOnlineTurn();
                else FinishPlanningTurn();
                return true;
            default:
                throw new InvalidOperationException("Unknown planning timer signal.");
        }
    }

    /// <summary>
    /// Sounds the online countdown's warnings, which the server's deadline drives.
    /// </summary>
    /// <remarks>
    /// Only while the turn is still the player's to plan. Once it is submitted the countdown is
    /// about how long the other players have, and hurrying somebody who has already finished is
    /// noise.
    /// </remarks>
    private void UpdateOnlineDeadlineWarnings()
    {
        if (_session is null || !_online.PlanningIsOpen)
        {
            _onlineDeadlineWarnings.Stop();
            return;
        }
        switch (_onlineDeadlineWarnings.Advance(
            _online.PlanningTurn, _online.DeadlineAt, DateTimeOffset.UtcNow))
        {
            case PlanningTimerSignal.LongWarning:
                PlayGeneralSound(GeneralSoundSlot.CountdownWarning);
                return;
            case PlanningTimerSignal.FinalWarning:
                PlayGeneralSound(GeneralSoundSlot.FinalSecondWarning);
                return;
            default:
                return;
        }
    }

    /// <summary>
    /// The countdown bar for an online turn, or null when there is no clock to draw one from.
    /// </summary>
    /// <remarks>
    /// Recomputed from the deadline every frame rather than counted down, like the line on the city
    /// footer, so a clock the server restarts — an absence vote closing, a desync pause lifting —
    /// corrects itself on the next frame. A paused turn has no deadline and so draws no bar, which
    /// is the honest picture: there is nothing running to show.
    /// </remarks>
    private int? OnlineBarWidth()
    {
        if (!_online.PlanningIsOpen || _online.DeadlineAt is not { } deadline) return null;
        var seconds = _online.Match?.Settings.TurnTimerSeconds ?? 0;
        if (seconds <= 0) return null;
        return PlanningTimerPolicy.VisibleBarWidth(
            TimeSpan.FromSeconds(seconds), deadline - DateTimeOffset.UtcNow);
    }

    private void DrawPlanningTimer(SpriteBatch batch, Texture2D pixel)
    {
        if (_screens.Current is ClientScreen.Options or ClientScreen.Help || _idleGangWarningOpen)
            return;
        // Online the bar comes from the server's deadline; the local timer is never armed there.
        var width = _session is not null
            ? OnlineBarWidth()
            : _planningTimer.IsActive ? _planningTimer.VisibleBarWidth(_inputTime) : null;
        if (width is not { } visible) return;

        batch.Draw(pixel, PlanningTimerLayout.Bar, Color.Black);
        if (visible > 0)
            batch.Draw(pixel, new Rectangle(
                PlanningTimerLayout.Bar.X, PlanningTimerLayout.Bar.Y,
                visible, PlanningTimerLayout.Bar.Height), Color.Lime);
    }
}
