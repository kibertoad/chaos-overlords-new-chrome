using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using CorePlayerStatus = Rechaos.Core.GameModel.PlayerStatus;

namespace Rechaos.Game;

/// <summary>
/// Watching an online match some turns behind its players, and the list of who is watching.
/// </summary>
/// <remarks>
/// <para>
/// Online play departs from the original as a whole (DEV-NET-001), and the view draws the city
/// screen's art (SCR-UI-003) with a panel of its own over the command buttons.
/// </para>
/// <para>
/// The spectator view keeps its own state, apart from <c>_state</c>: the watched match is never the
/// match this client plays, no planning screen is opened on it, and nothing the view does reaches
/// the server but the spectator's own leave. The city is drawn from the copy
/// <see cref="MultiplayerSpectatorWatch"/> hands over, which is only ever what the server has
/// released.
/// </para>
/// <para>
/// The map is drawn for one seat at a time, the way a player's own city is drawn for them, because
/// what a seat sees (its site markers and the gangs it has sighted) is part of what the map shows.
/// The spectator chooses the seat.
/// </para>
/// </remarks>
public sealed partial class ChaosGame
{
    private MultiplayerSpectatorWatch? _spectatorWatch;
    private SpectatorMatchView? _spectatorView;
    private MatchState? _spectatorState;
    private bool _spectatorHasState;
    private int? _spectatorShownTurn;
    private bool _spectatorComplete;
    private bool _spectatorConnected = true;
    private int _spectatorFollowedSeat;
    private int _spectatorCursor;
    private string _spectatorJoinCode = string.Empty;
    private string _spectatorPassword = string.Empty;
    private Uri? _spectatorServer;
    private MultiplayerRecovery? _spectatorRecovery;

    // The view's own map caches: a hot-seat match can stay alive behind the online screens, and
    // its planning snapshot (RULE-UI-006) and site-search selections must survive the watch.
    private readonly GangSightSnapshotCache _spectatorGangSight = new();
    private readonly GangStatusMarkerMap _spectatorGangMarkers = new();
    private static readonly IReadOnlySet<short> NoSiteSearchSelections = new HashSet<short>();

    /// <summary>Where a spectator's memberships are kept, apart from the seats.</summary>
    /// <remarks>
    /// A build that predates spectating reads <c>multiplayer-recovery.json</c> and would take a
    /// spectator's record there for a seat, so these never go in it.
    /// </remarks>
    private string SpectatorRecoveryPath =>
        Path.Combine(Path.GetDirectoryName(_multiplayerRecoveryPath) ?? string.Empty,
            "multiplayer-spectating.json");

    /// <summary>Asks to watch the match whose join code is on the connect form.</summary>
    private void BeginWatch()
    {
        if (!RequireUsableName()) return;
        var code = _online.JoinCode.Value.Trim();
        if (code.Length == 0)
        {
            _online.Status = "ENTER A JOIN CODE";
            return;
        }
        if (!TrySelectedServer(out var server))
        {
            _online.Status = "THE SERVER ADDRESS MUST BE AN HTTP OR HTTPS URL";
            return;
        }
        SavePreferences();
        var password = OptionalPassword();
        var name = _online.DisplayName.Value.Trim();
        StartWatch(server, code, password,
            watch => watch.Join(new SpectateRequest(code, name, password)));
    }

    /// <summary>Whether the browser's selected session lets anyone watch it.</summary>
    private bool CanWatchSelectedListing(IReadOnlyList<DiscoveredListing> listings) =>
        CanCallOnlineLobby(listings.Count)
        && listings[Math.Clamp(_online.DiscoverySelection, 0, listings.Count - 1)]
            .Listing.Settings.SpectatorDelayTurns is not null;

    /// <summary>Asks to watch the session selected in the browser.</summary>
    private void WatchSelectedOnlineListing()
    {
        var listings = FilteredOnlineListings();
        if (_lobby is not { } lobby || !CanWatchSelectedListing(listings) || !RequireUsableName()) return;
        var listing = listings[Math.Clamp(_online.DiscoverySelection, 0, listings.Count - 1)].Listing;
        var password = OptionalPassword();
        var name = _online.DisplayName.Value.Trim();
        StartWatch(lobby.BaseAddress, listing.JoinCode, password,
            watch => watch.Join(new SpectateRequest(listing.JoinCode, name, password)));
    }

