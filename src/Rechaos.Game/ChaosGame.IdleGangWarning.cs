using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class IdleGangWarningLayout
{
    // Native handler 0x00448718 (FND-OPTIONS-002) draws and hit-tests PX05020 from (104,124).
    public static Rectangle Panel => new(104, 124, 344, 209);
    public static Rectangle Cancel => new(137, 261, 49, 22);
    public static Rectangle Ok => new(137, 293, 49, 22);

    /// <summary>The warning line the panel blinks on ticks of the presentation clock.</summary>
    public static Rectangle BlinkingLine => new(269, 169, 97, 9);

    /// <summary>
    /// Whether the warning line shows <paramref name="ticks"/> ticks of the presentation clock
    /// after the panel opened: six ticks shown, then two filled black (RULE-UI-008, FND-UI-024),
    /// counted from the open (FND-UI-054).
    /// </summary>
    public static bool LineShown(long ticks)
    {
        if (ticks < 0) throw new ArgumentOutOfRangeException(nameof(ticks));
        return ticks % 8 < 6;
    }

    /// <summary>
    /// Whether the warning line shows at <paramref name="now"/> for a panel opened at
    /// <paramref name="openedAt"/>, counting the ticks of the presentation clock that fall after
    /// the open (FND-UI-054). The clock runs for the whole program, as slot 0 of the original's
    /// timer does (FND-TIMER-002), so the first tick comes at most one period after the open
    /// and the first shown part lasts more than five periods and at most six. A time before the
    /// open counts as the open.
    /// </summary>
    public static bool LineShown(TimeSpan openedAt, TimeSpan now) =>
        LineShown(now < openedAt ? 0 : PresentationClock.Ticks(now) - PresentationClock.Ticks(openedAt));
}

public static class IdleGangWarningPolicy
{
    public static bool ShouldWarn(bool enabled, IEnumerable<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        return enabled && gangs.Any(gang => gang.IsActive && gang.QueuedCommand is null);
    }

    public static IdleGangWarningChoice KeyboardChoice(
        KeyboardState current,
        KeyboardState previous)
    {
        bool Pressed(Keys key) => current.IsKeyDown(key) && !previous.IsKeyDown(key);

        if (Pressed(Keys.Enter) || Pressed(Keys.Execute)) return IdleGangWarningChoice.Confirm;
        return Pressed(Keys.Escape) ? IdleGangWarningChoice.Cancel : IdleGangWarningChoice.None;
    }
}

public enum IdleGangWarningChoice
{
    None,
    Confirm,
    Cancel
}

public sealed partial class ChaosGame
{
    private bool _idleGangWarningOpen;
    private TimeSpan _idleGangWarningOpenedAt;

    /// <summary>The presses on the warning, by region, to tell the second press of a double click.</summary>
    private readonly IndexedDoubleClickTracker _idleGangWarningClicks = new();

