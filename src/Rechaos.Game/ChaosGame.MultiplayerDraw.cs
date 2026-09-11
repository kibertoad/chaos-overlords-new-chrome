using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawOnline(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "ONLINE PLAY");
        DrawField(batch, pixel, font, OnlineConnectLayout.Server, _online.Server);
        DrawField(batch, pixel, font, OnlineConnectLayout.Name, _online.DisplayName);
        DrawField(batch, pixel, font, OnlineConnectLayout.JoinCode, _online.JoinCode);
        DrawField(batch, pixel, font, OnlineConnectLayout.Password, _online.Password);
        var busy = _online.Stage == MultiplayerStage.Busy;
        DrawButton(batch, pixel, font, OnlineConnectLayout.Host, "HOST", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Join, "JOIN", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Back, "BACK", !busy);
        DrawCentered(font, batch, _online.Status, OnlineConnectLayout.StatusY, Color.Gold, 1);
    }

    private void DrawLobby(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "LOBBY");
        font.Draw(batch, $"JOIN CODE  {_online.JoinCodeShown}", new Vector2(120, 120), Color.Gold, 2);
        var match = _online.Match;
        if (match is null)
        {
            DrawCentered(font, batch, "NO LOBBY", 200, Color.White, 1);
            return;
        }
        font.Draw(batch, $"{match.Settings.Name}", new Vector2(120, 156), Color.White, 1);
        font.Draw(
            batch,
            $"{SeatedPlayerCount(match)} OF {match.Settings.MaxPlayers} SEATED",
            new Vector2(120, 172),
            new Color(150, 165, 165),
            1);
        var row = 0;
        foreach (var player in match.Players.Where(Seated))
        {
            var colour = player.Slot >= 0 && player.Slot < PlayerColors.Length
                ? PlayerColors[player.Slot]
                : Color.White;
            var suffix = player.IsHost ? "  HOST" : string.Empty;
            font.Draw(batch, $"{player.DisplayName}{suffix}", new Vector2(136, 200 + row * 16), colour, 1);
            row++;
        }
        // Every unseated slot plays as a computer player, which is worth saying before the start.
        var computers = MatchLimits.PlayerCount - SeatedPlayerCount(match);
        if (computers > 0)
        {
            font.Draw(batch, $"{computers} COMPUTER PLAYERS WILL FILL THE REST",
                new Vector2(120, 200 + (row + 1) * 16), new Color(150, 165, 165), 1);
        }
        DrawButton(batch, pixel, font, LobbyStart, "START", _online.IsHost);
        DrawButton(batch, pixel, font, LobbyLeave, "LEAVE", true);
        DrawCentered(font, batch, _online.Status, 424, Color.Gold, 1);
    }

    private void DrawOnlinePanel(SpriteBatch batch, Texture2D pixel, PixelFont font, string title)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
        var panel = new Rectangle(100, 72, 440, 372);
        batch.Draw(pixel, panel, new Color(12, 22, 20, 250));
        DrawBorder(batch, pixel, panel, Color.Lime, 2);
        DrawCentered(font, batch, title, 88, Color.Gold, 2);
    }

    private static void DrawField(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Rectangle bounds,
        TextField field)
    {
        font.Draw(batch, field.Label, new Vector2(bounds.X, bounds.Y - 12), new Color(150, 165, 165), 1);
        batch.Draw(pixel, bounds, new Color(4, 10, 9));
        DrawBorder(batch, pixel, bounds, field.IsFocused ? Color.Gold : new Color(70, 90, 88), 1);
        font.Draw(batch, field.Display, new Vector2(bounds.X + 4, bounds.Y + 6), Color.White, 1);
    }

    /// <summary>
    /// Whether a roster entry is somebody the lobby is still counting.
    /// </summary>
    /// <remarks>
    /// A player who left or was kicked stays on the roster — the match's history needs them — so
    /// counting rows would over-report how full a lobby is and under-report how many computer players
    /// will fill the rest of the table.
    /// </remarks>
    private static bool Seated(PlayerView player) => player.Status == WirePlayerStatus.Active;

    private static int SeatedPlayerCount(MatchView match) => match.Players.Count(Seated);

    /// <summary>
    /// The countdown for the open turn, or an empty string when the match has no timer.
    /// </summary>
    /// <remarks>
    /// It is recomputed every frame from the deadline rather than counted down, so a paused match
    /// that resumes with a restarted clock corrects itself the moment the new deadline arrives.
    /// </remarks>
    private string OnlineCountdown()
    {
        if (_online.DeadlineAt is not { } deadline) return string.Empty;
        var remaining = deadline - DateTimeOffset.UtcNow;
        if (remaining <= TimeSpan.Zero) return "SEALING";
        return $"{(int)remaining.TotalMinutes:00}:{remaining.Seconds:00}";
    }

    /// <summary>
    /// A line for the city screen saying where the online turn stands.
    /// </summary>
    /// <remarks>
    /// A disconnection takes the line over, because it explains everything else on it: a countdown
    /// that is still running and a turn that is not resolving mean something quite different when the
    /// server has stopped answering, and the player is the one who can do something about it.
    /// </remarks>
    private string OnlineTurnStatus()
    {
        if (!_online.IsConnected) return $"RECONNECTING TO THE SERVER  {OnlineSeatTally()}";
        return _online.Stage switch
        {
            MultiplayerStage.WaitingForSeal =>
                $"WAITING FOR THE OTHER PLAYERS {OnlineSeatTally()} {OnlineCountdown()}",
            MultiplayerStage.Desynced => "MATCH PAUSED  REPAIRING A DESYNC",
            MultiplayerStage.Finished => "MATCH COMPLETE",
            MultiplayerStage.Playing => $"TURN {_online.PlanningTurn}  {OnlineCountdown()}",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// How many seats have finished planning, of the ones the turn seals on.
    /// </summary>
    /// <remarks>
    /// The point of showing it is that "waiting for the other players" does not say whether one
    /// opponent is deciding or four have closed the game. Empty until the server has said something
    /// about this turn's readiness, rather than claiming nobody is ready when nobody has reported.
    /// </remarks>
    private string OnlineSeatTally() =>
        _online.SeatedSeats > 0 ? $"{_online.ReadySeats}/{_online.SeatedSeats}" : string.Empty;
}