    /// <summary>Follows a match again with a spectator token kept from an earlier watch.</summary>
    private void ResumeWatch(MultiplayerRecovery recovery, Uri server)
    {
        // A record whose watch never started stays as it was, so it can be rejoined later.
        if (!StartWatch(server, recovery.JoinCode, recovery.Password,
                watch => watch.Resume(recovery.MatchId, recovery.Token)))
            return;
        _spectatorRecovery = recovery;
        // A resumed watch is never announced as joined, so the form is released here.
        _online.Stage = MultiplayerStage.Connect;
        _online.Status = string.Empty;
        _screens.Show(ClientScreen.Spectate);
    }

    /// <returns>Whether the watch was started.</returns>
    private bool StartWatch(
        Uri server, string joinCode, string? password, Action<MultiplayerSpectatorWatch> begin)
    {
        if (_definitions is null)
        {
            _online.Status = "THE GAME DATA IS NOT LOADED";
            return false;
        }
        // The browser's lobby session has done its job; a spectator holds no seat to poll.
        Forget(_lobby?.StopAsync(), "multiplayer.lobby.stop.failed");
        _lobby = null;
        CloseDiscoveryFilterMenu();
        CancelServerProbe();
        ForgetSpectatorView();
        _spectatorServer = server;
        _spectatorJoinCode = joinCode;
        _spectatorPassword = password ?? string.Empty;
        _spectatorWatch = new MultiplayerSpectatorWatch(
            _http, new MultiplayerClientOptions(server), _definitions);
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = SpectatorViewPresentation.Joining;
        begin(_spectatorWatch);
        return true;
    }

    /// <summary>Drains what the watch has to say, on the game thread.</summary>
    private void PumpSpectatorNotices()
    {
        while (_spectatorWatch?.TryDequeueNotice(out var notice) == true) Apply(notice);
    }

    private void Apply(SpectatorNotice notice)
    {
        switch (notice)
        {
            case SpectatorNotice.Joined joined:
                _spectatorView = joined.Membership.Match;
                RememberSpectatorMembership(joined.Membership);
                _online.Stage = MultiplayerStage.Connect;
                _online.Status = string.Empty;
                _screens.Show(ClientScreen.Spectate);
                return;
            case SpectatorNotice.Progressed progressed:
                _spectatorView = progressed.View;
                _spectatorHasState = progressed.HasState;
                _spectatorShownTurn = progressed.ShownTurn;
                _spectatorComplete = progressed.IsComplete;
                _spectatorConnected = true;
                if (progressed.State is { } state)
                {
                    _spectatorState = state;
                    // The marker caches key on the state they last drew; a new one starts afresh.
                    _spectatorGangSight.Clear();
                    _spectatorGangMarkers.Clear();
                    _spectatorFollowedSeat = NextSeatInPlay(state, _spectatorFollowedSeat, 0);
                }
                if (progressed.IsComplete && _spectatorRecovery is { Completed: false } finished)
                    UpdateSpectatorRecovery(finished with { CleanExit = true, Completed = true });
                return;
            case SpectatorNotice.ConnectionChanged connection:
                _spectatorConnected = connection.IsConnected;
                return;
            case SpectatorNotice.Ended ended:
                _diagnostics?.Write("multiplayer.spectator.ended", new Dictionary<string, string?>
                {
                    ["reason"] = ended.Reason,
                    ["error"] = RuntimeDiagnostics.ExceptionType(ended.Error),
                    ["apiReason"] = ApiFailure(ended.Error)?.Reason,
                    ["membershipGone"] = ended.MembershipGone.ToString(CultureInfo.InvariantCulture),
                });
                if (ended.MembershipGone) ForgetSpectatorRecovery();
                EndSpectating();
                OpenOnline();
                _online.Status = string.Empty;
                _online.ConnectionError = $"WATCHING STOPPED: {ended.Reason.ToUpperInvariant()}";
                _online.ConnectionErrorCopyStatus = string.Empty;
                return;
        }
    }

