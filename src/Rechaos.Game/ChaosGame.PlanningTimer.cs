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

/// <summary>
/// The planning clock's arithmetic, in whole milliseconds as the original's <c>timeGetTime</c>
/// gives them (RULE-TIMER-001, RULE-TIMER-002, RULE-TIMER-003).
/// </summary>
public static class PlanningTimerPolicy
{
    public const int BarWidth = 60;

    /// <summary>The presentation ticks between two redraws of the bar (RULE-TIMER-003).</summary>
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

    /// <summary>RULE-TIMER-001: <c>planning_limit_ms</c> for a choice, or null for no limit.</summary>
    public static int? LimitMilliseconds(PlanningTimeLimit limit) => limit switch
    {
        PlanningTimeLimit.None => null,
        PlanningTimeLimit.ThirtySeconds => 30000,
        PlanningTimeLimit.TwoMinutes => 120000,
        PlanningTimeLimit.FiveMinutes => 300000,
        _ => throw new ArgumentOutOfRangeException(nameof(limit))
    };

    public static TimeSpan? Duration(PlanningTimeLimit limit) =>
        LimitMilliseconds(limit) is { } milliseconds ? TimeSpan.FromMilliseconds(milliseconds) : null;

    /// <summary>
    /// RULE-TIMER-002: the turn ends once the elapsed milliseconds exceed the limit, so it is still
    /// running when they equal it.
    /// </summary>
    public static bool Expired(int limitMilliseconds, int elapsedMilliseconds) =>
        elapsedMilliseconds > limitMilliseconds;

    /// <summary>
    /// RULE-TIMER-003: the width the bar is drawn with, from the whole percent elapsed. The 32-bit
    /// product wraps as the original's does. Below 1 the empty bar is drawn and above 59 the full
    /// one, which <see cref="VisibleBarWidth(int, int)"/> gives as 0 and 60.
    /// </summary>
    public static int RawBarWidth(int limitMilliseconds, int elapsedMilliseconds)
    {
        if (limitMilliseconds <= 0) throw new ArgumentOutOfRangeException(nameof(limitMilliseconds));
        var elapsedPercent = unchecked(elapsedMilliseconds * 100) / limitMilliseconds;
        return BarWidth - elapsedPercent * BarWidth / 100;
    }

    public static int VisibleBarWidth(int limitMilliseconds, int elapsedMilliseconds) =>
        Math.Clamp(RawBarWidth(limitMilliseconds, elapsedMilliseconds), 0, BarWidth);

    public static int VisibleBarWidth(TimeSpan duration, TimeSpan remaining)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (remaining >= duration) return BarWidth;
        if (remaining <= TimeSpan.Zero) return 0;
        var limit = checked((int)(duration.Ticks / TimeSpan.TicksPerMillisecond));
        return VisibleBarWidth(limit, WholeMilliseconds(duration - remaining));
    }

    /// <summary>
    /// RULE-TIMER-003: the effect a redraw plays for the remaining milliseconds: slot 7 above 1000
    /// and below 10000, slot 8 above 0 and up to 1000.
    /// </summary>
    public static int? WarningSoundSlot(int remainingMilliseconds) => remainingMilliseconds switch
    {
        > 1000 and < 10000 => 7,
        > 0 and <= 1000 => 8,
        _ => null,
    };

    public static int? WarningSoundSlot(TimeSpan remaining) =>
        remaining < TimeSpan.Zero ? null : WarningSoundSlot(WholeMilliseconds(remaining));

    /// <summary>A span in whole milliseconds, as the original's 32-bit millisecond clock counts it.</summary>
    public static int WholeMilliseconds(TimeSpan span) =>
        unchecked((int)(span.Ticks / TimeSpan.TicksPerMillisecond));
}

public static class PlanningTimerLayout
{
    public static Rectangle Bar => new(520, 336, PlanningTimerPolicy.BarWidth, 3);
    public static IReadOnlyList<Rectangle> SetupChoices => SetupPanelLayout.PlanningTimes;
}

public enum PlanningTimerSignal
{
    None,
    LongWarning,
    FinalWarning,
    Expired
}

/// <summary>
/// The planning clock of a timed human turn (RULE-TIMER-002, RULE-TIMER-003).
/// </summary>
/// <remarks>
/// The bar is drawn when the clock starts and then on every sixth tick of the presentation clock,
/// and it keeps the width of its last redraw in between. The redraw countdown runs on every tick
/// whether or not a turn is timed and is not reset when a turn starts, so the first redraw after
/// the start comes one to six ticks later. Each redraw plays the warning its remaining time calls
/// for. The turn expires on the first update whose elapsed whole milliseconds exceed the limit.
/// </remarks>
public sealed class PlanningTimer
{
    private int _limit;
    private TimeSpan _start;
    private int _redrawCountdown = PlanningTimerPolicy.RefreshCountdown;
    private long? _lastTick;
    private TimeSpan? _pausedElapsed;