    private bool TryOpenIdleGangWarning()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || !IdleGangWarningPolicy.ShouldWarn(
                _warnIfIdleGangs, _state.FindPlayer(playerId)!.Gangs))
            return false;

        _idleGangWarningOpen = true;
        _idleGangWarningOpenedAt = PresentationDrawTime;
        _idleGangWarningClicks.Cancel();
        _message = string.Empty;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelOpen);
        return true;
    }

    private void UpdateIdleGangWarning(KeyboardState keyboard)
    {
        switch (IdleGangWarningPolicy.KeyboardChoice(keyboard, _previousKeyboard))
        {
            // FND-UI-024: the keys go through fn_00418CCC, which shows the face pressed for one
            // tick of the presentation clock before the panel acts (RULE-TIMER-004).
            case IdleGangWarningChoice.Confirm:
                PressKeyFace(PressedKeyFace.Confirm, IdleGangWarningLayout.Ok.Location, () =>
                {
                    // The planning timer may have closed the warning during the wait.
                    if (_idleGangWarningOpen) ConfirmIdleGangWarning();
                });
                break;
            case IdleGangWarningChoice.Cancel:
                PressKeyFace(PressedKeyFace.Cancel, IdleGangWarningLayout.Cancel.Location, () =>
                {
                    if (_idleGangWarningOpen) CancelIdleGangWarning();
                });
                break;
        }
    }

    /// <summary>
    /// A press on the warning (SCR-OPTIONS-001): Cancel and OK go through the held-button helper
    /// and act on a release inside themselves (FND-UI-047), and a press outside the panel is
    /// refused with slot 4. The second press of a double click does nothing (FND-UI-024).
    /// </summary>
    private void HandleIdleGangWarningClick(Point point)
    {
        var region = IdleGangWarningLayout.Ok.Contains(point) ? 1
            : IdleGangWarningLayout.Cancel.Contains(point) ? 2
            : IdleGangWarningLayout.Panel.Contains(point) ? 3
            : 0;
        if (_idleGangWarningClicks.Register(region, _inputTime)) return;
        if (region == 1)
            PressPanelFace(point, IdleGangWarningLayout.Panel, IdleGangWarningLayout.Ok,
                ConfirmIdleGangWarning);
        else
            PressPanelFace(point, IdleGangWarningLayout.Panel, IdleGangWarningLayout.Cancel,
                CancelIdleGangWarning, HeldButtonKind.Cancel);
    }

    /// <summary>
    /// Closes the warning and lets go of its face held under the pointer. The warning is drawn over
    /// the city or the sector view, so the held face's screen check cannot tell that it closed: a
    /// release where the face was would otherwise play slot 3, or answer a warning opened since.
    /// </summary>
    private void CloseIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        if (_pressedPanelFace is { } held
            && (held.Face == IdleGangWarningLayout.Ok || held.Face == IdleGangWarningLayout.Cancel))
            _pressedPanelFace = null;
        // The warning is not a screen of its own, so the screen change cannot clear its face.
        if (_releasedPanelFace is { } released
            && (released.Face == IdleGangWarningLayout.Ok || released.Face == IdleGangWarningLayout.Cancel))
            _releasedPanelFace = null;
    }

    /// <summary>
    /// The player said "yes, finish anyway", so the turn ends the way its match ends turns.
    /// </summary>
    /// <remarks>
    /// Online that means submitting the order document and waiting for the seal, not resolving
    /// here: a client that finished its own turn would be a turn ahead of everyone else's, and the
    /// sealed set would then apply the same orders a second time.
    /// </remarks>
    private void ConfirmIdleGangWarning()
    {
        CloseIdleGangWarning();
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        if (_session is not null) SubmitOnlineTurn();
        else FinishPlanningTurn();
    }

    private void CancelIdleGangWarning()
    {
        CloseIdleGangWarning();
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        _message = string.Empty;
    }

    private void DrawIdleGangWarning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (IdleGangWarningBackground is not null)
            batch.Draw(IdleGangWarningBackground, IdleGangWarningLayout.Panel, Color.White);
        else
        {
            batch.Draw(pixel, IdleGangWarningLayout.Panel, new Color(10, 23, 25, 252));
            DrawBorder(batch, pixel, IdleGangWarningLayout.Panel, new Color(80, 180, 130), 2);
            font.Draw(batch, "SYSTEM WARNING:", new Vector2(270, 171), Color.Red, 1);
            font.Draw(batch, "IDLE GANG DETECTED", new Vector2(239, 196), Color.Lime, 1);
            font.Draw(batch, "AT LEAST ONE OF YOUR GANGS HAS", new Vector2(222, 224), Color.Lime, 1);
            font.Draw(batch, "NOTHING TO DO. ARE YOU SURE YOU", new Vector2(218, 233), Color.Lime, 1);
            font.Draw(batch, "WANT TO END YOUR TURN?", new Vector2(246, 242), Color.Lime, 1);
            DrawButton(batch, pixel, font, IdleGangWarningLayout.Cancel, "CANCEL", false);
            DrawButton(batch, pixel, font, IdleGangWarningLayout.Ok, "OK", true);
        }
        // FND-UI-054: a reference frame draws the recorded ticks since the open, kept modulo 8.
        // FND-UI-047: the presentation clock stops while a face is held, and the line with it.
        // DEV-UI-027: Steady Lights keeps the line drawn.
        var shown = _referenceFrame?.IdlePhase is { } ticks
            ? IdleGangWarningLayout.LineShown(ticks)
            : _steadyLights || IdleGangWarningLayout.LineShown(_idleGangWarningOpenedAt, PresentationDrawTime);
        if (!shown)
            batch.Draw(pixel, IdleGangWarningLayout.BlinkingLine, Color.Black);
    }
}
