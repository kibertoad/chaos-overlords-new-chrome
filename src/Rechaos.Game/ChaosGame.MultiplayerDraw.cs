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
    private static readonly Rectangle StopReconnectButton = new(222, 354, 196, 28);

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
        {
            DrawSettingChoice(batch, pixel, font, "WHO CAN FIND IT",
                OnlineConnectLayout.PublicChoice, "PUBLIC",
                OnlineConnectLayout.PrivateChoice, "PRIVATE",
                _online.PublicListing);
        }
        if (OnlinePasswordApplies)
            DrawField(batch, pixel, font, OnlineConnectLayout.Password, _online.Password);
        else
            DrawDisabledField(batch, pixel, font, OnlineConnectLayout.Password, "PASSWORD",
                "THE JOIN CODE IS THE GATE");
        DrawButton(batch, pixel, font, OnlineConnectLayout.Continue,
            _online.Role == OnlineConnectRole.Host ? "CREATE" : "CONNECT", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Discover, "BROWSE", !busy);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Reconnect, "PREVIOUS SESSIONS",
            !busy && RecoverableOnlineSessions.Count > 0);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Back, "BACK", !busy);
        DrawCentered(font, batch, _online.ServerStatus, OnlineConnectLayout.ServerStatusY,
            _online.ServerStatus.EndsWith("ONLINE", StringComparison.Ordinal) ? Color.Lime : Color.Gold, 1);
        DrawCentered(font, batch, _online.Status, OnlineConnectLayout.StatusY, Color.Gold, 1);
    }

    private void DrawOnlineDiscovery(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "BROWSE GAMES");
        for (var filter = 0; filter < DiscoveryFilters.Count; filter++)
        {
            var bounds = OnlineConnectLayout.DiscoveryFilter(filter);
            DrawButton(batch, pixel, font,
                bounds, DiscoveryFilters.Label(filter, ChosenDiscoveryFilterOption(filter)), true);
            DrawDropdownArrow(batch, pixel, bounds, _online.OpenDiscoveryFilter == filter);
        }
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
                $"{ScenarioCatalog.Get(settings.Scenario).Name}  " +
                $"{DifficultyPresentation.Label(settings.AiMentality)}",
                new Vector2(bounds.X + 6, bounds.Y + 19), new Color(150, 165, 165), 1);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryJoin, "JOIN", listings.Count > 0);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryBack, "BACK", true);
        DrawCentered(font, batch, _online.Status, 432, Color.Gold, 1);
        DrawDiscoveryFilterMenu(batch, pixel, font);
    }

    /// <summary>Draws the open filter dropdown over everything else the screen has already drawn.</summary>
    private void DrawDiscoveryFilterMenu(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var filter = _online.OpenDiscoveryFilter;
        if (filter < 0) return;
        batch.Draw(pixel, OnlineConnectLayout.DiscoveryFilterMenu(filter), new Color(6, 14, 13));
        DrawBorder(batch, pixel, OnlineConnectLayout.DiscoveryFilterMenu(filter), Color.Gold, 1);
        var chosen = ChosenDiscoveryFilterOption(filter);
        for (var option = 0; option < DiscoveryFilters.OptionCount(filter); option++)
        {
            var bounds = OnlineConnectLayout.DiscoveryFilterOption(filter, option);
            if (option == _online.DiscoveryFilterHighlight)
                batch.Draw(pixel, new Rectangle(
                    bounds.X + 1, bounds.Y, bounds.Width - 2, bounds.Height), new Color(30, 62, 55));
            font.Draw(batch, DiscoveryFilters.Label(filter, option),
                new Vector2(bounds.X + 6, bounds.Y + 6),
                option == chosen ? Color.Gold : Color.White, 1);
        }
    }

    /// <summary>The three-row triangle that marks a button as a dropdown, pointing the way it opens.</summary>
    private static void DrawDropdownArrow(
        SpriteBatch batch, Texture2D pixel, Rectangle bounds, bool open)
    {
        var x = bounds.Right - 13;
        var y = bounds.Y + (bounds.Height - 3) / 2;
        for (var row = 0; row < 3; row++)
            batch.Draw(pixel, new Rectangle(
                x + row, y + (open ? 2 - row : row), 5 - row * 2, 1), Color.Gold);
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

    /// <summary>Where a control's caption goes, which is the same place for every one of them.</summary>
    private static Vector2 CaptionAt(Rectangle bounds) =>
        new(bounds.X, bounds.Y - OnlineConnectLayout.CaptionOffset);

    /// <summary>As much of a value as fits inside the box it is drawn in.</summary>
    private static string Fitted(string value, Rectangle bounds)
    {
        var columns = (bounds.Width - 10) / OriginalFontLayout.CellWidth;
        return value.Length > columns ? value[..columns] : value;
    }

    /// <summary>Draws a field's place while the choices on the screen leave it out of use.</summary>
    private static void DrawDisabledField(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Rectangle bounds,
        string label,
        string note)
    {
        font.Draw(batch, label, CaptionAt(bounds), new Color(90, 105, 100), 1);
        batch.Draw(pixel, bounds, new Color(12, 22, 20));
        DrawBorder(batch, pixel, bounds, new Color(55, 70, 66), 1);
        font.Draw(batch, note, new Vector2(bounds.X + 5, bounds.Y + 7), new Color(90, 105, 100), 1);
    }

    /// <summary>
    /// Draws a captioned pair of buttons, of which the one in force is lit.
    /// </summary>
    /// <remarks>
    /// The caption carries the question and the buttons carry the answers, so neither has to be read
    /// through the other: ALLOWED under JOIN AFTER START says what the session will do, where a lone
    /// button reading OFF leaves the player to work out what is off.
    /// </remarks>
    private void DrawSettingChoice(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        string caption,
        Rectangle left,
        string leftLabel,
        Rectangle right,
        string rightLabel,
        bool leftIsChosen)
    {
        font.Draw(batch, caption, CaptionAt(left), new Color(150, 165, 165), 1);
        DrawButton(batch, pixel, font, left, leftLabel, leftIsChosen);
        DrawButton(batch, pixel, font, right, rightLabel, !leftIsChosen);
    }

    private static void DrawReadOnlyServer(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font)
    {
        var bounds = OnlineConnectLayout.Server;
        font.Draw(batch, "SERVER", CaptionAt(bounds), new Color(150, 165, 165), 1);
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
            var name = player.DisplayName.Length > OnlineLobbyLayout.RosterNameColumns
                ? player.DisplayName[..OnlineLobbyLayout.RosterNameColumns]
                : player.DisplayName;
            font.Draw(batch, $"{name}{suffix}", new Vector2(136, 200 + row * 16), colour, 1);
            row++;
        }
        // Every unseated slot plays as a computer player, which is worth saying before the start.
        var computers = MatchLimits.PlayerCount - SeatedPlayerCount(match);
        if (computers > 0)
        {
            font.Draw(batch, $"{computers} COMPUTERS FILL THE REST",
                new Vector2(120, 200 + (row + 1) * 16), new Color(150, 165, 165), 1);
        }
        DrawLobbySettings(batch, pixel, font, match);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.CopyCode, "COPY CODE", true);
        var configurable = CanConfigureOnlineLobby();
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Setup, "GAME RULES", configurable);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Start, "START", configurable);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Leave, "LEAVE", true);
        DrawCentered(font, batch, _online.Status, 424, Color.Gold, 1);
    }

    /// <summary>
    /// Draws the settings of a lobby that has not started, which only its host may change.
    /// </summary>
    /// <remarks>
    /// A seated player reads them rather than being shown nothing: what the session is called,
    /// whether anyone can find it, and whether they can expect a latecomer are all things worth
    /// knowing before the match starts, and none of them are theirs to set.
    /// </remarks>
    private void DrawLobbySettings(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchView match)
    {
        if (CanConfigureOnlineLobby())
            DrawField(batch, pixel, font, OnlineLobbyLayout.SessionName, _online.SessionName);
        else
            DrawDisabledField(batch, pixel, font, OnlineLobbyLayout.SessionName, "SESSION NAME",
                Fitted(match.Settings.Name, OnlineLobbyLayout.SessionName));
        DrawSettingChoice(batch, pixel, font, "WHO CAN FIND IT",
            OnlineLobbyLayout.PublicChoice, "PUBLIC",
            OnlineLobbyLayout.PrivateChoice, "PRIVATE",
            _online.PublicListing);
        DrawSettingChoice(batch, pixel, font, "JOIN AFTER START",
            OnlineLobbyLayout.LateJoinAllowed, "ALLOWED",
            OnlineLobbyLayout.LateJoinRefused, "NOT ALLOWED",
            _online.AllowLateJoin);
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
        font.Draw(batch, field.Label, CaptionAt(bounds), new Color(150, 165, 165), 1);
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
    /// Marks every opponent still drafting this turn, under their portrait on the city top bar.
    /// </summary>
    /// <remarks>
    /// Drawn on black because the eight rows under the portraits are background art, which lime
    /// text alone is not reliably legible over. Offline there is nobody to wait for, and once the
    /// match is paused by a desync or over altogether nobody is drafting anything, so the captions
    /// go with the turn they describe rather than lingering as a state that cannot change.
    /// </remarks>
    private void DrawOpponentPlanning(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_session is null) return;
        var turnIsOpen = _online.Stage
            is MultiplayerStage.Playing or MultiplayerStage.WaitingForSeal;
        for (var slot = 0; slot < MatchLimits.PlayerCount; slot++)
        {
            if (!OpponentPlanningPresentation.IsDrafting(
                    slot, _session.Slot, turnIsOpen, _online.AwaitedSlots, _online.ReadySlots))
                continue;
            var caption = PlayerPortraitLayout.CityCaption(
                slot, OpponentPlanningPresentation.WaitingCaption.Length);
            batch.Draw(pixel, caption, Color.Black);
            font.Draw(batch, OpponentPlanningPresentation.WaitingCaption,
                new Vector2(caption.X, caption.Y), OpponentPlanningPresentation.WaitingColor, 1);
        }
    }

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
        if (!_online.IsConnected)
            return $"RECONNECTING TO THE SERVER  ATTEMPT {_online.ReconnectAttempt}";
        if (_online.TurnSyncError.Length > 0)
            return $"TURN SYNC ERROR  {_online.TurnSyncError}";
        return _online.Stage switch
        {
            MultiplayerStage.WaitingForSeal =>
                _online.ReadySubmissionPending
                    ? "SENDING FINISHED TURN  AWAITING SERVER ACKNOWLEDGEMENT"
                    : _online.SeatedSeats > 0 && _online.ReadySeats >= _online.SeatedSeats
                        ? $"SERVER ACKNOWLEDGED  ALL PLAYERS READY {OnlineSeatTally()}"
                        : $"SERVER ACKNOWLEDGED  WAITING FOR OTHER PLAYERS "
                            + $"{OnlineSeatTally()} {OnlineCountdown()}",
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

    /// <summary>Modal progress and diagnostics while the session reconnects in the background.</summary>
    private void DrawReconnectPopup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_screens.Current == ClientScreen.Online && _online.ConnectionError.Length > 0)
        {
            DrawOnlineErrorPopup(batch, pixel, font);
            return;
        }
        if (_session is null || _online.IsConnected) return;
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 190));
        var panel = new Rectangle(82, 82, 476, 316);
        batch.Draw(pixel, panel, new Color(12, 22, 20));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        DrawCentered(font, batch, "CONNECTION LOST  RECONNECTING", 102, Color.Gold, 1);
        font.Draw(batch, "AUTOMATIC RETRIES CONTINUE FOR UP TO FIVE MINUTES.",
            new Vector2(104, 132), Color.White, 1);
        font.Draw(batch, "RECENT ATTEMPTS", new Vector2(104, 162), new Color(150, 165, 165), 1);
        IReadOnlyList<string> lines = _online.ReconnectLog.Count == 0
            ? ["WAITING FOR THE NEXT ATTEMPT"]
            : _online.ReconnectLog;
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            if (line.Length > 66) line = line[..63] + "...";
            font.Draw(batch, line, new Vector2(104, 184 + index * 22), Color.White, 1);
        }
        DrawButton(batch, pixel, font, StopReconnectButton, "STOP RETRYING", true);
    }

    /// <summary>A modal error that keeps the complete diagnostic available without overflowing.</summary>
    private void DrawOnlineErrorPopup(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_screens.Current != ClientScreen.Online || _online.ConnectionError.Length == 0) return;
        batch.Draw(pixel, new Rectangle(0, 0, 640, 460), new Color(0, 0, 0, 200));
        var panel = OnlineConnectLayout.ErrorPanel;
        batch.Draw(pixel, panel, new Color(12, 22, 20));
        DrawBorder(batch, pixel, panel, Color.Gold, 2);
        var matchStopped = _online.ConnectionError.StartsWith(
            "ONLINE MATCH STOPPED", StringComparison.Ordinal);
        DrawCentered(font, batch, matchStopped ? "ONLINE MATCH ERROR" : "COULD NOT CONNECT",
            94, Color.Gold, 2);
        DrawCentered(font, batch,
            matchStopped ? "THE MATCH COULD NOT CONTINUE. DETAILS:" : "THE ONLINE REQUEST FAILED. DETAILS:",
            126, Color.White, 1);

        var lines = BugReportTextEditor.Wrap(_online.ConnectionError, 74);
        const int visibleRows = 9;
        for (var index = 0; index < Math.Min(visibleRows, lines.Count); index++)
        {
            var line = index == visibleRows - 1 && lines.Count > visibleRows
                ? lines[index][..Math.Min(lines[index].Length, 71)] + "..."
                : lines[index];
            DrawCentered(font, batch, line, 152 + index * 18, new Color(205, 215, 212), 1);
        }
        DrawCentered(font, batch, _online.ConnectionErrorCopyStatus, 320, Color.Lime, 1);
        DrawButton(batch, pixel, font, OnlineConnectLayout.CopyError, "COPY FULL ERROR", true);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DismissError, "CLOSE", false);
    }

    private void DrawReconnectPopupOverCurrentFrame(Viewport viewport)
    {
        if (_batch is null || _pixel is null || _font is null
            || _session is null || _online.IsConnected)
            return;
        _batch.Begin(
            samplerState: SamplerState.PointClamp,
            transformMatrix: VirtualInput.Transform(viewport));
        DrawReconnectPopup(_batch, _pixel, _font);
        _batch.End();
    }

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
