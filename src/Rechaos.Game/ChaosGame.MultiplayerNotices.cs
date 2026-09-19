using System.Globalization;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

/// <summary>
/// What the lobby and match sessions tell the interface, and what the interface does about it.
/// </summary>
/// <remarks>
/// The sessions run their protocol on background tasks and answer through a queue; everything here
/// runs on the game thread, draining that queue once a frame. Kept apart from the online screens so
/// the two can be read for what they are: this is the match reacting to the server, and
/// <c>ChaosGame.Multiplayer.cs</c> is the player working the screens.
/// </remarks>
public sealed partial class ChaosGame
{
    /// <summary>
    /// Everything an online match needs of a frame, in the order it needs it.
    /// </summary>
    /// <remarks>
    /// Notices first, so a turn that resolved on the server is adopted before anything reads what
    /// the player is planning; then the draft, which is what preserves their work when the server's
    /// clock seals the turn before they finish it; then the countdown they are racing.
    /// </remarks>
    private void UpdateOnlineSession()
    {
        PumpOnlineNotices();
        SendOnlineDraft();
        UpdateOnlineDeadlineWarnings();
    }

    /// <summary>
    /// Drains what the sessions have to say, on the game thread.
    /// </summary>
    /// <remarks>
    /// Every notice carries a state the interface owns outright, so adopting one is an assignment
    /// rather than a lock: the sessions keep their own copies and never hand one over.
    /// </remarks>
    private void PumpOnlineNotices()
    {
        while (_lobby?.TryDequeueNotice(out var lobbyNotice) == true) Apply(lobbyNotice);
        while (_session?.TryDequeueNotice(out var notice) == true) Apply(notice);
        CheckOnlineResolutionWatchdog();
    }

    private void Apply(LobbyNotice notice)
    {
        switch (notice)
        {
            case LobbyNotice.Seated seated:
                _online.IsHost = seated.Membership.Player.IsHost;
                _online.JoinCodeShown = seated.Membership.JoinCode;
                _online.Match = seated.Membership.Match;
                AdoptLobbySettings(seated.Membership.Match);
                RememberOnlineMembership(seated.Membership);
                if (seated.Membership.Match.Status is MatchStatus.Finished or MatchStatus.Abandoned)
                {
                    CompleteOnlineRecovery();
                    EndOnlineMatch("THE SAVED ONLINE MATCH HAS ALREADY ENDED");
                    return;
                }
                if (seated.Membership.Match.Status is MatchStatus.Running or MatchStatus.Desynced)
                {
                    _online.JoinedInProgress = true;
                    _online.Stage = MultiplayerStage.Busy;
                    _online.Status = "RESTORING THE MATCH";
                    _screens.Show(ClientScreen.Online);
                    StartOnlineMatch(seated.Membership.Match);
                    return;
                }
                _online.Stage = MultiplayerStage.Lobby;
                _online.Status = _online.IsHost
                    ? "READ OUT THE JOIN CODE"
                    : "WAITING FOR THE HOST";
                _screens.Show(ClientScreen.Lobby);
                return;
            case LobbyNotice.Updated updated:
                _online.Match = updated.Match;
                // Not while the host is editing them: the poll that carries a settings change back
                // is the same poll that would type over the name being written next to it.
                if (!_online.IsHost) AdoptLobbySettings(updated.Match);
                if (_session is null && updated.Match.Status == MatchStatus.Running)
                    StartOnlineMatch(updated.Match);
                return;
            case LobbyNotice.Listed listed:
                _online.Listings = Describe(listed.Matches);
                _online.DiscoverySelection = 0;
                _online.Stage = MultiplayerStage.Discover;
                _online.Status = listed.Matches.Count == 0
                    ? "NO PUBLIC SESSIONS FOUND"
                    : string.Empty;
                return;
            case LobbyNotice.Failed failed:
                if (_online.Stage == MultiplayerStage.Busy) _online.Stage = MultiplayerStage.Connect;
                _online.Status = string.Empty;
                _online.ConnectionError = failed.Reason;
                _online.ConnectionErrorCopyStatus = string.Empty;
                return;
            default:
                return;
        }
    }

