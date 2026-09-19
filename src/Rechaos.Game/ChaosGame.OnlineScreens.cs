using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

/// <summary>
/// The screens that get a player from the title screen into an online match.
/// </summary>
/// <remarks>
/// One frame, drawn by <see cref="DrawOnlineFrame"/>, and five bodies inside it: the connect form,
/// the browser, the unfinished sessions, the seat picker and the lobby. Each body reads as a stack
/// of captioned groups, each group answers one question, and every screen ends with the same status
/// line and the same row of actions in the same place, so moving between them moves nothing the eye
/// has already found.
/// </remarks>
public sealed partial class ChaosGame
{
    /// <summary>
    /// The colours the whole flow is drawn in, so that one meaning keeps one colour.
    /// </summary>
    /// <remarks>
    /// White is what the player typed or what the server said; <see cref="OnlineSecondaryText"/> is
    /// everything that names or qualifies it; <see cref="OnlineMutedText"/> is what is not in use,
    /// or not there yet. A field and a list row share one inset fill because they are the same
    /// thing: a well the eye reads a value out of.
    /// </remarks>
    private static readonly Color OnlineSecondaryText = new(150, 165, 165);
    private static readonly Color OnlineMutedText = new(96, 112, 107);
    private static readonly Color OnlineInsetFill = new(4, 10, 9);
    private static readonly Color OnlineOutline = new(70, 90, 88);
    private static readonly Color OnlineRuleColour = new(46, 66, 62);
    private static readonly Color OnlineSelectedRow = new(30, 62, 55);

    /// <summary>The colour of a session this build cannot play.</summary>
    private static readonly Color IncompatibleSession = new(220, 120, 90);

    private void DrawOnline(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        switch (_online.Stage)
        {
            case MultiplayerStage.History:
                DrawOnlineHistory(batch, pixel, font);
                return;
            case MultiplayerStage.Discover:
                DrawOnlineDiscovery(batch, pixel, font);
                return;
            case MultiplayerStage.LateJoinSeat:
                DrawLateJoinSeats(batch, pixel, font);
                return;
            default:
                DrawOnlineConnect(batch, pixel, font);
                return;
        }
    }

