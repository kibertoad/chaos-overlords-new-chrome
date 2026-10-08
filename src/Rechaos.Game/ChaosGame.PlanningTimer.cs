using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
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

    /// <summary>
    /// The width for a countdown that is not the original's planning clock, such as an online turn
    /// deadline of up to a day. The percent is taken in 64 bits, because the 32-bit product of
    /// <see cref="RawBarWidth(int, int)"/> wraps once more than about six hours have passed.
    /// </summary>
    public static int VisibleBarWidth(TimeSpan duration, TimeSpan remaining)
    {
        if (duration <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
        if (remaining >= duration) return BarWidth;
        if (remaining <= TimeSpan.Zero) return 0;
        var limit = duration.Ticks / TimeSpan.TicksPerMillisecond;
        var elapsed = (duration - remaining).Ticks / TimeSpan.TicksPerMillisecond;
        return BarWidth - (int)(elapsed * 100 / limit * BarWidth / 100);
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

    // Ten seconds or more never warns, and is not narrowed to 32 bits, where a span of more than
    // about 24 days would wrap into the warning range.
    public static int? WarningSoundSlot(TimeSpan remaining) =>
        remaining < TimeSpan.Zero || remaining >= TimeSpan.FromSeconds(10)
            ? null
            : WarningSoundSlot(WholeMilliseconds(remaining));

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
    FinalWarning
}

/// <summary>
/// The planning clock of a timed human turn (RULE-TIMER-002, RULE-TIMER-003).
/// </summary>
/// <remarks>
/// <para>
/// The bar is drawn when the clock starts and then on every sixth tick of the presentation clock,
/// and it keeps the width of its last redraw in between. The redraw countdown runs on the ticks of
/// untimed turns too and is not reset when a turn starts, so the first redraw after the start comes
/// one to six ticks later. While a timed turn is paused (the game menu open) the countdown stops,
/// and <see cref="Resume"/> drops the ticks that passed. Each redraw plays the warning its
/// remaining time calls for.
/// </para>
/// <para>
/// The clock only reports expiry; the caller decides when to test it, because the original tests
/// it only on a pass of the planning loop. Until then a timed turn past its limit keeps redrawing
/// the empty bar. <see cref="Stop"/> leaves the bar as last drawn, and <see cref="ShowsBar"/> stays
/// set until the next start or <see cref="Clear"/> (RULE-TIMER-002).
/// </para>
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

    /// <summary>Whether a timed turn has drawn the bar since the last untimed start or clear.</summary>
    public bool ShowsBar { get; private set; }

    public void Start(PlanningTimeLimit limit, TimeSpan now)
    {
        if (PlanningTimerPolicy.LimitMilliseconds(limit) is not { } milliseconds)
        {
            Clear();
            return;
        }
        Stop();
        _limit = milliseconds;
        _start = now;
        IsActive = true;
        // planning_timer_start draws the bar at once and leaves the countdown where it was.
        Redraw(now);
    }

    /// <summary>Ends the timed turn and leaves the bar as last drawn (RULE-TIMER-002).</summary>
    public void Stop()
    {
        IsActive = false;
        _limit = 0;
        _start = TimeSpan.Zero;
        _pausedElapsed = null;
    }

    /// <summary>Ends the timed turn and forgets the bar, for leaving the match.</summary>
    public void Clear()
    {
        Stop();
        ShowsBar = false;
        VisibleBarWidth = PlanningTimerPolicy.BarWidth;
    }

    /// <summary>
    /// RULE-TIMER-002: whether the running turn's elapsed whole milliseconds exceed its limit. A
    /// paused turn has not expired.
    /// </summary>
    public bool HasExpired(TimeSpan now) =>
        IsActive && _pausedElapsed is null && PlanningTimerPolicy.Expired(_limit, Elapsed(now));

    public void Pause(TimeSpan now)
    {
        if (!IsActive || _pausedElapsed is not null) return;
        _pausedElapsed = now - _start;
    }

    public void Resume(TimeSpan now) => Resume(now, PresentationClock.Ticks(now));

    /// <summary>
    /// Resumes a paused turn, dropping the presentation ticks up to <paramref name="tick"/>, the
    /// count the caller advances the clock with.
    /// </summary>
    public void Resume(TimeSpan now, long tick)
    {
        if (!IsActive || _pausedElapsed is not { } elapsed) return;
        _start = now - elapsed;
        _pausedElapsed = null;
        _lastTick = tick;
    }

    public PlanningTimerSignal Advance(TimeSpan now) => Advance(now, PresentationClock.Ticks(now));

    /// <summary>
    /// Counts the presentation ticks up to <paramref name="tick"/> and redraws the bar when the
    /// countdown runs out. The game passes the ticks its event pump has taken, which stop while a
    /// hold keeps the pump from running (<see cref="EventPumpClock"/>). Tests pass the tick
    /// themselves to replay a recorded run of the original, whose ticks do not fall on exact
    /// multiples of the period.
    /// </summary>
    internal PlanningTimerSignal Advance(TimeSpan now, long tick)
    {
        if (_pausedElapsed is not null) return PlanningTimerSignal.None;
        var signal = PlanningTimerSignal.None;
        var pending = _lastTick is { } last ? tick - last : 0;
        _lastTick = tick;
        if (pending > 0 && pending < _redrawCountdown)
        {
            _redrawCountdown -= (int)pending;
        }
        else if (pending > 0)
        {
            // The countdown reached zero at least once in these ticks; every redraw among them
            // happens now, so one is drawn, and the countdown keeps the ticks since the last.
            var sinceLast = (pending - _redrawCountdown) % PlanningTimerPolicy.RefreshCountdown;
            _redrawCountdown = PlanningTimerPolicy.RefreshCountdown - (int)sinceLast;
            if (IsActive) signal = Redraw(now);
        }
        return signal;
    }

    private int Elapsed(TimeSpan now) => PlanningTimerPolicy.WholeMilliseconds(now - _start);

    private PlanningTimerSignal Redraw(TimeSpan now)
    {
        var elapsed = Elapsed(now);
        VisibleBarWidth = PlanningTimerPolicy.VisibleBarWidth(_limit, elapsed);
        ShowsBar = true;
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
    /// Only warnings: a client does not end an online turn, and answering the deadline locally is
    /// exactly what the online path must not do.
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

    /// <summary>Stops the clock and forgets its bar, when the match leaves the screen.</summary>
    private void ClearPlanningTimer() => _planningTimer.Clear();

    /// <summary>
    /// RULE-TIMER-002: whether this update stands for a pass of the original's planning loop, the
    /// only place it tests the time limit. The city and the detailed sector view are that loop
    /// (SCR-UI-003, SCR-UI-004). A panel runs its own loop, and so does the Hire handler from a
    /// press on an offer or its reject cross until the button is released (FND-HIRE-008), as do
    /// the console tile helper (FND-UI-032) and the sector view's back control (FND-UI-015); the
    /// idle-gang warning is answered before the test. A left press on the portrait of one of the
    /// player's gang cards runs the individual command handler's own loop until the button is
    /// released, whether or not the gang is dragged (FND-UI-044). The original's hold loops end only
    /// when the button comes up, so a hold the rebuild's Escape or right press lets go of keeps the
    /// test from running until then.
    /// </summary>
    private bool AtPlanningLoopPass() =>
        _screens.Current is ClientScreen.City or ClientScreen.Sector
        && !_idleGangWarningOpen
        && !HoldsCityPointer()
        && _pressedPanelFace is null;

    /// <summary>
    /// The presses on the city and the sector view that hold the original in a loop of its own
    /// until the left button comes up (the right one for a console tile it pressed, FND-UI-063), so
    /// that neither the planning loop (<see cref="AtPlanningLoopPass"/>) nor the event pump
    /// (<see cref="HoldsPointerOutsideEventPump"/>) runs: an offer and its reject cross
    /// (FND-HIRE-008), a console tile (FND-UI-032) and a gang card's portrait (FND-UI-044). A left
    /// hold the rebuild's Escape or right press lets go of still counts until the left button comes
    /// up, and a tile hold of the right button that Escape lets go of until the right one comes up,
    /// since the original's loop ends only then.
    /// </summary>
    private bool HoldsCityPointer() =>
        _draggedHireDefinitionId is not null
        || _pressedHireRejectSlot is not null
        || _pressedCityConsoleControl is not null
        || _draggedGangId is not null
        || _leftHoldOutlivesCancel
        || _rightHoldOutlivesCancel;

    /// <summary>
    /// Whether a press holds the game in a loop of the original that dispatches window messages
    /// without calling the event pump, so the steps the pump drives stop (<see cref="EventPumpClock"/>).
    /// They are the Hire handler's two loops for an offer and its reject cross (FND-HIRE-008), the
    /// individual command handler's loops for a gang card's portrait (FND-UI-044), the console tile
    /// helper (FND-UI-032), the Last Turn Events page arrows (FND-EVENT-007) and the held-button
    /// helper behind the faces of the panels, the Comlink Send panel, the attack picker, the
    /// idle-gang warning, the Search panel's ALL, NONE and Done, Detailed Combat's Exit face and
    /// the sector view's back control (FND-UI-046, FND-UI-047). Each loop runs until the button
    /// that pressed it comes up: the left one, or the right one for a console tile pressed with it
    /// (FND-UI-063).
    /// </summary>
    private bool HoldsPointerOutsideEventPump() =>
        HoldsCityPointer()
        || _pressedEventsButton is not null
        || _pressedCommandPanelButton is not null
        || _pressedComlinkSendButton is not null
        || _pressedAttackFace is not null
        || _combatExit.Tracking
        || _pressedPanelFace is not null;

    /// <summary>
    /// Whether the original would be in a loop that does not call the event pump: a pointer hold
    /// (<see cref="HoldsPointerOutsideEventPump"/>) or a soundtrack fade. The fade runs inside the
    /// pump's music step and leaves timer slot 0 alone, so the pump takes no tick until it ends
    /// and then takes the one the flag kept (FND-AUDIO-017).
    /// </summary>
    private bool OutsideEventPump() => HoldsPointerOutsideEventPump() || _soundtrackFade is not null;

    /// <summary>
    /// Called by a cancel that lets go of one of the holds <see cref="HoldsCityPointer"/> lists,
    /// so the planning loop and the event pump stay out until the left button comes up (FND-UI-044,
    /// FND-HIRE-008).
    /// </summary>
    private void KeepLeftHoldUntilRelease()
    {
        if (_previousMouse.LeftButton == ButtonState.Pressed) _leftHoldOutlivesCancel = true;
    }

    /// <summary>
    /// The same for a console tile the right button holds, whose helper loop ends only when the
    /// right button comes up (FND-UI-063).
    /// </summary>
    private void KeepRightHoldUntilRelease()
    {
        if (_previousMouse.RightButton == ButtonState.Pressed) _rightHoldOutlivesCancel = true;
    }

    /// <summary>The screens that are not the match, where no planning clock is drawn or run.</summary>
    private bool LeftMatchScreen() =>
        _screens.Current is ClientScreen.Title or ClientScreen.Setup
            or ClientScreen.Online or ClientScreen.Lobby or ClientScreen.Spectate
            or ClientScreen.Handoff or ClientScreen.Elimination or ClientScreen.Endgame;

    private bool UpdatePlanningTimer(TimeSpan now)
    {
        if (!_planningTimer.IsActive)
        {
            // The redraw countdown runs on the presentation ticks of untimed turns too.
            _planningTimer.Advance(now, _eventPump.Ticks);
            return false;
        }
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || _state.Coordinator.Phase != TurnPhase.Command
            || _state.FindPlayer(playerId)?.Setup.Controller != PlayerController.Human
            || LeftMatchScreen())
        {
            StopPlanningTimer();
            return false;
        }

        switch (_planningTimer.Advance(now, _eventPump.Ticks))
        {
            case PlanningTimerSignal.LongWarning:
                PlayGeneralSound(GeneralSoundSlot.CountdownWarning);
                break;
            case PlanningTimerSignal.FinalWarning:
                PlayGeneralSound(GeneralSoundSlot.FinalSecondWarning);
                break;
        }
        // A panel open past the limit keeps the empty bar up, and the turn ends on the first pass
        // after it closes.
        if (!AtPlanningLoopPass() || !_planningTimer.HasExpired(now)) return false;

        StopPlanningTimer();
        _message = string.Empty;
        // Online this never runs, because the clock is not armed there. It still goes through the
        // online path rather than straight to the local resolution, so that arming it later cannot
        // silently resolve a turn on one client alone.
        if (_session is not null) SubmitOnlineTurn();
        else FinishPlanningTurn();
        return true;
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
        // Locally the bar stays as last drawn after planning ends, through the resolution, until
        // the next timed start redraws it (RULE-TIMER-002).
        var width = _session is not null
            ? OnlineBarWidth()
            : _planningTimer.ShowsBar && !LeftMatchScreen() ? _planningTimer.VisibleBarWidth : null;
        if (width is not { } visible) return;

        batch.Draw(pixel, PlanningTimerLayout.Bar, Color.Black);
        if (visible > 0)
            batch.Draw(pixel, new Rectangle(
                PlanningTimerLayout.Bar.X, PlanningTimerLayout.Bar.Y,
                visible, PlanningTimerLayout.Bar.Height), Color.Lime);
    }
}
