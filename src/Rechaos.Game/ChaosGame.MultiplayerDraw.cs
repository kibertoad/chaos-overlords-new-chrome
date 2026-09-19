using System.Globalization;
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
        DrawOnlinePortraitChoice(batch, pixel, font);
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

    /// <summary>
    /// Draws the face this player will sit down under, and the arrows that turn it.
    /// </summary>
    /// <remarks>
    /// Beside the name, because the two are the same kind of thing: what the other players at the
    /// table see of whoever is about to take a seat. The art is the atlas the rest of the game draws
    /// overlords from, so the face chosen here is the one the city screen shows all match.
    /// </remarks>
    private void DrawOnlinePortraitChoice(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var bounds = OnlineConnectLayout.Portrait;
        font.Draw(batch, "FACE", CaptionAt(bounds), new Color(150, 165, 165), 1);
        batch.Draw(pixel, bounds, new Color(4, 10, 9));
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, bounds,
                OriginalSpriteLayout.OverlordPortrait(_online.Portrait), Color.White);
        }
        DrawBorder(batch, pixel, bounds, new Color(70, 90, 88), 1);
        DrawHorizontalArrow(
            batch, pixel, OnlineConnectLayout.PortraitPrevious, left: true, Color.Gold);
        DrawHorizontalArrow(
            batch, pixel, OnlineConnectLayout.PortraitNext, left: false, Color.Gold);
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
            var (listing, settings) = listings[index];
            var bounds = OnlineConnectLayout.DiscoveryRow(row);
            batch.Draw(pixel, bounds, index == _online.DiscoverySelection
                ? new Color(30, 62, 55) : new Color(4, 10, 9));
            DrawBorder(batch, pixel, bounds,
                index == _online.DiscoverySelection ? Color.Gold : new Color(70, 90, 88), 1);
            var phase = listing.Status == MatchStatus.Lobby ? "WAITING" : "ONGOING";
            font.Draw(batch, $"{listing.Name}  {phase}  {listing.PlayerCount}/{listing.MaxPlayers}",
                new Vector2(bounds.X + 6, bounds.Y + 6), Color.White, 1);
            // A session this build cannot read the settings of is still listed, so the second line
            // says so rather than naming a scenario and a mentality that were never read.
            font.Draw(batch,
                settings is { } known
                    ? $"{ScenarioCatalog.Get(known.Scenario).Name}  " +
                        $"{DifficultyPresentation.Label(known.AiMentality)}"
                    : "SETTINGS THIS VERSION OF THE GAME CANNOT READ",
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

    /// <summary>The colour of a session this build cannot play.</summary>
    private static readonly Color IncompatibleSession = new(220, 120, 90);

    private void DrawOnlineHistory(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlinePanel(batch, pixel, font, "UNFINISHED SESSIONS");
        var sessions = RecoverableOnlineSessions;
        var selected = SelectedOnlineRecovery;
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
            DrawHistoryRow(batch, font, bounds, recovery);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryRejoin, "REJOIN",
            selected is { CanResume: true });
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryBack, "BACK", true);
        DrawCentered(font, batch, OnlineHistoryPresentation.Footer(selected), 424,
            selected is { IsCompatible: false } ? IncompatibleSession : new Color(150, 165, 165), 1);
        DrawCentered(font, batch, _online.Status, 440, Color.Gold, 1);
    }

    /// <summary>The membership on one row, with the reason beside it when it cannot be taken.</summary>
    private static void DrawHistoryRow(
        SpriteBatch batch, PixelFont font, Rectangle bounds, MultiplayerRecovery recovery)
    {
        var roleOrNote = OnlineHistoryPresentation.Note(recovery)
            ?? (recovery.IsHost ? "HOST" : "PLAYER");
        var lastPlayed = LastPlayedLabel(recovery);
        var detail = new Color(150, 165, 165);
        font.Draw(batch, Fitted(SessionLabel(recovery), RowRoom(bounds, roleOrNote)),
            new Vector2(bounds.X + 7, bounds.Y + 6), Color.White, 1);
        DrawRightAligned(font, batch, roleOrNote, bounds.Right - 7, bounds.Y + 6,
            recovery.IsCompatible ? detail : IncompatibleSession);
        font.Draw(batch,
            Fitted($"{recovery.DisplayName}  {recovery.JoinCode}", RowRoom(bounds, lastPlayed)),
            new Vector2(bounds.X + 7, bounds.Y + 17), detail, 1);
        DrawRightAligned(font, batch, lastPlayed, bounds.Right - 7, bounds.Y + 17, detail);
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
    private static string Fitted(string value, Rectangle bounds) => Fitted(value, bounds.Width - 10);

    /// <summary>As much of a value as fits in a given width, in whole glyphs.</summary>
    private static string Fitted(string value, int width)
    {
        var columns = Math.Max(0, width) / OriginalFontLayout.CellWidth;
        return value.Length > columns ? value[..columns] : value;
    }

    /// <summary>
    /// What a session row's left-hand text may take up without running into its right-hand column.
    /// </summary>
    /// <remarks>Seven pixels of padding at each edge, and seven more between the two columns.</remarks>
    private static int RowRoom(Rectangle bounds, string rightHandText) =>
        bounds.Width - 21 - rightHandText.Length * OriginalFontLayout.CellWidth;

    /// <summary>The match's own name, or a stand-in where the record was written before one was kept.</summary>
    private static string SessionLabel(MultiplayerRecovery recovery) =>
        recovery.SessionName.Length > 0 ? recovery.SessionName : "UNNAMED SESSION";

    /// <summary>
    /// When the seat last had turn data stored, read in the player's own time zone.
    /// </summary>
    /// <remarks>
    /// Formatted invariantly rather than by the current culture: the font draws
    /// <see cref="OriginalFontLayout.FirstCharacter"/> through
    /// <see cref="OriginalFontLayout.LastCharacter"/> and nothing else, so a culture whose default
    /// calendar or digits fall outside that range would draw the row as blanks.
    /// </remarks>
    private static string LastPlayedLabel(MultiplayerRecovery recovery) =>
        recovery.LastUpdatedAt is { } updated
            ? updated.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "TIME UNKNOWN";

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
            // The face each player chose on their way in, so the roster says who is who by more
            // than a name: it is the face their overlord wears on every screen once the match runs.
            var face = OnlineLobbyLayout.RosterPortrait(row);
            if (_uiSprites is not null)
            {
                batch.Draw(_uiSprites, face,
                    OriginalSpriteLayout.OverlordPortrait(OnlinePortrait(player)), Color.White);
            }
            font.Draw(batch, $"{name}{suffix}",
                new Vector2(face.Right + 6, face.Y + 5), colour, 1);
            row++;
        }
        // Every unseated slot plays as a computer player, which is worth saying before the start.
        var computers = MatchLimits.PlayerCount - SeatedPlayerCount(match);
        if (computers > 0)
        {
            font.Draw(batch, $"{computers} COMPUTERS FILL THE REST",
                new Vector2(120, OnlineLobbyLayout.RosterPortrait(row).Y + 5),
                new Color(150, 165, 165), 1);
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
    ///
    /// An open vote on this player's own seat takes the line over from the stage it is planned in,
    /// and it is the only place they are told: the modal that asks everyone else about them is
    /// deliberately not shown to them (see <see cref="MultiplayerUiState.CurrentTakeoverVote"/>),
    /// and without this they would be looking at a stopped clock with nothing to explain it. It
    /// gives way in turn to a halt the whole match is under, on the same reasoning: a player owed
    /// both answers is owed the one that explains why nothing is resolving for anybody.
    /// </remarks>
    private string OnlineTurnStatus()
    {
        if (!_online.IsConnected)
            return $"RECONNECTING TO THE SERVER  ATTEMPT {_online.ReconnectAttempt}";
        if (_online.TurnSyncError.Length > 0)
            return $"TURN SYNC ERROR  {_online.TurnSyncError}";
        return _online.Stage switch
        {
            MultiplayerStage.Desynced => "MATCH PAUSED  REPAIRING A DESYNC",
            MultiplayerStage.Finished => "MATCH COMPLETE",
            _ when _online.OwnTakeoverVote is { } ownVote =>
                $"YOU MISSED TURN {ownVote.Turn}  THE OTHER PLAYERS ARE VOTING ON "
                    + "COMPUTER CONTROL OF YOUR SEAT",
            MultiplayerStage.WaitingForSeal =>
                _online.ReadySubmissionPending
                    ? "SENDING FINISHED TURN  AWAITING SERVER ACKNOWLEDGEMENT"
                    : _online.SeatedSeats > 0 && _online.ReadySeats >= _online.SeatedSeats
                        ? $"SERVER ACKNOWLEDGED  ALL PLAYERS READY {OnlineSeatTally()}"
                        : $"SERVER ACKNOWLEDGED  WAITING FOR OTHER PLAYERS "
                            + $"{OnlineSeatTally()} {OnlineCountdown()}",
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
}