    private void Apply(MultiplayerNotice notice)
    {
        switch (notice)
        {
            case MultiplayerNotice.Resumed resumed:
                _online.Match = resumed.Match;
                // The same invariant round-trip parse the session uses on the same ISO-8601 string.
                // Left to the current culture it can fail where the session's own parse succeeded —
                // on one whose default calendar is not Gregorian — and drop the countdown.
                _online.DeadlineAt = resumed.Match.Turn is { DeadlineAt: { } deadlineText }
                    && DateTimeOffset.TryParse(
                        deadlineText, CultureInfo.InvariantCulture,
                        DateTimeStyles.RoundtripKind, out var parsed)
                        ? parsed
                        : null;
                ResetMatchPresentation(resumed.State);
                _online.AwaitedSlots = AwaitedSeats(resumed.Match.Players);
                _online.Status = string.Empty;
                if (AdoptOnlineState(resumed.State, resumed.Submission, resumed.Turn))
                {
                    _message = resumed.Submission.Ready
                        ? "ORDERS RESTORED  WAITING FOR THE OTHER PLAYERS"
                        : "MATCH RESTORED";
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.TurnResolved resolved:
                // Read before the adopt, which reopens planning on the turn that follows: the
                // player was cut off exactly when the seal arrived while the turn was still theirs
                // to plan. Saying so matters because the two outcomes look identical afterwards —
                // a new turn either way — and only one of them cost them the orders they were
                // still giving.
                var cutOff = _online.Stage == MultiplayerStage.Playing;
                if (AdoptOnlineState(resolved.State))
                {
                    _message = cutOff
                        ? resolved.IncludedOwnOrders
                            ? "TIME UP  THE TURN SEALED WITH THE ORDERS YOU HAD SENT"
                            : "TIME UP  YOUR SEAT GAVE NO ORDERS THIS TURN"
                        : "NEW TURN READY  PLAY AGAIN";
                    PlayGeneralSound(AudioRouting.OnlineTurnReadySound());
                    ShowTurnReportsOrCity();
                }
                return;
            case MultiplayerNotice.Resynced resynced:
                if (AdoptOnlineState(resynced.State))
                {
                    _message = string.Empty;
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.Desynced desynced:
                _online.Stage = MultiplayerStage.Desynced;
                CloseOnlinePlanning();
                _online.TurnSyncError = desynced.IsHostRepair
                    ? $"DESYNC TURN {desynced.Turn}  AUTOMATIC REPAIR IN PROGRESS"
                    : $"DESYNC TURN {desynced.Turn}  WAITING FOR HOST REPAIR";
                _message = desynced.IsHostRepair
                    ? $"DESYNC ON TURN {desynced.Turn}  SENDING A SNAPSHOT"
                    : $"DESYNC ON TURN {desynced.Turn}  WAITING FOR THE HOST";
                _diagnostics?.Write("multiplayer.desync", new Dictionary<string, string?>
                {
                    ["turn"] = desynced.Turn.ToString(CultureInfo.InvariantCulture),
                    ["details"] = desynced.Details,
                    ["hostRepair"] = desynced.IsHostRepair.ToString(),
                });
                if (desynced.IsHost && !desynced.IsHostRepair)
                {
                    ShowOnlineMatchFailure(
                        $"DESYNC ON TURN {desynced.Turn}. THIS HOST'S STATE IS NOT AN "
                        + $"ALLOWED REPAIR CANDIDATE. {desynced.Details}. RECONNECT FROM "
                        + "PREVIOUS SESSIONS TO REBUILD FROM THE AUTHORITATIVE HISTORY.");
                }
                return;
            case MultiplayerNotice.MatchUpdated updated:
                _online.Match = updated.Match;
                return;
            case MultiplayerNotice.TakeoverVoteChanged changed:
                var name = _online.Match?.Players
                    .FirstOrDefault(player => player.Id == changed.PlayerId)?.DisplayName
                    ?? "THE ABSENT PLAYER";
                _online.RecordTakeoverVote(new TakeoverVotePrompt(
                    changed.PlayerId, name, changed.Turn, changed.Votes));
                return;
            case MultiplayerNotice.TakeoverVoteClosed closed:
                _online.CloseTakeoverVote(closed.PlayerId);
                var ownSeat = string.Equals(
                    closed.PlayerId, _online.SelfPlayerId, StringComparison.Ordinal);
                _message = (closed.ComputerControl, ownSeat) switch
                {
                    (true, true) => "THE OTHER PLAYERS GAVE YOUR SEAT TO THE COMPUTER",
                    (true, false) => "PLAYERS APPROVED COMPUTER CONTROL",
                    (false, true) => "YOU ARE BACK IN THE MATCH  THE VOTE ON YOUR SEAT IS OFF",
                    (false, false) => "THE PLAYER RETURNED  TAKEOVER VOTE CANCELLED",
                };
                return;
            case MultiplayerNotice.DeadlineChanged deadline:
                _online.DeadlineAt = deadline.DeadlineAt;
                return;
            case MultiplayerNotice.ReadinessChanged readiness:
                if (readiness.Turn != _online.PlanningTurn) return;
                _online.ReadySlots = readiness.ReadySlots;
                _online.AwaitedSlots = readiness.AwaitedSlots;
                UpdateOnlineResolutionExpectation();
                return;
            case MultiplayerNotice.OrdersAccepted accepted:
                // A draft needs no announcement; the submission that ends a turn already said so.
                _online.TurnSyncError = string.Empty;
                if (accepted.Ready && accepted.Turn == _online.PlanningTurn)
                {
                    _online.ReadySubmissionPending = false;
                    _online.ReadySubmissionAcknowledged = true;
                    _message = "SERVER ACKNOWLEDGED FINISHED TURN";
                    _diagnostics?.Write("multiplayer.orders.acknowledged",
                        new Dictionary<string, string?>
                        {
                            ["turn"] = accepted.Turn.ToString(CultureInfo.InvariantCulture),
                        });
                    UpdateOnlineResolutionExpectation();
                }
                return;
            case MultiplayerNotice.OrdersRefused refused:
                // Not fatal. The turn may have sealed while the player was still planning it, which
                // costs them that turn and nothing else.
                _message = refused.Reason.ToUpperInvariant();
                _online.TurnSyncError = refused.Reason.ToUpperInvariant();
                if (refused.Turn == _online.PlanningTurn)
                {
                    _online.ReadySubmissionPending = false;
                    _online.ResolutionExpectedSince = null;
                }
                _diagnostics?.Write("multiplayer.orders.refused",
                    new Dictionary<string, string?>
                    {
                        ["turn"] = refused.Turn.ToString(CultureInfo.InvariantCulture),
                        ["reason"] = refused.Reason,
                    });
                if (_online.Stage == MultiplayerStage.WaitingForSeal)
                    _online.Status = refused.Reason.ToUpperInvariant();
                return;
            case MultiplayerNotice.ConnectionChanged connection:
                _online.IsConnected = connection.IsConnected;
                if (connection.IsConnected)
                {
                    _online.ReconnectLog.Clear();
                    _online.ReconnectAttempt = 0;
                    _message = string.Empty;
                    UpdateOnlineResolutionExpectation();
                }
                else if (connection.Detail is { } detail)
                {
                    _online.ResolutionExpectedSince = null;
                    _online.ReconnectAttempt = Math.Max(1, connection.Attempt);
                    var entry = $"ATTEMPT {Math.Max(1, connection.Attempt)}  {detail}";
                    _online.ReconnectLog.Add(entry.ToUpperInvariant());
                    while (_online.ReconnectLog.Count > 6) _online.ReconnectLog.RemoveAt(0);
                    _message = "CONNECTION LOST  AUTOMATICALLY RECONNECTING";
                }
                return;
            case MultiplayerNotice.MatchFinished:
                // The outcome usually arrives first, with the turn that produced it. This is the
                // server's own word for it, and the case where a match ended without one.
                if (_online.Stage != MultiplayerStage.Finished)
                {
                    _online.ConcludeMatch();
                    CloseOnlinePlanning();
                    _message = string.Empty;
                    if (_state?.Outcome is not null) _screens.Show(ClientScreen.Endgame);
                }
                CompleteOnlineRecovery();
                return;
            case MultiplayerNotice.MatchAbandoned:
                _online.ConcludeMatch();
                EndOnlineMatch("THE MATCH WAS ABANDONED");
                return;
            case MultiplayerNotice.Failed failed:
                _diagnostics?.Write("multiplayer.failed", new Dictionary<string, string?>
                {
                    ["reason"] = failed.Reason,
                    ["error"] = RuntimeDiagnostics.ExceptionType(failed.Error),
                });
                ShowOnlineMatchFailure(failed.Reason);
                return;
            default:
                return;
        }
    }
}
