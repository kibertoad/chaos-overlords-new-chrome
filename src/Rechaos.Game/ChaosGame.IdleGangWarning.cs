using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class IdleGangWarningLayout
{
    public static Rectangle Panel => new(138, 154, 364, 152);
    public static Rectangle Continue => new(164, 250, 142, 36);
    public static Rectangle GoBack => new(334, 250, 142, 36);
}

public static class IdleGangWarningPolicy
{
    public static bool ShouldWarn(bool enabled, IEnumerable<MatchGangState> gangs)
    {
        ArgumentNullException.ThrowIfNull(gangs);
        return enabled && gangs.Any(gang => gang.IsActive && gang.QueuedCommand is null);
    }
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
        _message = "SOME GANGS HAVE NO COMMANDS";
        PlayGeneralSound(0);
        return true;
    }

    private void UpdateIdleGangWarning(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Enter) || Pressed(keyboard, Keys.Y))
            ConfirmIdleGangWarning();
        else if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Back)
                 || Pressed(keyboard, Keys.N))
            CancelIdleGangWarning();
    }

    private void HandleIdleGangWarningClick(Point point)
    {
        if (IdleGangWarningLayout.Continue.Contains(point)) ConfirmIdleGangWarning();
        else if (IdleGangWarningLayout.GoBack.Contains(point)) CancelIdleGangWarning();
    }

    private void ConfirmIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        PlayGeneralSound(1);
        FinishPlanningTurn();
    }

    private void CancelIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        PlayGeneralSound(1);
        _message = "PLANNING CONTINUES";
    }

    private void DrawIdleGangWarning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 150));
        batch.Draw(pixel, IdleGangWarningLayout.Panel, new Color(10, 23, 25, 252));
        DrawBorder(batch, pixel, IdleGangWarningLayout.Panel, new Color(80, 180, 130), 2);
        DrawCentered(font, batch, "IDLE GANGS", 174, Color.Gold, 2);
        DrawCentered(font, batch, "SOME GANGS HAVE NO COMMANDS.", 214, Color.White, 1);
        DrawCentered(font, batch, "FINISH PLANNING ANYWAY?", 230, Color.White, 1);
        DrawButton(batch, pixel, font, IdleGangWarningLayout.Continue, "CONTINUE", true);
        DrawButton(batch, pixel, font, IdleGangWarningLayout.GoBack, "GO BACK", false);
    }
}