    /// <summary>Stops watching, ends the token on the server and forgets it here.</summary>
    private void LeaveSpectating()
    {
        if (_spectatorWatch is { } watch)
        {
            _pendingLeave = watch.LeaveAsync();
            Forget(_pendingLeave, "multiplayer.spectator.leave.failed");
        }
        ForgetSpectatorRecovery();
        EndSpectating();
        OpenOnline();
        _online.Status = "YOU STOPPED WATCHING";
    }

    /// <summary>Lets go of the watch and everything the view drew from it.</summary>
    private void EndSpectating()
    {
        Forget(_spectatorWatch?.StopAsync(), "multiplayer.spectator.stop.failed");
        _spectatorWatch = null;
        _spectatorRecovery = null;
        _spectatorServer = null;
        ForgetSpectatorView();
        _online.Stage = MultiplayerStage.Connect;
    }

    private void ForgetSpectatorView()
    {
        _spectatorView = null;
        _spectatorState = null;
        _spectatorHasState = false;
        _spectatorShownTurn = null;
        _spectatorComplete = false;
        _spectatorConnected = true;
        _spectatorGangSight.Clear();
        _spectatorGangMarkers.Clear();
    }

    private void RememberSpectatorMembership(SpectatorMembership membership)
    {
        if (_spectatorServer is not { } server) return;
        var recovery = new MultiplayerRecovery(
            MultiplayerRecovery.CurrentFormatVersion,
            server.ToString(),
            membership.Match.Id,
            membership.Spectator.Id,
            membership.Token,
            _spectatorJoinCode,
            membership.Spectator.DisplayName,
            IsHost: false,
            CleanExit: false,
            Completed: false,
            _spectatorPassword,
            SessionVersion: membership.Match.SessionVersion,
            SessionName: membership.Match.Settings.Name,
            LastUpdatedAt: DateTimeOffset.UtcNow,
            Spectating: true);
        _spectatorRecovery = recovery;
        _multiplayerRecoveries.RemoveAll(item => SameMembership(item, recovery));
        _multiplayerRecoveries.Insert(0, recovery);
        SaveOnlineRecoveries(seats: false);
    }

    private void UpdateSpectatorRecovery(MultiplayerRecovery recovery)
    {
        var index = _multiplayerRecoveries.FindIndex(item => SameMembership(item, recovery));
        if (index >= 0) _multiplayerRecoveries[index] = recovery;
        else _multiplayerRecoveries.Insert(0, recovery);
        _spectatorRecovery = recovery;
        SaveOnlineRecoveries(seats: false);
    }

    private void ForgetSpectatorRecovery()
    {
        if (_spectatorRecovery is not { } recovery) return;
        _multiplayerRecoveries.RemoveAll(item => SameMembership(item, recovery));
        _spectatorRecovery = null;
        SaveOnlineRecoveries(seats: false);
    }

    /// <summary>
    /// The first seat in play from <paramref name="seat"/>, moving by <paramref name="direction"/>
    /// (0 keeps the seat when it is still in play, and otherwise looks forwards).
    /// </summary>
    private static int NextSeatInPlay(MatchState state, int seat, int direction)
    {
        var step = direction < 0 ? -1 : 1;
        var start = direction == 0 ? seat : seat + step;
        for (var offset = 0; offset < MatchLimits.PlayerCount; offset++)
        {
            var candidate = Mod(start + offset * step, MatchLimits.PlayerCount);
            if (SeatInPlay(state, candidate)) return candidate;
        }
        return seat;
    }

    private static bool SeatInPlay(MatchState state, int seat) =>
        state.Players.Any(player => player.Id.Value == seat && player.Status != CorePlayerStatus.Eliminated);

    private void FollowSeat(int direction)
    {
        if (_spectatorState is not { } state) return;
        var next = NextSeatInPlay(state, _spectatorFollowedSeat, direction);
        if (next == _spectatorFollowedSeat) return;
        _spectatorFollowedSeat = next;
        _spectatorGangSight.Clear();
        _spectatorGangMarkers.Clear();
    }

