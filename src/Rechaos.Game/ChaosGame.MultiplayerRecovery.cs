using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>The memberships kept for a reconnect: written when a seat is taken, stamped as it plays.</summary>
public sealed partial class ChaosGame
{
    private void RememberOnlineMembership(MembershipView membership)
    {
        // The session's own address, not the one the connect form is showing. They differ whenever
        // the form was edited after the session was built, and a record naming the wrong server is
        // a seat every later reconnect gets a 401 or 404 for — after which reconciliation deletes it.
        var server = _lobby?.BaseAddress;
        if (server is null && !TrySelectedServer(out server)) return;
        var recovery = new MultiplayerRecovery(
            MultiplayerRecovery.CurrentFormatVersion,
            server.ToString(),
            membership.Match.Id,
            membership.Player.Id,
            membership.Token,
            membership.JoinCode,
            membership.Player.DisplayName,
            membership.Player.IsHost,
            CleanExit: false,
            Completed: false,
            _online.PasswordShown,
            SessionVersion: membership.Match.SessionVersion,
            SessionName: membership.Match.Settings.Name,
            LastUpdatedAt: DateTimeOffset.UtcNow);
        _activeMultiplayerRecovery = recovery;
        _multiplayerRecoveries.RemoveAll(item => SameMembership(item, recovery));
        _multiplayerRecoveries.Insert(0, recovery);
        SaveOnlineRecoveries();
    }

    /// <summary>
    /// Stamps the seat with the moment its turn data was last stored.
    /// </summary>
    /// <remarks>
    /// What the list of unfinished sessions is read by, next to the match's name: two matches a
    /// player still has a seat in are told apart by which one they were last playing. Called where
    /// authoritative state is adopted rather than where a turn is sent, because that is the point
    /// the client has the turn's data to keep; a clean exit and a retirement both carry the stamp
    /// forward untouched, since neither advances the match.
    /// </remarks>
    private void TouchOnlineRecovery()
    {
        if (_activeMultiplayerRecovery is not { Completed: false } recovery) return;
        UpdateOnlineRecovery(recovery with { LastUpdatedAt = DateTimeOffset.UtcNow }, durable: false);
    }

    private void CompleteOnlineRecovery()
    {
        if (_activeMultiplayerRecovery is not { } recovery) return;
        UpdateOnlineRecovery(recovery with { CleanExit = true, Completed = true });
    }

    /// <summary>
    /// Writes a membership back to the history file.
    /// </summary>
    /// <param name="recovery">The membership as it now stands.</param>
    /// <param name="durable">
    /// Whether the write has to survive losing power. True for the marks that decide whether a
    /// player is offered a reconnect at all — the clean exit and the completion — and false for the
    /// routine stamp every resolved turn makes, which costs an fsync on the game thread in the
    /// frame the new turn appears and whose loss costs only the order of a list.
    /// </param>
    private void UpdateOnlineRecovery(MultiplayerRecovery recovery, bool durable = true)
    {
        var index = _multiplayerRecoveries.FindIndex(item => SameMembership(item, recovery));
        if (index >= 0) _multiplayerRecoveries[index] = recovery;
        else _multiplayerRecoveries.Insert(0, recovery);
        _activeMultiplayerRecovery = recovery;
        SaveOnlineRecoveries(durable);
    }

    /// <summary>
    /// Writes the history back, and marks the views over it stale.
    /// </summary>
    /// <remarks>
    /// Every path that changes <see cref="_multiplayerRecoveries"/> ends here, which is why the
    /// version lives in this one place rather than beside each mutation: a caller cannot add a
    /// membership and forget to say so.
    /// </remarks>
    private void SaveOnlineRecoveries(bool durable = true)
    {
        _multiplayerRecoveryVersion++;
        MultiplayerRecoveryStore.TrySaveAll(_multiplayerRecoveryPath,
            _multiplayerRecoveries.Where(recovery => !recovery.Spectating), durable);
        MultiplayerRecoveryStore.TrySaveAll(SpectatorRecoveryPath,
            _multiplayerRecoveries.Where(recovery => recovery.Spectating), durable);
    }

    private static bool SameMembership(MultiplayerRecovery left, MultiplayerRecovery right) =>
        string.Equals(left.Server, right.Server, StringComparison.OrdinalIgnoreCase)
        && left.MatchId == right.MatchId
        && left.PlayerId == right.PlayerId;
}
