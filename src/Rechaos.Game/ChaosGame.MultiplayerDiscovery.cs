using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private IReadOnlyList<MultiplayerRecovery> RecoverableOnlineSessions =>
        _multiplayerRecoveries.Where(recovery => recovery.CanReconnect).ToArray();

    private void OpenOnlineHistory()
    {
        if (RecoverableOnlineSessions.Count == 0) return;
        _online.RecoverySelection = Math.Min(
            _online.RecoverySelection, RecoverableOnlineSessions.Count - 1);
        _online.Stage = MultiplayerStage.History;
    }

    private void CloseOnlineHistory() => _online.Stage = MultiplayerStage.Connect;

    private void OpenOnlineDiscovery()
    {
        if (!TryBeginLobby()) return;
        _online.Stage = MultiplayerStage.Discover;
        CloseDiscoveryFilterMenu();
        _online.Status = "FINDING PUBLIC SESSIONS";
        _lobby!.Browse();
    }

    private void CloseOnlineDiscovery()
    {
        Forget(_lobby?.StopAsync(), "multiplayer.discovery.stop.failed");
        _lobby = null;
        CloseDiscoveryFilterMenu();
        _online.Stage = MultiplayerStage.Connect;
    }

    private IReadOnlyList<LobbyListing> FilteredOnlineListings() =>
        _online.Listings.Where(MatchesDiscoveryFilters).ToArray();

    private bool MatchesDiscoveryFilters(LobbyListing listing)
    {
        if (_online.DiscoveryStatusFilter == 1 && listing.Status != MatchStatus.Lobby) return false;
        if (_online.DiscoveryStatusFilter == 2 && listing.Status != MatchStatus.Running) return false;
        MultiplayerGameSettings settings;
        try { settings = MultiplayerGameSettings.FromWire(listing.Settings.GameSettings); }
        catch (MultiplayerProtocolException) { return false; }
        return (_online.DiscoveryScenarioFilter < 0
                || (int)settings.Scenario == _online.DiscoveryScenarioFilter)
            && (_online.DiscoveryAiFilter < 0
                || (int)settings.AiMentality == _online.DiscoveryAiFilter);
    }

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
        if (listings.Count == 0 || !RequireUsableName() || _lobby is null) return;
        var listing = listings[Math.Clamp(_online.DiscoverySelection, 0, listings.Count - 1)];
        if (listing.Status == MatchStatus.Lobby)
        {
            var password = OptionalPassword();
            _online.PasswordShown = password ?? string.Empty;
            _online.Stage = MultiplayerStage.Busy;
            _online.Status = "JOINING LOBBY";
            _lobby.Join(new JoinMatchRequest(
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
        if (listing is null || listing.AvailableSeatSummaries.Count == 0 || _lobby is null) return;
        var seat = listing.AvailableSeatSummaries[
            Math.Clamp(_online.LateJoinSeatSelection, 0, listing.AvailableSeatSummaries.Count - 1)];
        var password = OptionalPassword();
        _online.PasswordShown = password ?? string.Empty;
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "JOINING GAME";
        _online.JoinedInProgress = true;
        _lobby.JoinRunning(new JoinRunningMatchRequest(
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
        for (var row = 0; row < Math.Min(6, seats.Count); row++)
            if (OnlineConnectLayout.HistoryRow(row).Contains(point))
                _online.LateJoinSeatSelection = row;
        if (OnlineConnectLayout.HistoryRejoin.Contains(point)) ConfirmLateJoin();
        else if (OnlineConnectLayout.HistoryBack.Contains(point))
            _online.Stage = MultiplayerStage.Discover;
    }
}
