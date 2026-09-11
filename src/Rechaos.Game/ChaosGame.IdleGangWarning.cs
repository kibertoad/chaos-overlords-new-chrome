using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public static class IdleGangWarningLayout
{
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Cancel => EquipmentCommandLayout.Cancel;
    public static Rectangle Ok => EquipmentCommandLayout.Ok;
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
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelOpen);
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
        if (IdleGangWarningLayout.Ok.Contains(point)) ConfirmIdleGangWarning();
        else if (IdleGangWarningLayout.Cancel.Contains(point)) CancelIdleGangWarning();
    }

    private void ConfirmIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        FinishPlanningTurn();
    }

    private void CancelIdleGangWarning()
    {
        _idleGangWarningOpen = false;
        if (_slidePanels) PlayGeneralSound(GeneralSoundSlot.PanelClose);
        _message = "PLANNING CONTINUES";
    }

    private void DrawIdleGangWarning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_idleGangWarningBackground is not null)
            batch.Draw(_idleGangWarningBackground, IdleGangWarningLayout.Panel, Color.White);
        else
        {
            batch.Draw(pixel, IdleGangWarningLayout.Panel, new Color(10, 23, 25, 252));
            DrawBorder(batch, pixel, IdleGangWarningLayout.Panel, new Color(80, 180, 130), 2);
            font.Draw(batch, "SYSTEM WARNING: IDLE GANG DETECTED",
                new Vector2(150, 179), Color.Lime, 1);
            DrawButton(batch, pixel, font, IdleGangWarningLayout.Cancel, "CANCEL", false);
            DrawButton(batch, pixel, font, IdleGangWarningLayout.Ok, "OK", true);
        }
    }
}