    private void UpdateSpectate(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Tab)) FollowSeat(1);
        if (_spectatorState is null) return;
        var dx = (Pressed(keyboard, Keys.Right) ? 1 : 0) - (Pressed(keyboard, Keys.Left) ? 1 : 0);
        var dy = (Pressed(keyboard, Keys.Down) ? 1 : 0) - (Pressed(keyboard, Keys.Up) ? 1 : 0);
        _spectatorCursor = MovedSector(_spectatorCursor, dx, dy);
    }

    private void HandleSpectateClick(Point point)
    {
        if (_spectatorState is not { } state)
        {
            if (SpectatorViewLayout.WaitingLeave.Contains(point)) LeaveSpectating();
            return;
        }
        if (SpectatorViewLayout.Leave.Contains(point))
        {
            LeaveSpectating();
            return;
        }
        if (SpectatorViewLayout.FollowPrevious.Contains(point))
        {
            FollowSeat(-1);
            return;
        }
        if (SpectatorViewLayout.FollowNext.Contains(point))
        {
            FollowSeat(1);
            return;
        }
        for (var seat = 0; seat < MatchLimits.PlayerCount; seat++)
        {
            if (!OverlordBarLayout.PortraitHit(seat).Contains(point) || !SeatInPlay(state, seat)) continue;
            if (seat != _spectatorFollowedSeat)
            {
                _spectatorFollowedSeat = seat;
                _spectatorGangSight.Clear();
                _spectatorGangMarkers.Clear();
            }
            return;
        }
        for (var sector = 0; sector < state.Sectors.Count; sector++)
        {
            if (!CityMapLayout.Destination(sector).Contains(point)) continue;
            _spectatorCursor = sector;
            return;
        }
    }

    private void DrawSpectate(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (_spectatorState is not { } state)
        {
            DrawSpectatorWaiting(batch, pixel, font);
            return;
        }
        if (_cityBackground is not null)
            batch.Draw(_cityBackground, new Rectangle(0, 0, 640, 460), Color.White);
        var followed = state.Players.FirstOrDefault(player => player.Id.Value == _spectatorFollowedSeat)
            ?? state.Players[0];
        _overlordMarkerClock.OtherView();
        DrawOverlordBar(batch, pixel, state, followed.Id, seatsSeen: null);
        DrawPreparedCityMap(batch, pixel, state, followed.Id,
            CityMapLayout.Bounds with { X = 0, Y = 0 }, CityMapLayout.Bounds.Location,
            new CityMapCaches(_spectatorGangSight, _spectatorGangMarkers, NoSiteSearchSelections));
        if (_uiKeyedSprites is not null)
            batch.Draw(_uiKeyedSprites, CityMapLayout.Destination(_spectatorCursor),
                CityMapLayout.SelectionFrameSource(SelectionFrameShown()), Color.White);
        else
            DrawBorder(batch, pixel, CityMapLayout.Destination(_spectatorCursor), Color.Gold, 2);
        foreach (var label in CityMapLayout.GridLabels())
            DrawGridLabel(batch, font, label);
        DrawStatusConsoleValues(batch, pixel, font, state, followed, _spectatorCursor,
            complete: state.Outcome is not null);
        DrawSpectatorPanel(batch, pixel, font, followed);
        font.Draw(batch, SpectatorViewLayout.Footer, new Vector2(18, 439), new Color(180, 190, 190), 1);
    }

    private SpectatorViewPresentation.Lines SpectatorLines() =>
        _spectatorView is { } view
            ? SpectatorViewPresentation.Describe(
                view, _spectatorHasState, _spectatorShownTurn, _spectatorComplete, _spectatorConnected)
            : new SpectatorViewPresentation.Lines(
                string.Empty, string.Empty,
                _spectatorConnected ? SpectatorViewPresentation.Joining : "CONNECTION LOST  RETRYING");

    /// <summary>The panel over the command buttons: what is watched, how late, for whom, and the way out.</summary>
    private void DrawSpectatorPanel(
        SpriteBatch batch, Texture2D pixel, PixelFont font, MatchPlayerState followed)
    {
        var panel = SpectatorViewLayout.Panel;
        batch.Draw(pixel, panel, new Color(8, 14, 13, 245));
        DrawBorder(batch, pixel, panel, OnlineOutline, 1);
        var lines = SpectatorLines();
        var left = SpectatorViewLayout.TextLeft;
        var columns = SpectatorViewLayout.LineColumns;
        font.Draw(batch, "WATCHING", new Vector2(left, SpectatorViewLayout.LineY(0)), Color.Gold, 1);
        font.Draw(batch, Clip(_spectatorView?.Settings.Name ?? string.Empty, columns),
            new Vector2(left, SpectatorViewLayout.LineY(1)), Color.White, 1);
        font.Draw(batch, lines.Progress, new Vector2(left, SpectatorViewLayout.LineY(3)), Color.White, 1);
        font.Draw(batch, lines.Delay, new Vector2(left, SpectatorViewLayout.LineY(4)), Color.Lime, 1);
        var standing = new List<string>();
        LobbyChatPresentation.Wrap(lines.Standing, columns, standing);
        for (var row = 0; row < standing.Count && 5 + row < SpectatorViewLayout.TextLines; row++)
            font.Draw(batch, standing[row],
                new Vector2(left, SpectatorViewLayout.LineY(5 + row) + 4), OnlineSecondaryText, 1);

        font.Draw(batch, "MAP DRAWN FOR",
            new Vector2(left, SpectatorViewLayout.FollowCaptionY), OnlineMutedText, 1);
        DrawHorizontalArrow(batch, pixel, SpectatorViewLayout.FollowPrevious, left: true, Color.Gold);
        DrawHorizontalArrow(batch, pixel, SpectatorViewLayout.FollowNext, left: false, Color.Gold);
        var name = Clip(followed.Setup.Name.ToUpperInvariant(),
            SpectatorViewLayout.FollowName.Width / OriginalFontLayout.CellWidth);
        var seat = followed.Id.Value;
        DrawCentredIn(font, batch, name, SpectatorViewLayout.FollowName,
            seat >= 0 && seat < PlayerColors.Length ? PlayerColors[seat] : Color.White);
        DrawButton(batch, pixel, font, SpectatorViewLayout.Leave, "LEAVE", ButtonEmphasis.Secondary);
    }

    /// <summary>Before there is a city to draw: the online frame, saying what the view waits for.</summary>
    private void DrawSpectatorWaiting(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        DrawOnlineFrame(batch, pixel, font, "WATCHING");
        var lines = SpectatorLines();
        var name = _spectatorView?.Settings.Name ?? string.Empty;
        DrawCentered(font, batch, name, 140, Color.White, 2);
        DrawCentered(font, batch, lines.Progress, 186, Color.White, 1);
        DrawCentered(font, batch, lines.Delay, 204, Color.Lime, 1);
        DrawCentered(font, batch, lines.Standing, 230, OnlineSecondaryText, 1);
        DrawButton(batch, pixel, font, SpectatorViewLayout.WaitingLeave, "LEAVE", ButtonEmphasis.Secondary);
    }

    private static string Clip(string text, int columns) =>
        text.Length <= columns ? text : text[..Math.Max(0, columns)];

    private static void DrawCentredIn(
        PixelFont font, SpriteBatch batch, string text, Rectangle bounds, Color colour) =>
        font.Draw(batch, text,
            new Vector2(bounds.X + (bounds.Width - text.Length * OriginalFontLayout.CellWidth) / 2,
                bounds.Y + (bounds.Height - OriginalFontLayout.GlyphHeight) / 2),
            colour, 1);

    // The list of who is watching, over the lobby or the match.

    private void OpenSpectatorList()
    {
        if (_lobby is not { Handle: not null } lobby) return;
        _online.SpectatorListOpen = true;
        _online.SpectatorListLoading = true;
        _online.SpectatorListStatus = string.Empty;
        _online.SpectatorSelection = 0;
        lobby.ListSpectators();
    }

    private void CloseSpectatorList()
    {
        _online.SpectatorListOpen = false;
        _online.SpectatorListLoading = false;
        _online.SpectatorListStatus = string.Empty;
    }

    private bool CanRemoveSpectator =>
        _online.IsHost && !_online.SpectatorListLoading && _online.Spectators.Count > 0;

    private void RemoveSelectedSpectator()
    {
        if (!CanRemoveSpectator || _lobby is not { } lobby) return;
        var spectator = _online.Spectators[
            Math.Clamp(_online.SpectatorSelection, 0, _online.Spectators.Count - 1)];
        _online.SpectatorListLoading = true;
        _online.SpectatorListStatus = string.Empty;
        lobby.RemoveSpectator(spectator.Id);
    }

    private void UpdateSpectatorList(KeyboardState keyboard)
    {
        var count = _online.Spectators.Count;
        if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Enter)) CloseSpectatorList();
        else if (count > 0 && Pressed(keyboard, Keys.Up))
            _online.SpectatorSelection = Mod(_online.SpectatorSelection - 1, count);
        else if (count > 0 && Pressed(keyboard, Keys.Down))
            _online.SpectatorSelection = Mod(_online.SpectatorSelection + 1, count);
        else if (Pressed(keyboard, Keys.Delete)) RemoveSelectedSpectator();
    }

    private void HandleSpectatorListClick(Point point)
    {
        var window = ListScrollWindow.Of(
            _online.Spectators.Count, _online.SpectatorSelection, SpectatorListLayout.Rows);
        if (RowClicked(point, SpectatorListLayout.Row, window) is { } row)
            _online.SpectatorSelection = row;
        else if (SpectatorListLayout.Remove.Contains(point)) RemoveSelectedSpectator();
        else if (SpectatorListLayout.Close.Contains(point)
                 || !SpectatorListLayout.Panel.Contains(point)) CloseSpectatorList();
    }

    private void DrawSpectatorList(SpriteBatch batch, Texture2D pixel, PixelFont font)
    {
        if (!_online.SpectatorListOpen) return;
        var panel = SpectatorListLayout.Panel;
        batch.Draw(pixel, panel, new Color(6, 14, 13, 250));
        DrawBorder(batch, pixel, panel, Color.Gold, 1);
        var spectators = _online.Spectators;
        var window = ListScrollWindow.Of(
            spectators.Count, _online.SpectatorSelection, SpectatorListLayout.Rows);
        font.Draw(batch, "WATCHING THIS MATCH",
            new Vector2(SpectatorListLayout.Row(0).X, SpectatorListLayout.CaptionY), Color.Gold, 1);
        DrawRightAligned(font, batch, window.Tally("SPECTATORS"),
            SpectatorListLayout.Row(0).Right, SpectatorListLayout.CaptionY, OnlineSecondaryText);
        for (var row = 0; row < window.VisibleRows; row++)
        {
            var index = window.IndexAt(row);
            var bounds = SpectatorListLayout.Row(row);
            DrawListRow(batch, pixel, bounds, index == _online.SpectatorSelection);
            font.Draw(batch, Clip(spectators[index].DisplayName, 40),
                new Vector2(bounds.X + 6, bounds.Y + 7), Color.White, 1);
        }
        var status = _online.SpectatorListStatus.Length > 0
            ? _online.SpectatorListStatus
            : _online.SpectatorListLoading
                ? "ASKING THE SERVER"
                : spectators.Count == 0
                    ? "NOBODY IS WATCHING"
                    : _online.IsHost ? "REMOVE ENDS THEIR VIEW" : "ONLY THE HOST CAN REMOVE A SPECTATOR";
        DrawCentered(font, batch, Clip(status, 56), SpectatorListLayout.StatusY, OnlineSecondaryText, 1);
        DrawButton(batch, pixel, font, SpectatorListLayout.Remove, "REMOVE",
            CanRemoveSpectator ? ButtonEmphasis.Secondary : ButtonEmphasis.Disabled);
        DrawButton(batch, pixel, font, SpectatorListLayout.Close, "CLOSE", ButtonEmphasis.Primary);
    }

    /// <summary>A spectator arrived or left during the match: said on the message line, and listed.</summary>
    private void AnnounceSpectator(SpectatorView? arrived, string? departedId, bool removed)
    {
        string line;
        if (arrived is not null)
        {
            _online.SpectatorNames[arrived.Id] = arrived.DisplayName;
            line = SpectatorAnnouncement.Joined(arrived.DisplayName);
        }
        else
        {
            var name = departedId is not null && _online.SpectatorNames.TryGetValue(departedId, out var known)
                ? known
                : SpectatorAnnouncement.UnknownName;
            line = SpectatorAnnouncement.Left(name, removed);
        }
        _message = line.ToUpperInvariant();
        if (_online.SpectatorListOpen && _lobby?.Handle is not null) _lobby.ListSpectators();
    }
}
