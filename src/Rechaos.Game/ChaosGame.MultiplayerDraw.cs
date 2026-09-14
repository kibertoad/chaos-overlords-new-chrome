using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle TakeoverVoteWait = new(164, 300, 140, 32);
    private static readonly Rectangle TakeoverVoteComputer = new(336, 300, 140, 32);

    private void DrawOnline(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_online.Stage == MultiplayerStage.History)
        {
            DrawOnlineHistory(batch, pixel, font);
            return;
        }
        if (_online.Stage == MultiplayerStage.Discover)
        {
            DrawOnlineDiscovery(batch, pixel, font);
            return;
        }
        if (_online.Stage == MultiplayerStage.LateJoinSeat)
        {
            DrawLateJoinSeats(batch, pixel, font);
            return;
        }
        DrawOnlinePanel(batch, pixel, font, "ONLINE PLAY");
        DrawButton(batch, pixel, font, OnlineConnectLayout.Central, "CENTRAL",
            _online.Service == OnlineServiceMode.Central);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Custom, "CUSTOM",
            _online.Service == OnlineServiceMode.Custom);
        if (_online.Service == OnlineServiceMode.Custom)
            DrawField(batch, pixel, font, OnlineConnectLayout.Server, _online.Server);
        else
            DrawReadOnlyServer(batch, pixel, font);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HostRole, "HOST",
            _online.Role == OnlineConnectRole.Host);
        DrawButton(batch, pixel, font, OnlineConnectLayout.JoinRole, "JOIN",
            _online.Role == OnlineConnectRole.Join);
        DrawField(batch, pixel, font, OnlineConnectLayout.Name, _online.DisplayName);
        var busy = _online.Stage == MultiplayerStage.Busy;
        if (_online.Role == OnlineConnectRole.Join)
        {
            DrawField(batch, pixel, font, OnlineConnectLayout.JoinCode, _online.JoinCode);
            DrawButton(batch, pixel, font, OnlineConnectLayout.PasteJoinCode, "PASTE", !busy);
        }
        else
            DrawField(batch, pixel, font, OnlineConnectLayout.JoinCode, _online.SessionName);
        DrawField(batch, pixel, font, OnlineConnectLayout.Password, _online.Password);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Continue,
            _online.Role == OnlineConnectRole.Host ? "CREATE" : "CONNECT", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Back, "BACK", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Discover, "DISCOVER", !busy);
        if (RecoverableOnlineSessions.Count > 0)
            DrawButton(batch, pixel, font, OnlineConnectLayout.Reconnect, "SESSIONS", !busy);
        DrawCentered(font, batch, _online.ServerStatus, OnlineConnectLayout.ServerStatusY,
            _online.ServerStatus.EndsWith("ONLINE", StringComparison.Ordinal) ? Color.Lime : Color.Gold, 1);
        DrawCentered(font, batch, _online.Status, OnlineConnectLayout.StatusY, Color.Gold, 1);
    }

    private void DrawOnlineDiscovery(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "DISCOVER GAMES");
        var status = _online.DiscoveryStatusFilter switch
        {
            1 => "WAITING",
            2 => "ONGOING",
            _ => "ALL STATES",
        };
        var scenario = _online.DiscoveryScenarioFilter < 0 ? "ALL MODES"
            : ((ScenarioId)_online.DiscoveryScenarioFilter).ToString().ToUpperInvariant();
        var ai = _online.DiscoveryAiFilter < 0 ? "ALL AI"
            : DifficultyPresentation.Label((AiDifficulty)_online.DiscoveryAiFilter);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryStatus, status, true);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryScenario, scenario, true);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryAi, ai, true);
        var listings = FilteredOnlineListings();
        var offset = Math.Clamp(_online.DiscoverySelection - 4, 0, Math.Max(0, listings.Count - 5));
        for (var row = 0; row < Math.Min(5, listings.Count - offset); row++)
        {
            var index = offset + row;
            var listing = listings[index];
            var bounds = OnlineConnectLayout.DiscoveryRow(row);
            batch.Draw(pixel, bounds, index == _online.DiscoverySelection
                ? new Color(30, 62, 55) : new Color(4, 10, 9));
            DrawBorder(batch, pixel, bounds,
                index == _online.DiscoverySelection ? Color.Gold : new Color(70, 90, 88), 1);
            var phase = listing.Status == MatchStatus.Lobby ? "WAITING" : "ONGOING";
            font.Draw(batch, $"{listing.Name}  {phase}  {listing.PlayerCount}/{listing.MaxPlayers}",
                new Vector2(bounds.X + 6, bounds.Y + 6), Color.White, 1);
            var settings = MultiplayerGameSettings.FromWire(listing.Settings.GameSettings);
            font.Draw(batch,
                $"{settings.Scenario}  {DifficultyPresentation.Label(settings.AiMentality)}",
                new Vector2(bounds.X + 6, bounds.Y + 19), new Color(150, 165, 165), 1);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryJoin, "JOIN", listings.Count > 0);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryBack, "BACK", true);
        DrawCentered(font, batch, _online.Status, 432, Color.Gold, 1);
    }

    private void DrawOnlineHistory(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "UNFINISHED SESSIONS");
        var sessions = RecoverableOnlineSessions;
        var offset = Math.Clamp(_online.RecoverySelection - 5, 0, Math.Max(0, sessions.Count - 6));
        for (var row = 0; row < Math.Min(6, sessions.Count - offset); row++)
        {
            var index = offset + row;
            var recovery = sessions[index];
            var bounds = OnlineConnectLayout.HistoryRow(row);
            batch.Draw(pixel, bounds, index == _online.RecoverySelection
                ? new Color(30, 62, 55)
                : new Color(4, 10, 9));
            DrawBorder(batch, pixel, bounds,
                index == _online.RecoverySelection ? Color.Gold : new Color(70, 90, 88), 1);
            var role = recovery.IsHost ? "HOST" : "PLAYER";
            font.Draw(batch, $"{recovery.DisplayName}  {recovery.JoinCode}  {role}",
                new Vector2(bounds.X + 7, bounds.Y + 9), Color.White, 1);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryRejoin, "REJOIN", sessions.Count > 0);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryBack, "BACK", true);
        DrawCentered(font, batch, "UP/DOWN SELECT  ENTER REJOINS", 424,
            new Color(150, 165, 165), 1);
        DrawCentered(font, batch, _online.Status, 440, Color.Gold, 1);
    }

    private void DrawLateJoinSeats(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "CHOOSE COMPUTER EMPIRE");
        var seats = _online.PendingLateJoin?.AvailableSeatSummaries ?? [];
        for (var row = 0; row < Math.Min(6, seats.Count); row++)
        {
            var seat = seats[row];
            var bounds = OnlineConnectLayout.HistoryRow(row);
            batch.Draw(pixel, bounds, row == _online.LateJoinSeatSelection
                ? new Color(30, 62, 55) : new Color(4, 10, 9));
            DrawBorder(batch, pixel, bounds,
                row == _online.LateJoinSeatSelection ? Color.Gold : new Color(70, 90, 88), 1);
            font.Draw(batch,
                $"PLAYER {seat.Slot + 1}   {seat.Gangs} GANGS   {seat.Sites} SITES   {seat.Sectors} SECTORS",
                new Vector2(bounds.X + 7, bounds.Y + 9), Color.White, 1);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryRejoin, "TAKE OVER", seats.Count > 0);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryBack, "BACK", true);
        DrawCentered(font, batch, _online.Status, 440, Color.Gold, 1);
    }

    private static void DrawDisabledJoinCode(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font)
    {
        var bounds = OnlineConnectLayout.JoinCode;
        font.Draw(batch, "JOIN CODE", new Vector2(bounds.X, bounds.Y - 11),
            new Color(90, 105, 100), 1);
        batch.Draw(pixel, bounds, new Color(12, 22, 20));
        DrawBorder(batch, pixel, bounds, new Color(55, 70, 66), 1);
        font.Draw(batch, "NOT NEEDED WHEN HOSTING", new Vector2(bounds.X + 5, bounds.Y + 7),
            new Color(90, 105, 100), 1);
    }

    private static void DrawReadOnlyServer(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font)
    {
        var bounds = OnlineConnectLayout.Server;
        font.Draw(batch, "SERVER", new Vector2(bounds.X, bounds.Y - 11), new Color(150, 165, 165), 1);
        batch.Draw(pixel, bounds, new Color(18, 35, 32));
        DrawBorder(batch, pixel, bounds, new Color(70, 105, 95), 1);
        font.Draw(batch, MultiplayerServiceEndpoint.Central.ToString().TrimEnd('/'),
            new Vector2(bounds.X + 5, bounds.Y + 7), Color.White, 1);
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
        DrawButton(batch, pixel, font, LobbyCopyCode, "COPY CODE", true);
        DrawButton(batch, pixel, font, LobbySetup, "SETUP", _online.IsHost);
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

    private void DrawTakeoverVote(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_gameMenuOpen || _online.CurrentTakeoverVote is not { } vote || _session is null) return;
        var panel = new Rectangle(120, 154, 400, 198);
        batch.Draw(pixel, panel, new Color(6, 12, 12, 248));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "PLAYER ABSENT", 174, Color.Gold, 2);
        DrawCentered(font, batch, vote.DisplayName.ToUpperInvariant(), 212, Color.White, 1);
        DrawCentered(font, batch, $"MISSED TURN {vote.Turn}", 232, new Color(150, 165, 165), 1);
        var eligible = _online.Match?.Players.Count(player => player.Status == WirePlayerStatus.Active) ?? 0;
        var approvals = vote.Votes.Count(entry => entry.Value == TakeoverChoice.Computer);
        DrawCentered(font, batch, $"AI APPROVALS {approvals}/{eligible}  UNANIMOUS REQUIRED", 256,
            new Color(150, 165, 165), 1);
        DrawButton(batch, pixel, font, TakeoverVoteWait, "WAIT", true);
        DrawButton(batch, pixel, font, TakeoverVoteComputer, "USE AI", true);
    }
}