    public bool IsActive { get; private set; }

    /// <summary>The width the bar was last drawn with, 0 to 60.</summary>
    public int VisibleBarWidth { get; private set; } = PlanningTimerPolicy.BarWidth;

    public void Start(PlanningTimeLimit limit, TimeSpan now)
    {
        Stop();
        if (PlanningTimerPolicy.LimitMilliseconds(limit) is not { } milliseconds) return;
        _limit = milliseconds;
        _start = now;
        IsActive = true;
        // planning_timer_start draws the bar at once and leaves the countdown where it was.
        Redraw(now);
    }

    public void Stop()
    {
        IsActive = false;
        _limit = 0;
        _start = TimeSpan.Zero;
        _pausedElapsed = null;
        VisibleBarWidth = PlanningTimerPolicy.BarWidth;
    }

    public void Pause(TimeSpan now)
    {
        if (!IsActive || _pausedElapsed is not null) return;
        _pausedElapsed = now - _start;
    }

    public void Resume(TimeSpan now)
    {
        if (!IsActive || _pausedElapsed is not { } elapsed) return;
        _start = now - elapsed;
        _pausedElapsed = null;
        _lastTick = PresentationClock.Ticks(now);
    }

    public PlanningTimerSignal Advance(TimeSpan now)
    {
        if (_pausedElapsed is not null) return PlanningTimerSignal.None;
        var signal = PlanningTimerSignal.None;
        var tick = PresentationClock.Ticks(now);
        for (var pending = _lastTick is { } last ? tick - last : 0; pending > 0; pending--)
        {
            if (_redrawCountdown > 0) _redrawCountdown--;
            if (_redrawCountdown != 0) continue;
            _redrawCountdown = PlanningTimerPolicy.RefreshCountdown;
            if (IsActive) signal = Redraw(now);
        }
        _lastTick = tick;

        if (!IsActive) return PlanningTimerSignal.None;
        if (PlanningTimerPolicy.Expired(_limit, Elapsed(now)))
        {
            Stop();
            return PlanningTimerSignal.Expired;
        }
        return signal;
    }

    private int Elapsed(TimeSpan now) => PlanningTimerPolicy.WholeMilliseconds(now - _start);

    private PlanningTimerSignal Redraw(TimeSpan now)
    {
        var elapsed = Elapsed(now);
        VisibleBarWidth = PlanningTimerPolicy.VisibleBarWidth(_limit, elapsed);
        return PlanningTimerPolicy.WarningSoundSlot(unchecked(_limit - elapsed)) switch
        {
            7 => PlanningTimerSignal.LongWarning,
            8 => PlanningTimerSignal.FinalWarning,
            _ => PlanningTimerSignal.None,
        };
    }
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
        if (!_planningTimer.IsActive)
        {
            // The redraw countdown runs on the presentation ticks of untimed turns too.
            _planningTimer.Advance(now);
            return false;
        }
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || _state.Coordinator.Phase != TurnPhase.Command
            || _state.FindPlayer(playerId)?.Setup.Controller != PlayerController.Human
            || _screens.Current is ClientScreen.Title or ClientScreen.Setup
                or ClientScreen.Online or ClientScreen.Lobby
                or ClientScreen.Handoff or ClientScreen.Elimination or ClientScreen.Endgame)
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
            _online.PlanningTurn, _online.DeadlineAt, OnlineServerNow()))
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
            TimeSpan.FromSeconds(seconds), deadline - OnlineServerNow());
    }

    private void DrawPlanningTimer(SpriteBatch batch, Texture2D pixel)
    {
        if (_screens.Current is ClientScreen.Options or ClientScreen.Help || _idleGangWarningOpen)
            return;
        // Online the bar comes from the server's deadline; the local timer is never armed there.
        var width = _session is not null
            ? OnlineBarWidth()
            : _planningTimer.IsActive ? _planningTimer.VisibleBarWidth : null;
        if (width is not { } visible) return;

        batch.Draw(pixel, PlanningTimerLayout.Bar, Color.Black);
        if (visible > 0)
            batch.Draw(pixel, new Rectangle(
                PlanningTimerLayout.Bar.X, PlanningTimerLayout.Bar.Y,
                visible, PlanningTimerLayout.Bar.Height), Color.Lime);
    }
}
