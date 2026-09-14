using Microsoft.Xna.Framework;
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
        _online.Status = "FINDING PUBLIC SESSIONS";
        _lobby!.Browse();
    }

    private void CloseOnlineDiscovery()
    {
        Forget(_lobby?.StopAsync(), "multiplayer.discovery.stop.failed");
        _lobby = null;
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

    private void CycleDiscoveryFilter(int filter)
    {
        if (filter == 0) _online.DiscoveryStatusFilter = (_online.DiscoveryStatusFilter + 1) % 3;
        else if (filter == 1)
            _online.DiscoveryScenarioFilter = (_online.DiscoveryScenarioFilter + 2)
                % (Enum.GetValues<ScenarioId>().Length + 1) - 1;
        else
            _online.DiscoveryAiFilter = (_online.DiscoveryAiFilter + 2)
                % (Enum.GetValues<AiDifficulty>().Length + 1) - 1;
        _online.DiscoverySelection = 0;
    }

    private void JoinSelectedOnlineListing()
    {
        var listings = FilteredOnlineListings();
        if (listings.Count == 0 || !RequireUsableName() || _lobby is null) return;
        var listing = listings[Math.Clamp(_online.DiscoverySelection, 0, listings.Count - 1)];
        if (listing.Status == MatchStatus.Lobby)
        {
            _online.Stage = MultiplayerStage.Busy;
            _online.Status = "JOINING LOBBY";
            _lobby.Join(new JoinMatchRequest(
                listing.JoinCode, _online.DisplayName.Value.Trim(), OptionalPassword()));
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
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "JOINING GAME";
        _lobby.JoinRunning(new JoinRunningMatchRequest(
            listing.Id, _online.DisplayName.Value.Trim(), OptionalPassword(), seat.Slot));
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