    /// <summary>
    /// The connect form, read top to bottom in the order the questions matter.
    /// </summary>
    /// <remarks>
    /// What the player wants to do comes first because it decides what the rest of the form asks;
    /// who they are comes next; then the one thing that is particular to this session; then its
    /// password; and last the server, which almost nobody changes and which used to be the first
    /// thing the screen demanded. The ways into a game that are not this form — the browser and the
    /// sessions already half-played — stand apart from it, above the rule, so that the row at the
    /// foot of the screen holds only what the form itself does.
    /// </remarks>
    private void DrawOnlineConnect(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlineFrame(batch, pixel, font, "ONLINE PLAY");
        var busy = _online.Stage == MultiplayerStage.Busy;
        var hosting = _online.Role == OnlineConnectRole.Host;
        var secondary = busy ? ButtonEmphasis.Disabled : ButtonEmphasis.Secondary;

        DrawChoicePair(batch, pixel, font, "WHAT DO YOU WANT TO DO",
            OnlineConnectLayout.HostRole, "HOST A NEW GAME",
            OnlineConnectLayout.JoinRole, "JOIN WITH A CODE",
            hosting, !busy);

        DrawField(batch, pixel, font, OnlineConnectLayout.Name, _online.DisplayName);
        DrawOnlinePortraitChoice(batch, pixel, font);

        if (hosting)
        {
            DrawChoicePair(batch, pixel, font, "WHO CAN FIND IT",
                OnlineConnectLayout.PublicChoice, "PUBLIC",
                OnlineConnectLayout.PrivateChoice, "PRIVATE",
                _online.PublicListing, !busy);
        }
        else
        {
            DrawField(batch, pixel, font, OnlineConnectLayout.JoinCode, _online.JoinCode,
                "THE 8 CHARACTERS THE HOST READS OUT");
            DrawButton(batch, pixel, font, OnlineConnectLayout.PasteJoinCode, "PASTE", secondary);
        }

        if (OnlinePasswordApplies)
        {
            DrawField(batch, pixel, font, OnlineConnectLayout.Password, _online.Password,
                hosting ? "LEAVE BLANK FOR NO PASSWORD" : "ONLY IF THE HOST SET ONE");
        }
        else
        {
            DrawDisabledField(batch, pixel, font, OnlineConnectLayout.Password, "PASSWORD",
                "THE JOIN CODE IS THE GATE");
        }

        DrawServerChoice(batch, pixel, font, busy);

        DrawButton(batch, pixel, font, OnlineConnectLayout.Discover, "BROWSE GAMES", secondary);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Reconnect, "UNFINISHED SESSIONS",
            !busy && RecoverableOnlineSessions.Count > 0
                ? ButtonEmphasis.Secondary
                : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Continue,
            hosting ? "CREATE GAME" : "JOIN GAME",
            busy ? ButtonEmphasis.Disabled : ButtonEmphasis.Primary);
        DrawButton(batch, pixel, font, OnlineConnectLayout.Back, "BACK", secondary);
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
        font.Draw(batch, "FACE", OnlineScreenLayout.CaptionAt(bounds), OnlineSecondaryText, 1);
        batch.Draw(pixel, bounds, OnlineInsetFill);
        if (_uiSprites is not null)
        {
            batch.Draw(_uiSprites, bounds,
                OriginalSpriteLayout.OverlordPortrait(_online.Portrait), Color.White);
        }
        DrawBorder(batch, pixel, bounds, OnlineOutline, 1);
        DrawHorizontalArrow(
            batch, pixel, OnlineConnectLayout.PortraitPrevious, left: true, Color.Gold);
        DrawHorizontalArrow(
            batch, pixel, OnlineConnectLayout.PortraitNext, left: false, Color.Gold);
    }

    /// <summary>
    /// The service, its address, and whether it is answering, on one row.
    /// </summary>
    /// <remarks>
    /// The health of the server is reported opposite the word SERVER rather than adrift at the foot
    /// of the screen, because it is a fact about this row. The central service's address is drawn as
    /// text, not as a field: nobody may edit it, and a box around it only invited the attempt.
    /// </remarks>
    private void DrawServerChoice(SpriteBatch batch, Texture2D pixel, PixelFont font, bool busy)
    {
        var custom = _online.Service == OnlineServiceMode.Custom;
        font.Draw(batch, "SERVER",
            OnlineScreenLayout.CaptionAt(OnlineConnectLayout.Central), OnlineSecondaryText, 1);
        DrawRightAligned(font, batch, _online.ServerStatus,
            OnlineScreenLayout.ContentRight, OnlineConnectLayout.ServerStatusY,
            ServerStatusColour(_online.ServerStatus));
        DrawChoice(batch, pixel, font, OnlineConnectLayout.Central, "CENTRAL", !custom, !busy);
        DrawChoice(batch, pixel, font, OnlineConnectLayout.Custom, "CUSTOM", custom, !busy);
        if (custom)
        {
            DrawFieldBox(batch, pixel, font, OnlineConnectLayout.Server, _online.Server,
                "HTTP://HOST:PORT");
            return;
        }
        DrawReadOnlyValue(batch, pixel, font, OnlineConnectLayout.Server,
            MultiplayerServiceEndpoint.Central.Host);
    }

    /// <summary>
    /// Lime only for a server that is up and encrypted.
    /// </summary>
    /// <remarks>
    /// A server reached in clear reports ONLINE followed by why that is not the whole story, so the
    /// line stops being green the moment the seat token and the lobby password would cross a network
    /// readable. See <see cref="SendsCredentialsInClear"/>.
    /// </remarks>
    private static Color ServerStatusColour(string status) =>
        status.EndsWith("ONLINE", StringComparison.Ordinal) ? Color.Lime : Color.Gold;

    private void DrawOnlineDiscovery(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlineFrame(batch, pixel, font, "BROWSE GAMES");
        var listings = FilteredOnlineListings();
        var window = ListScrollWindow.Of(
            listings.Count, _online.DiscoverySelection, OnlineScreenLayout.ListRows);
        DrawListHeading(batch, font, OnlineConnectLayout.DiscoveryStatus.Y,
            "FILTER", window.Tally("GAMES"));
        for (var filter = 0; filter < DiscoveryFilters.Count; filter++)
        {
            var bounds = OnlineConnectLayout.DiscoveryFilter(filter);
            DrawButton(batch, pixel, font, bounds,
                DiscoveryFilters.Label(filter, ChosenDiscoveryFilterOption(filter)),
                ButtonEmphasis.Secondary);
            DrawDropdownArrow(batch, pixel, bounds, _online.OpenDiscoveryFilter == filter);
        }
        if (listings.Count == 0)
        {
            DrawEmptyList(batch, font, OnlineConnectLayout.DiscoveryTop,
                _online.Listings.Count == 0
                    ? "NO PUBLIC GAMES ARE OPEN RIGHT NOW"
                    : "NO GAME MATCHES THESE FILTERS",
                _online.Listings.Count == 0
                    ? "PRESS REFRESH, OR GO BACK AND HOST ONE"
                    : "WIDEN A FILTER TO SEE MORE");
        }
        for (var row = 0; row < window.VisibleRows; row++)
        {
            var index = window.IndexAt(row);
            var bounds = OnlineConnectLayout.DiscoveryRow(row);
            DrawListRow(batch, pixel, bounds, index == _online.DiscoverySelection);
            DrawListingRow(batch, font, bounds, listings[index]);
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryJoin, "JOIN",
            CanCallOnlineLobby(listings.Count)
                ? ButtonEmphasis.Primary
                : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryRefresh, "REFRESH",
            CanCallOnlineLobby() ? ButtonEmphasis.Secondary : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineConnectLayout.DiscoveryBack, "BACK",
            ButtonEmphasis.Secondary);
        DrawDiscoveryFilterMenu(batch, pixel, font);
    }

    /// <summary>What one listed session says: its name and how full it is, over what is being played.</summary>
    private static void DrawListingRow(
        SpriteBatch batch, PixelFont font, Rectangle bounds, DiscoveredListing entry)
    {
        var (listing, settings) = entry;
        var phase = listing.Status == MatchStatus.Lobby ? "WAITING" : "ONGOING";
        var seats = $"{phase}  {listing.PlayerCount}/{listing.MaxPlayers}";
        font.Draw(batch, Fitted(listing.Name, RowRoom(bounds, seats)),
            new Vector2(bounds.X + 7, bounds.Y + 8), Color.White, 1);
        DrawRightAligned(font, batch, seats, bounds.Right - 7, bounds.Y + 8, OnlineSecondaryText);
        // A session this build cannot read the settings of is still listed, so the second line says
        // so rather than naming a scenario and a mentality that were never read.
        font.Draw(batch,
            settings is { } known
                ? $"{ScenarioCatalog.Get(known.Scenario).Name}  " +
                    $"{DifficultyPresentation.Label(known.AiMentality)}"
                : "SETTINGS THIS VERSION OF THE GAME CANNOT READ",
            new Vector2(bounds.X + 7, bounds.Y + 21), OnlineSecondaryText, 1);
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
                    bounds.X + 1, bounds.Y, bounds.Width - 2, bounds.Height), OnlineSelectedRow);
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
        DrawOnlineFrame(batch, pixel, font, "UNFINISHED SESSIONS");
        var sessions = RecoverableOnlineSessions;
        var selected = SelectedOnlineRecovery;
        var window = ListScrollWindow.Of(
            sessions.Count, _online.RecoverySelection, OnlineScreenLayout.ListRows);
        DrawListHeading(batch, font, OnlineConnectLayout.HistoryTop,
            "SEATS YOU ARE STILL HOLDING", window.Tally("SEATS"));
        for (var row = 0; row < window.VisibleRows; row++)
        {
            var index = window.IndexAt(row);
            var bounds = OnlineConnectLayout.HistoryRow(row);
            DrawListRow(batch, pixel, bounds, index == _online.RecoverySelection);
            DrawHistoryRow(batch, font, bounds, sessions[index]);
        }
        if (sessions.Count == 0)
        {
            DrawEmptyList(batch, font, OnlineConnectLayout.HistoryTop,
                "NO UNFINISHED SESSION IS WAITING FOR YOU",
                "A SEAT APPEARS HERE WHEN A MATCH IS LEFT MID-GAME");
        }
        DrawCentered(font, batch, OnlineHistoryPresentation.Footer(selected),
            OnlineConnectLayout.HistoryNoteY,
            selected is { IsCompatible: false } ? IncompatibleSession : OnlineSecondaryText, 1);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryRejoin, "REJOIN",
            selected is { CanResume: true } ? ButtonEmphasis.Primary : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryBack, "BACK",
            ButtonEmphasis.Secondary);
    }

    /// <summary>The membership on one row, with the reason beside it when it cannot be taken.</summary>
    private static void DrawHistoryRow(
        SpriteBatch batch, PixelFont font, Rectangle bounds, MultiplayerRecovery recovery)
    {
        var roleOrNote = OnlineHistoryPresentation.Note(recovery)
            ?? (recovery.IsHost ? "HOST" : "PLAYER");
        var lastPlayed = LastPlayedLabel(recovery);
        font.Draw(batch, Fitted(SessionLabel(recovery), RowRoom(bounds, roleOrNote)),
            new Vector2(bounds.X + 7, bounds.Y + 8), Color.White, 1);
        DrawRightAligned(font, batch, roleOrNote, bounds.Right - 7, bounds.Y + 8,
            recovery.IsCompatible ? OnlineSecondaryText : IncompatibleSession);
        font.Draw(batch,
            Fitted($"{recovery.DisplayName}  {recovery.JoinCode}", RowRoom(bounds, lastPlayed)),
            new Vector2(bounds.X + 7, bounds.Y + 21), OnlineSecondaryText, 1);
        DrawRightAligned(font, batch, lastPlayed, bounds.Right - 7, bounds.Y + 21,
            OnlineSecondaryText);
    }

    private void DrawLateJoinSeats(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlineFrame(batch, pixel, font, "TAKE OVER AN EMPIRE");
        var seats = _online.PendingLateJoin?.AvailableSeatSummaries ?? [];
        var window = ListScrollWindow.Of(
            seats.Count, _online.LateJoinSeatSelection, OnlineScreenLayout.ListRows);
        DrawListHeading(batch, font, OnlineConnectLayout.HistoryTop,
            "COMPUTER EMPIRES NOBODY HAS PLAYED", window.Tally("SEATS"));
        for (var row = 0; row < window.VisibleRows; row++)
        {
            var index = window.IndexAt(row);
            var seat = seats[index];
            var bounds = OnlineConnectLayout.HistoryRow(row);
            DrawListRow(batch, pixel, bounds, index == _online.LateJoinSeatSelection);
            font.Draw(batch, $"PLAYER {seat.Slot + 1}",
                new Vector2(bounds.X + 7, bounds.Y + 15), Color.White, 1);
            DrawRightAligned(font, batch,
                $"{seat.Gangs} GANGS   {seat.Sites} SITES   {seat.Sectors} SECTORS",
                bounds.Right - 7, bounds.Y + 15, OnlineSecondaryText);
        }
        if (seats.Count == 0)
        {
            DrawEmptyList(batch, font, OnlineConnectLayout.HistoryTop,
                "THIS GAME HAS NO SEAT TO TAKE OVER", "EVERY EMPIRE IN IT HAS HAD A PLAYER");
        }
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryRejoin, "TAKE OVER",
            CanCallOnlineLobby(seats.Count) ? ButtonEmphasis.Primary : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineConnectLayout.HistoryBack, "BACK",
            ButtonEmphasis.Secondary);
    }

    /// <summary>
    /// The lobby: who is here on the left, what they are about to play on the right.
    /// </summary>
    /// <remarks>
    /// The join code leads, at double height and with the button that copies it beside it, because
    /// reading it out is the one thing a host has to do here. The line above the actions says what
    /// the lobby is waiting for, which is different for the host and for everybody else and was
    /// previously left for them to work out from which buttons were lit.
    /// </remarks>
    private void DrawLobby(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlineFrame(batch, pixel, font, "LOBBY");
        var match = _online.Match;
        var configurable = CanConfigureOnlineLobby();
        var busy = _lobby?.IsBusy == true;
        DrawLobbyJoinCode(batch, pixel, font);
        if (match is not null)
        {
            DrawLobbyRoster(batch, font, match);
            DrawLobbySettings(batch, pixel, font, match);
        }
        DrawCentered(font, batch,
            match is null
                ? "WAITING FOR THE SERVER TO DESCRIBE THE LOBBY"
                : configurable
                    ? "READ OUT THE JOIN CODE, THEN PRESS START"
                    : "WAITING FOR THE HOST TO START THE MATCH",
            OnlineLobbyLayout.WaitingHintY, OnlineSecondaryText, 1);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Start, "START",
            configurable && !busy ? ButtonEmphasis.Primary : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Leave, "LEAVE",
            ButtonEmphasis.Secondary);
    }

    private void DrawLobbyJoinCode(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        var code = _online.JoinCodeShown;
        font.Draw(batch, "JOIN CODE",
            new Vector2(OnlineLobbyLayout.RosterLeft, OnlineLobbyLayout.JoinCodeCaptionY),
            OnlineSecondaryText, 1);
        font.Draw(batch, HasLobbyJoinCode ? code : "--------",
            new Vector2(OnlineLobbyLayout.RosterLeft, OnlineLobbyLayout.JoinCodeY),
            HasLobbyJoinCode ? Color.Gold : OnlineMutedText, 2);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.CopyCode, "COPY",
            HasLobbyJoinCode ? ButtonEmphasis.Secondary : ButtonEmphasis.Disabled);
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

    /// <summary>Who has taken a seat, and how many computer players will fill the rest.</summary>
    private void DrawLobbyRoster(SpriteBatch batch, PixelFont font, MatchView match)
    {
        var seated = SeatedPlayerCount(match);
        font.Draw(batch, "PLAYERS",
            new Vector2(OnlineLobbyLayout.RosterLeft, OnlineLobbyLayout.RosterCaptionY),
            OnlineSecondaryText, 1);
        DrawRightAligned(font, batch, $"{seated} OF {match.Settings.MaxPlayers}",
            OnlineLobbyLayout.RosterRight, OnlineLobbyLayout.RosterCaptionY, OnlineSecondaryText);
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
        var computers = MatchLimits.PlayerCount - seated;
        if (computers <= 0) return;
        font.Draw(batch, $"{computers} COMPUTERS FILL THE REST",
            new Vector2(OnlineLobbyLayout.RosterLeft, OnlineLobbyLayout.RosterPortrait(row).Y + 5),
            OnlineMutedText, 1);
    }

    /// <summary>
    /// Draws the settings of a lobby that has not started, which only its host may change.
    /// </summary>
    /// <remarks>
    /// A seated player reads them rather than being shown nothing: what the session is called,
    /// whether anyone can find it, and whether they can expect a latecomer are all things worth
    /// knowing before the match starts, and none of them are theirs to set. They are drawn in the
    /// same place either way, dimmed rather than removed, so the two views of the lobby are the
    /// same picture.
    /// </remarks>
    private void DrawLobbySettings(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchView match)
    {
        var configurable = CanConfigureOnlineLobby();
        if (configurable)
            DrawField(batch, pixel, font, OnlineLobbyLayout.SessionName, _online.SessionName);
        else
            DrawDisabledField(batch, pixel, font, OnlineLobbyLayout.SessionName, "SESSION NAME",
                Fitted(match.Settings.Name, OnlineLobbyLayout.SessionName));
        DrawChoicePair(batch, pixel, font, "WHO CAN FIND IT",
            OnlineLobbyLayout.PublicChoice, "PUBLIC",
            OnlineLobbyLayout.PrivateChoice, "PRIVATE",
            _online.PublicListing, configurable);
        DrawChoicePair(batch, pixel, font, "JOIN AFTER START",
            OnlineLobbyLayout.LateJoinAllowed, "ALLOWED",
            OnlineLobbyLayout.LateJoinRefused, "NOT ALLOWED",
            _online.AllowLateJoin, configurable);
        DrawLobbySummary(batch, font);
        DrawButton(batch, pixel, font, OnlineLobbyLayout.Setup, "CHANGE GAME RULES",
            configurable ? ButtonEmphasis.Secondary : ButtonEmphasis.Disabled);
    }

    /// <summary>
    /// What the match will be, which everybody in the lobby reads and only the host changes.
    /// </summary>
    /// <remarks>
    /// Taken from this client's own setup choices, which a seated player has had the lobby's
    /// settings written into and a host holds because they are the ones who chose them. See
    /// <see cref="AdoptLobbySettings"/>, which leaves them alone when the host wrote a settings
    /// document this build cannot read — so the summary says it cannot read it rather than
    /// presenting this client's own local setup as what everybody is about to play.
    /// </remarks>
    private void DrawLobbySummary(SpriteBatch batch, PixelFont font)
    {
        font.Draw(batch, "WHAT YOU WILL PLAY",
            new Vector2(OnlineLobbyLayout.SettingsLeft, OnlineLobbyLayout.SummaryCaptionY),
            OnlineSecondaryText, 1);
        if (!_online.LobbySettingsReadable)
        {
            for (var line = 0; line < OnlineLobbySummary.Unreadable.Count; line++)
            {
                var bounds = OnlineLobbyLayout.SummaryRow(line);
                font.Draw(batch, OnlineLobbySummary.Unreadable[line],
                    new Vector2(bounds.X, bounds.Y), OnlineMutedText, 1);
            }
            return;
        }
        var rows = OnlineLobbySummary.Rows(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _selectedPlanningTimeLimit);
        for (var index = 0; index < rows.Count; index++)
        {
            var (label, value) = rows[index];
            var bounds = OnlineLobbyLayout.SummaryRow(index);
            font.Draw(batch, label, new Vector2(bounds.X, bounds.Y), OnlineMutedText, 1);
            DrawRightAligned(font, batch, Fitted(value, RowRoom(bounds, label)),
                bounds.Right, bounds.Y, Color.White);
        }
    }

    /// <summary>
    /// The frame every online screen stands in.
    /// </summary>
    /// <remarks>
    /// Background, panel, title, the rule that closes the header, the rule that opens the footer and
    /// the status line under it. The footer is reserved whether or not there is anything to say, so
    /// that a message arriving never moves the buttons beneath it — and so that no screen has to
    /// find room for its status of its own accord, which is how two of them ended up writing theirs
    /// through the panel's bottom border.
    /// </remarks>
    private void DrawOnlineFrame(SpriteBatch batch, Texture2D pixel, PixelFont font, string title)
    {
        batch.Draw(pixel, new Rectangle(0, 0, VirtualInput.Width, VirtualInput.Height), Color.Black);
        var panel = OnlineScreenLayout.Panel;
        batch.Draw(pixel, panel, new Color(12, 22, 20, 250));
        DrawBorder(batch, pixel, panel, Color.Lime, 2);
        DrawCentered(font, batch, title, OnlineScreenLayout.TitleY, Color.Gold, 2);
        batch.Draw(pixel, OnlineScreenLayout.Rule(OnlineScreenLayout.HeaderRuleY), OnlineRuleColour);
        batch.Draw(pixel, OnlineScreenLayout.Rule(OnlineScreenLayout.FooterRuleY), OnlineRuleColour);
        DrawCentered(font, batch, _online.Status, OnlineScreenLayout.StatusY, Color.Gold, 1);
    }

    /// <summary>A list's caption, with what it is showing of how many opposite it.</summary>
    private static void DrawListHeading(
        SpriteBatch batch, PixelFont font, int listTop, string caption, string tally)
    {
        var y = listTop - OnlineScreenLayout.CaptionOffset;
        font.Draw(batch, caption,
            new Vector2(OnlineScreenLayout.ContentLeft, y), OnlineSecondaryText, 1);
        DrawRightAligned(font, batch, tally, OnlineScreenLayout.ContentRight, y, OnlineSecondaryText);
    }

    /// <summary>The box one entry of a list is drawn in, lit when it is the selected one.</summary>
    private static void DrawListRow(
        SpriteBatch batch, Texture2D pixel, Rectangle bounds, bool selected)
    {
        batch.Draw(pixel, bounds, selected ? OnlineSelectedRow : OnlineInsetFill);
        DrawBorder(batch, pixel, bounds, selected ? Color.Gold : OnlineOutline, 1);
    }

    /// <summary>
    /// What stands where a list's rows would have been when it has none.
    /// </summary>
    /// <remarks>
    /// An empty list used to draw nothing at all, which reads as a screen that has not finished
    /// loading. Saying that there is nothing, and what would put something there, is the difference
    /// between a dead end and a next step.
    /// </remarks>
    private static void DrawEmptyList(
        SpriteBatch batch, PixelFont font, int listTop, string message, string hint)
    {
        DrawCentered(font, batch, message, listTop + 46, OnlineSecondaryText, 1);
        DrawCentered(font, batch, hint, listTop + 64, OnlineMutedText, 1);
    }

    /// <summary>
    /// A captioned pair of controls, of which the one in force is lit.
    /// </summary>
    /// <remarks>
    /// The caption carries the question and the segments carry the answers, so neither has to be
    /// read through the other: ALLOWED under JOIN AFTER START says what the session will do, where a
    /// lone button reading OFF leaves the player to work out what is off.
    /// </remarks>
    private static void DrawChoicePair(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        string caption,
        Rectangle left,
        string leftLabel,
        Rectangle right,
        string rightLabel,
        bool leftIsChosen,
        bool enabled = true)
    {
        font.Draw(batch, caption, OnlineScreenLayout.CaptionAt(left),
            enabled ? OnlineSecondaryText : OnlineMutedText, 1);
        DrawChoice(batch, pixel, font, left, leftLabel, leftIsChosen, enabled);
        DrawChoice(batch, pixel, font, right, rightLabel, !leftIsChosen, enabled);
    }

    /// <summary>A captioned text field.</summary>
    private static void DrawField(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Rectangle bounds,
        TextField field,
        string hint = "")
    {
        font.Draw(batch, field.Label, OnlineScreenLayout.CaptionAt(bounds), OnlineSecondaryText, 1);
        DrawFieldBox(batch, pixel, font, bounds, field, hint);
    }

    /// <summary>
    /// The field itself, without its caption, for the one row that captions two controls at once.
    /// </summary>
    /// <remarks>
    /// An empty field shows what belongs in it rather than a blank box, and a value too long for the
    /// box is drawn from its end, which is where the caret is: typing a long server address used to
    /// run the text out through the border and off the panel.
    /// </remarks>
    private static void DrawFieldBox(
        SpriteBatch batch,
        Texture2D pixel,
        PixelFont font,
        Rectangle bounds,
        TextField field,
        string hint)
    {
        batch.Draw(pixel, bounds, OnlineInsetFill);
        DrawBorder(batch, pixel, bounds, field.IsFocused ? Color.Gold : OnlineOutline, 1);
        var position = new Vector2(bounds.X + 5, TextBaseline(bounds));
        var text = field.Display;
        if (text.Length == 0)
        {
            if (hint.Length > 0) font.Draw(batch, Fitted(hint, bounds), position, OnlineMutedText, 1);
            return;
        }
        font.Draw(batch, Tail(text, bounds.Width - 10), position, Color.White, 1);
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
        font.Draw(batch, label, OnlineScreenLayout.CaptionAt(bounds), OnlineMutedText, 1);
        batch.Draw(pixel, bounds, new Color(12, 22, 20));
        DrawBorder(batch, pixel, bounds, new Color(55, 70, 66), 1);
        font.Draw(batch, Fitted(note, bounds),
            new Vector2(bounds.X + 5, TextBaseline(bounds)), OnlineMutedText, 1);
    }

    /// <summary>A value nobody may edit, drawn as text rather than in a box that invites the attempt.</summary>
    private static void DrawReadOnlyValue(
        SpriteBatch batch, Texture2D pixel, PixelFont font, Rectangle bounds, string value)
    {
        batch.Draw(pixel, bounds, new Color(10, 18, 17));
        DrawBorder(batch, pixel, bounds, new Color(55, 70, 66), 1);
        font.Draw(batch, Fitted(value, bounds),
            new Vector2(bounds.X + 5, TextBaseline(bounds)), Color.White, 1);
    }

    /// <summary>Where a single line of text sits in a control, which is the middle of it.</summary>
    private static int TextBaseline(Rectangle bounds) =>
        bounds.Y + (bounds.Height - OriginalFontLayout.GlyphHeight) / 2;

    /// <summary>As much of a value as fits inside the box it is drawn in.</summary>
    private static string Fitted(string value, Rectangle bounds) => Fitted(value, bounds.Width - 10);

    /// <summary>As much of a value as fits in a given width, in whole glyphs.</summary>
    private static string Fitted(string value, int width)
    {
        var columns = Math.Max(0, width) / OriginalFontLayout.CellWidth;
        return value.Length > columns ? value[..columns] : value;
    }

    /// <summary>The end of a value, which is where the caret is, when the whole of it will not fit.</summary>
    private static string Tail(string value, int width)
    {
        var columns = Math.Max(0, width) / OriginalFontLayout.CellWidth;
        return value.Length > columns ? value[^columns..] : value;
    }

    /// <summary>
    /// What a list row's left-hand text may take up without running into its right-hand column.
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
}
