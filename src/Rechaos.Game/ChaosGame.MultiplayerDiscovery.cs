using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    /// <summary>
    /// The memberships the player can still take a seat in, cached until the list behind them moves.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A membership from another session version is left off: this build cannot play it, so it is
    /// not offered. It stays in the store, and a build of that version lists it again.
    /// </para>
    /// <para>
    /// Both screens that read this read it several times a frame — for the row count, for the
    /// scroll window, for the selected row, and once per row drawn — and it used to be a fresh
    /// filter and array allocation each time. It changes only when a membership is written back,
    /// which is a handful of times a match.
    /// </para>
    /// </remarks>
    private IReadOnlyList<MultiplayerRecovery> RecoverableOnlineSessions
    {
        get
        {
            if (_recoverableSessions is null || _recoverableSessionsVersion != _multiplayerRecoveryVersion)
            {
                _recoverableSessions =
                    _multiplayerRecoveries.Where(recovery => recovery.CanResume).ToArray();
                _recoverableSessionsVersion = _multiplayerRecoveryVersion;
            }
            return _recoverableSessions;
        }
    }

    private IReadOnlyList<MultiplayerRecovery>? _recoverableSessions;
    private int _recoverableSessionsVersion = -1;

    /// <summary>
    /// The row the browser is on, or null when there is nothing to browse.
    /// </summary>
    private MultiplayerRecovery? SelectedOnlineRecovery
    {
        get
        {
            var sessions = RecoverableOnlineSessions;
            return sessions.Count == 0
                ? null
                : sessions[Math.Clamp(_online.RecoverySelection, 0, sessions.Count - 1)];
        }
    }

    private void OpenOnlineHistory()
    {
        if (RecoverableOnlineSessions.Count == 0) return;
        _online.RecoverySelection = Math.Min(
            _online.RecoverySelection, RecoverableOnlineSessions.Count - 1);
        _online.Stage = MultiplayerStage.History;
    }

    private void CloseOnlineHistory() => _online.Stage = MultiplayerStage.Connect;

    /// <summary>
    /// Whether a control that makes a lobby call is on offer, given what it acts on.
    /// </summary>
    /// <remarks>
    /// The screens' reading of <see cref="OnlineConnectPolicy.CanCallLobby"/>, where the rule and
    /// its reasons live. Drawing and clicking both come through here, so no button is drawn live
    /// that would drop the press, and none is drawn disabled that would still act on it.
    /// </remarks>
    private bool CanCallOnlineLobby(int subjects = 1) => OnlineConnectPolicy.CanCallLobby(
        _lobby is not null, _lobby?.IsBusy == true, subjects);

    private void OpenOnlineDiscovery()
    {
        if (!TryBeginLobby()) return;
        _online.Stage = MultiplayerStage.Discover;
        CloseDiscoveryFilterMenu();
        _online.Status = "FINDING PUBLIC SESSIONS";
        _lobby!.Browse();
    }

    /// <summary>
    /// Asks the server for the list again.
    /// </summary>
    /// <remarks>
    /// The list arrives once, when the screen opens, and nothing refreshes it: a player watching for
    /// a friend's lobby to appear had to leave the screen and come back. The notice that carries the
    /// answer puts the selection back at the top and reports an empty result, so there is nothing to
    /// do here but ask and say that it was asked.
    /// </remarks>
    private void RefreshOnlineDiscovery()
    {
        if (_lobby is not { } lobby || !CanCallOnlineLobby()) return;
        CloseDiscoveryFilterMenu();
        _online.Status = "FINDING PUBLIC SESSIONS";
        lobby.Browse();
    }

    private void CloseOnlineDiscovery()
    {
        Forget(_lobby?.StopAsync(), "multiplayer.discovery.stop.failed");
        _lobby = null;
        CloseDiscoveryFilterMenu();
        _online.Stage = MultiplayerStage.Connect;
    }

    /// <summary>
    /// Reads each listed session's settings blob once, as the list arrives.
    /// </summary>
    /// <remarks>
    /// The discovery screen filters and draws its rows every frame, and both want the scenario and
    /// the mentality inside the blob. Parsing it here keeps that off the frame and gives the two a
    /// single answer, including for a blob this build cannot read: it becomes a listing with no
    /// settings rather than one that quietly disappears.
    /// </remarks>
    private static IReadOnlyList<DiscoveredListing> Describe(IReadOnlyList<LobbyListing> listings)
    {
        var described = new DiscoveredListing[listings.Count];
        for (var index = 0; index < listings.Count; index++)
        {
            var listing = listings[index];
            MultiplayerGameSettings? settings;
            try { settings = MultiplayerGameSettings.FromWire(listing.Settings.GameSettings); }
            catch (MultiplayerProtocolException) { settings = null; }
            described[index] = new DiscoveredListing(listing, settings);
        }
        return described;
    }

    /// <summary>
    /// The listings the current filters admit, cached until the listings or the filters move.
    /// </summary>
    /// <remarks>
    /// Read the same way as the recovery list above: several times a frame, for a result that
    /// changes only when a refresh lands or the player moves a filter.
    /// </remarks>
    private IReadOnlyList<DiscoveredListing> FilteredOnlineListings()
    {
        var filters = HashCode.Combine(
            _online.Listings,
            _online.DiscoveryStatusFilter,
            _online.DiscoveryScenarioFilter,
            _online.DiscoveryAiFilter);
        if (_filteredListings is null || _filteredListingsKey != filters)
        {
            _filteredListings = _online.Listings.Where(MatchesDiscoveryFilters).ToArray();
            _filteredListingsKey = filters;
        }
        return _filteredListings;
    }

    private IReadOnlyList<DiscoveredListing>? _filteredListings;
    private int _filteredListingsKey;

    private bool MatchesDiscoveryFilters(DiscoveredListing entry) => DiscoveryFilters.Matches(
        _online.DiscoveryStatusFilter,
        _online.DiscoveryScenarioFilter,
        _online.DiscoveryAiFilter,
        entry.Listing.Status,
        entry.Settings);

    private int DiscoveryFilterValue(int filter) => filter switch
    {
        DiscoveryFilters.Status => _online.DiscoveryStatusFilter,
        DiscoveryFilters.Scenario => _online.DiscoveryScenarioFilter,
        DiscoveryFilters.Ai => _online.DiscoveryAiFilter,
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };

    /// <summary>The dropdown row a filter currently sits on.</summary>
    private int ChosenDiscoveryFilterOption(int filter) =>
        DiscoveryFilters.OptionOf(filter, DiscoveryFilterValue(filter));

    private void OpenDiscoveryFilterMenu(int filter)
    {
        if (_online.OpenDiscoveryFilter == filter)
        {
            CloseDiscoveryFilterMenu();
            return;
        }
        _online.OpenDiscoveryFilter = filter;
        _online.DiscoveryFilterHighlight = ChosenDiscoveryFilterOption(filter);
    }

    private void CloseDiscoveryFilterMenu() => _online.OpenDiscoveryFilter = -1;

    private void ChooseDiscoveryFilterOption(int filter, int option)
    {
        var value = DiscoveryFilters.ValueOf(filter, option);
        if (filter == DiscoveryFilters.Status) _online.DiscoveryStatusFilter = value;
        else if (filter == DiscoveryFilters.Scenario) _online.DiscoveryScenarioFilter = value;
        else _online.DiscoveryAiFilter = value;
        _online.DiscoverySelection = 0;
        CloseDiscoveryFilterMenu();
    }

    private void HandleDiscoveryFilterMenuClick(Point point)
    {
        var filter = _online.OpenDiscoveryFilter;
        for (var option = 0; option < DiscoveryFilters.OptionCount(filter); option++)
            if (OnlineConnectLayout.DiscoveryFilterOption(filter, option).Contains(point))
            {
                ChooseDiscoveryFilterOption(filter, option);
                return;
            }
        CloseDiscoveryFilterMenu();
    }

    private void UpdateDiscoveryFilterMenu(KeyboardState keyboard)
    {
        var filter = _online.OpenDiscoveryFilter;
        var count = DiscoveryFilters.OptionCount(filter);
        if (Pressed(keyboard, Keys.Escape)) CloseDiscoveryFilterMenu();
        else if (Pressed(keyboard, Keys.Up))
            _online.DiscoveryFilterHighlight = Mod(_online.DiscoveryFilterHighlight - 1, count);
        else if (Pressed(keyboard, Keys.Down))
            _online.DiscoveryFilterHighlight = Mod(_online.DiscoveryFilterHighlight + 1, count);
        else if (Pressed(keyboard, Keys.Enter))
            ChooseDiscoveryFilterOption(filter, _online.DiscoveryFilterHighlight);
    }

    private void JoinSelectedOnlineListing()
    {
        var listings = FilteredOnlineListings();
        if (_lobby is not { } lobby || !CanCallOnlineLobby(listings.Count)
            || !RequireUsableName()) return;
        var listing = listings[
            Math.Clamp(_online.DiscoverySelection, 0, listings.Count - 1)].Listing;
        if (listing.Status == MatchStatus.Lobby)
        {
            var password = OptionalPassword();
            _online.PasswordShown = password ?? string.Empty;
            _online.Stage = MultiplayerStage.Busy;
            _online.Status = "JOINING LOBBY";
            lobby.Join(new JoinMatchRequest(
                listing.JoinCode, _online.DisplayName.Value.Trim(),
                _online.Portrait, password));
        }
        else if (listing.AvailableSeatSummaries.Count > 0)
        {
            _online.PendingLateJoin = listing;
            _online.LateJoinSeatSelection = 0;
            _online.Stage = MultiplayerStage.LateJoinSeat;
            _online.Status = "CHOOSE A COMPUTER EMPIRE";
        }
        else
        {
            _online.Stage = MultiplayerStage.Discover;
            _online.Status = "NO NEVER-HUMAN COMPUTER SEAT IS AVAILABLE";
        }
    }

    private void ConfirmLateJoin()
    {
        var listing = _online.PendingLateJoin;
        if (listing is null || _lobby is not { } lobby
            || !CanCallOnlineLobby(listing.AvailableSeatSummaries.Count)) return;
        var seat = listing.AvailableSeatSummaries[
            Math.Clamp(_online.LateJoinSeatSelection, 0, listing.AvailableSeatSummaries.Count - 1)];
        var password = OptionalPassword();
        _online.PasswordShown = password ?? string.Empty;
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "JOINING GAME";
        // Not JoinedInProgress yet: the seat is only taken when the server says it is, and the
        // membership that says so carries the match status the flag is read from. Setting it here
        // left it standing on a client whose join was refused, or who backed out of this screen.
        lobby.JoinRunning(new JoinRunningMatchRequest(
            listing.Id, _online.DisplayName.Value.Trim(),
            SeatPortrait(listing, seat.Slot), password, seat.Slot));
    }

    /// <summary>
    /// The face the empire being taken over already wears, rather than the one on the form.
    /// </summary>
    /// <remarks>
    /// A latecomer inherits a seat the match was generated with: every client built that overlord
    /// from the host's settings before this player existed, and the setup a city was generated from
    /// is hashed into every turn verdict. Bringing their own face would hand a different city to any
    /// client that still bootstraps this match from the roster. A blob this build cannot read is
    /// left to the server's default; the join then fails at bootstrap, where it can be explained.
    /// </remarks>
    private static int? SeatPortrait(LobbyListing listing, int slot)
    {
        try
        {
            return MultiplayerGameSettings.FromWire(listing.Settings.GameSettings).Portraits[slot];
        }
        catch (MultiplayerProtocolException)
        {
            return null;
        }
    }

    private void HandleLateJoinSeatClick(Point point)
    {
        var seats = _online.PendingLateJoin?.AvailableSeatSummaries ?? [];
        var window = ListScrollWindow.Of(
            seats.Count, _online.LateJoinSeatSelection, OnlineScreenLayout.ListRows);
        if (RowClicked(point, OnlineConnectLayout.HistoryRow, window) is { } seat)
            _online.LateJoinSeatSelection = seat;
        if (OnlineConnectLayout.HistoryRejoin.Contains(point)) ConfirmLateJoin();
        else if (OnlineConnectLayout.HistoryBack.Contains(point))
            _online.Stage = MultiplayerStage.Discover;
    }
}

/// <summary>
/// A listed public session and the settings it carries, or null settings when this build cannot
/// read the blob the host wrote.
/// </summary>
internal readonly record struct DiscoveredListing(
    LobbyListing Listing,
    MultiplayerGameSettings? Settings);
