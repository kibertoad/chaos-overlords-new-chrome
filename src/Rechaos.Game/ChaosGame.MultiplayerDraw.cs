using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private void DrawOnline(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "ONLINE PLAY");
        DrawField(batch, pixel, font, OnlineServerField, _online.Server);
        DrawField(batch, pixel, font, OnlineNameField, _online.DisplayName);
        DrawField(batch, pixel, font, OnlineJoinCodeField, _online.JoinCode);
        var busy = _online.Stage == MultiplayerStage.Busy;
        DrawButton(batch, pixel, font, OnlineHost, "HOST", !busy);
        DrawButton(batch, pixel, font, OnlineJoin, "JOIN", !busy);
        DrawButton(batch, pixel, font, OnlineBack, "BACK", !busy);
        font.Draw(batch, "LEAVE THE CODE EMPTY TO HOST A NEW MATCH", new Vector2(120, 274),
            new Color(150, 165, 165), 1);
        font.Draw(batch, "THE SETUP SCREEN'S SCENARIO AND MENTALITY ARE USED", new Vector2(120, 288),
            new Color(150, 165, 165), 1);
        DrawCentered(font, batch, _online.Status, 320, Color.Gold, 1);
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
            $"{match.Players.Count} OF {match.Settings.MaxPlayers} SEATED",
            new Vector2(120, 172),
            new Color(150, 165, 165),
            1);
        var row = 0;
        foreach (var player in match.Players)
        {
            var colour = player.Slot >= 0 && player.Slot < PlayerColors.Length
                ? PlayerColors[player.Slot]
                : Color.White;
            var suffix = player.IsHost ? "  HOST" : string.Empty;
            font.Draw(batch, $"{player.DisplayName}{suffix}", new Vector2(136, 200 + row * 16), colour, 1);
            row++;
        }
        // Every unseated slot plays as a computer player, which is worth saying before the start.
        var computers = MatchLimitsPlayerCount - match.Players.Count;
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

    /// <summary>The six seats of the original game, named here so the draw does not reach for core.</summary>
    private const int MatchLimitsPlayerCount = 6;

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

    /// <summary>A line for the city screen saying where the online turn stands.</summary>
    private string OnlineTurnStatus() => _online.Stage switch
    {
        MultiplayerStage.WaitingForSeal => $"WAITING FOR THE OTHER PLAYERS {OnlineCountdown()}",
        MultiplayerStage.Desynced => "MATCH PAUSED  REPAIRING A DESYNC",
        MultiplayerStage.Playing => $"TURN {_online.PlanningTurn}  {OnlineCountdown()}",
        _ => string.Empty,
    };
}
