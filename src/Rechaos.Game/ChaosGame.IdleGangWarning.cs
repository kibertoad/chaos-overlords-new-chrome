using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class IdleGangWarningLayout
{
    // Native handler 0x00448718 draws and hit-tests PX05020 from (104,124).
    public static Rectangle Panel => new(104, 124, 344, 209);
    public static Rectangle Cancel => new(137, 261, 49, 22);
    public static Rectangle Ok => new(137, 293, 49, 22);

    /// <summary>The warning line the panel blinks on ticks of the presentation clock.</summary>
    public static Rectangle BlinkingLine => new(269, 169, 97, 9);

    /// <summary>
    /// Whether the warning line shows: six ticks of the presentation clock shown, then two filled
    /// black (RULE-UI-008, FND-UI-024). Which part of the cycle the panel opens on is not recorded.
    /// </summary>
    public static bool LineShown(TimeSpan now) => PresentationClock.Ticks(now) % 8 < 6;
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

    private bool TryOpenIdleGangWarning()
    {
        if (_state?.Coordinator.ActivePlayer is not { } playerId
            || !IdleGangWarningPolicy.ShouldWarn(
                _warnIfIdleGangs, _state.FindPlayer(playerId)!.Gangs))
            return false;

        _idleGangWarningOpen = true;
        _message = string.Empty;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelOpen);
        return true;
    }

    private void UpdateIdleGangWarning(KeyboardState keyboard)
    {
        switch (IdleGangWarningPolicy.KeyboardChoice(keyboard, _previousKeyboard))
        {
            case IdleGangWarningChoice.Confirm:
                ConfirmIdleGangWarning();
                break;
            case IdleGangWarningChoice.Cancel:
                CancelIdleGangWarning();
                break;
        }
    }

    private void HandleIdleGangWarningClick(Point point)
    {
        if (IdleGangWarningLayout.Ok.Contains(point)) ConfirmIdleGangWarning();
        else if (IdleGangWarningLayout.Cancel.Contains(point)) CancelIdleGangWarning();
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
        _idleGangWarningOpen = false;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        if (_session is not null) SubmitOnlineTurn();
        else FinishPlanningTurn();
    }

    private void CancelIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        _message = string.Empty;
    }

    private void DrawIdleGangWarning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_idleGangWarningBackground is not null)
            batch.Draw(_idleGangWarningBackground, IdleGangWarningLayout.Panel, Color.White);
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
        if (!IdleGangWarningLayout.LineShown(_inputTime))
            batch.Draw(pixel, IdleGangWarningLayout.BlinkingLine, Color.Black);
    }
}
