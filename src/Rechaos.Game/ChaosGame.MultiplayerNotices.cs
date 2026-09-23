using System.Globalization;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
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
                AdoptOwnProfile(seated.Membership.Player);
                AdoptLobbySettings(seated.Membership.Match);
                RememberOnlineMembership(seated.Membership);
                if (seated.Membership.Match.Status is MatchStatus.Finished or MatchStatus.Abandoned)
                {
                    CompleteOnlineRecovery();
                    EndOnlineMatch("THE SAVED ONLINE MATCH HAS ALREADY ENDED");
                    return;
                }
                // A seat resumed while the server is still starting the match waits in the lobby,
                // where the poll brings the finished match to the start below.
                if (TryStartOnlineMatch(seated.Membership.Match, resumingSeat: true)) return;
                _online.Stage = MultiplayerStage.Lobby;
                // The lobby says what it is waiting for on its own standing line, so the status is
                // left clear for what happens next: a settings change, or a refusal of one.
                _online.Status = string.Empty;
                _screens.Show(ClientScreen.Lobby);
                return;
            case LobbyNotice.Updated updated:
                _online.Match = updated.Match;
                // A host who leaves the lobby abandons the match. The poll carries that status
                // back, and looking only for `Running` left the joiner's screen saying WAITING FOR
                // THE HOST TO START THE MATCH for as long as they cared to wait. `Seated` already
                // handles both terminal statuses; this is the same rule on the other notice.
                if (_session is null
                    && updated.Match.Status is MatchStatus.Abandoned or MatchStatus.Finished)
                {
                    CompleteOnlineRecovery();
                    EndOnlineMatch(updated.Match.Status == MatchStatus.Abandoned
                        ? "THE HOST CLOSED THE LOBBY"
                        : "THE MATCH IS OVER");
                    return;
                }
                // Not while the host is editing them: the poll that carries a settings change back
                // is the same poll that would type over the name being written next to it.
                if (!_online.IsHost) AdoptLobbySettings(updated.Match);
                if (updated.Match.Status == MatchStatus.Lobby) RememberOwnLobbyName(updated.Match);
                // A running or desynced match both count as started, as they do for a resumed seat;
                // one the server is still starting is left for a later poll.
                if (_session is null) TryStartOnlineMatch(updated.Match);
                return;
            case LobbyNotice.Listed listed:
                _online.Listings = Describe(listed.Matches);
                _online.DiscoverySelection = 0;
                // Only the browser is the answer's to move. A browse is a round trip, and the
                // player is free to spend it: taking a seat, or walking into the seat picker for a
                // running game. Forcing the stage here evicted them from whatever they had opened
                // in the meantime, and overwrote what that screen was telling them.
                if (_online.Stage != MultiplayerStage.Discover) return;
                _online.Status = listed.Matches.Count == 0
                    ? "NO PUBLIC SESSIONS FOUND"
                    : string.Empty;
                return;
            case LobbyNotice.Failed failed:
            {
                var lobbyApi = ApiFailure(failed.Error);
                _diagnostics?.Write("multiplayer.lobby.failed", new Dictionary<string, string?>
                {
                    ["reason"] = failed.Reason,
                    ["error"] = RuntimeDiagnostics.ExceptionType(failed.Error),
                    ["operation"] = failed.Operation,
                    ["httpStatus"] = lobbyApi is null
                        ? null
                        : ((int)lobbyApi.Status).ToString(CultureInfo.InvariantCulture),
                    ["apiReason"] = lobbyApi?.Reason,
                    ["requestId"] = lobbyApi?.RequestId,
                });
                // A refused name or face leaves the seat exactly as it was, so it is said on the lobby
                // rather than treated as the connection failing.
                if (failed.Operation == nameof(MultiplayerLobbySession.UpdateProfile))
                {
                    RejectLobbyProfile(failed);
                    return;
                }
                RememberOnlineFailure(failed.Error, failed.Operation, lastEventSequence: null);
                if (_online.Stage == MultiplayerStage.Busy)
                {
                    _online.Stage = MultiplayerStage.Connect;
                    // A call that failed seated nobody, so the seat's own flags go with it.
                    _online.JoinedInProgress = false;
                }
                // Let the session go while no seat is held, so the next attempt builds one from the
                // form. It was kept alive across a failure and reused by `TryBeginLobby`, so a
                // player who entered a code with Central selected, got "no match with that code",
                // then switched to Custom and typed their friend's address, went on dialling
                // Central until they backed all the way out to the title screen.
                if (_lobby is { Handle: null } stale)
                {
                    Forget(stale.StopAsync(), "multiplayer.lobby.stop.failed");
                    _lobby = null;
                }
                _online.Status = string.Empty;
                _online.ConnectionError = failed.Reason;
                _online.ConnectionErrorCopyStatus = string.Empty;
                return;
            }
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
                if (AdoptOnlineState(resolved.State, restored: resolved.Planning))
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
            case MultiplayerNotice.TakeoverVoteFailed failedVote:
                // The vote is still open and the buttons are still there; the player is told the
                // answer did not reach the server so they can give it again.
                _message = "THE VOTE DID NOT REACH THE SERVER  TRY AGAIN";
                _online.Status = _message;
                _diagnostics?.Write("multiplayer.takeover-vote.failed",
                    new Dictionary<string, string?>
                    {
                        ["player"] = failedVote.PlayerId,
                        ["choice"] = failedVote.Choice.ToString(),
                        ["reason"] = failedVote.Reason,
                    });
                return;
            case MultiplayerNotice.Resynced resynced:
                if (AdoptOnlineState(resynced.State, restored: resynced.Planning))
                {
                    _message = string.Empty;
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.Desynced desynced:
                _online.Stage = MultiplayerStage.Desynced;
                CloseOnlinePlanning();
                // A pause is not a silence. The resolution watchdog is waiting for a seal that
                // cannot come until the repair lands, and a legitimately slow repair on the
                // previous turn used to trip it.
                _online.ResolutionExpectedSince = null;
                _online.TurnSyncError = desynced.IsRepairing
                    ? $"DESYNC TURN {desynced.Turn}  AUTOMATIC REPAIR IN PROGRESS"
                    : $"DESYNC TURN {desynced.Turn}  WAITING FOR A REPAIR";
                _message = desynced.IsRepairing
                    ? $"DESYNC ON TURN {desynced.Turn}  SENDING A SNAPSHOT"
                    : $"DESYNC ON TURN {desynced.Turn}  WAITING FOR ANOTHER PLAYER";
                _diagnostics?.Write("multiplayer.desync", new Dictionary<string, string?>
                {
                    ["turn"] = desynced.Turn.ToString(CultureInfo.InvariantCulture),
                    ["details"] = desynced.Details,
                    ["repairing"] = desynced.IsRepairing.ToString(),
                });
                // A client that cannot post the repair WAITS. It used to be a terminal failure for
                // a host whose own state was not a candidate — the one case where the host is the
                // odd one out — and that was the exact moment the match needed the session alive:
                // the repair comes from whoever holds the majority's state, and the pause lifts for
                // everyone when it lands.
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
                _diagnostics?.Write("multiplayer.orders.refused",
                    new Dictionary<string, string?>
                    {
                        ["turn"] = refused.Turn.ToString(CultureInfo.InvariantCulture),
                        ["reason"] = refused.Reason,
                    });
                // Only about the turn the player is on. The outbox and the pump are independent
                // lanes, so a `409 turn_not_open` for turn N routinely arrives AFTER the seal of
                // turn N has already opened N+1 — and "TURN SYNC ERROR" then sat over a perfectly
                // healthy new turn until the next draft was accepted. `OrdersAccepted` already
                // draws this line.
                if (refused.Turn != _online.PlanningTurn) return;
                _message = refused.Reason.ToUpperInvariant();
                _online.TurnSyncError = refused.Reason.ToUpperInvariant();
                _online.ReadySubmissionPending = false;
                _online.ResolutionExpectedSince = null;
                if (refused.ReadinessWithdrawn && ReopenRefusedTurn(refused.Turn))
                    _online.Status = "FINISHED TURN REFUSED  CHANGE IT AND END THE TURN AGAIN";
                else if (_online.Stage == MultiplayerStage.WaitingForSeal)
                    _online.Status = refused.Reason.ToUpperInvariant();
                return;
            case MultiplayerNotice.ConnectionChanged connection:
                _online.IsConnected = connection.IsConnected;
                if (connection.IsConnected)
                {
                    _online.ReconnectLog.Clear();
                    _online.ReconnectCopyStatus = string.Empty;
                    _online.ReconnectAttempt = 0;
                    _message = string.Empty;
                    UpdateOnlineResolutionExpectation();
                }
                else if (connection.Detail is not null)
                {
                    _online.ResolutionExpectedSince = null;
                    _online.ReconnectAttempt = Math.Max(1, connection.Attempt);
                    _online.ReconnectLog.Add(ReconnectAttemptEntry.From(connection, DateTimeOffset.Now));
                    while (_online.ReconnectLog.Count > ReconnectPopupLayout.MaxRows)
                        _online.ReconnectLog.RemoveAt(0);
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
            {
                var api = ApiFailure(failed.Error);
                _diagnostics?.Write("multiplayer.failed", new Dictionary<string, string?>
                {
                    ["reason"] = failed.Reason,
                    ["error"] = RuntimeDiagnostics.ExceptionType(failed.Error),
                    ["operation"] = failed.Operation,
                    ["httpStatus"] = api is null
                        ? null
                        : ((int)api.Status).ToString(CultureInfo.InvariantCulture),
                    ["apiReason"] = api?.Reason,
                    ["requestId"] = api?.RequestId,
                    ["turn"] = _online.PlanningTurn.ToString(CultureInfo.InvariantCulture),
                    ["eventSequence"] = failed.LastEventSequence?.ToString(CultureInfo.InvariantCulture),
                });
                RememberOnlineFailure(failed);
                ShowOnlineMatchFailure(failed.Reason);
                return;
            }
            default:
                return;
        }
    }

    private void RememberOnlineFailure(MultiplayerNotice.Failed failed)
        => RememberOnlineFailure(failed.Error, failed.Operation, failed.LastEventSequence);

    private void RememberOnlineFailure(
        Exception? error,
        string? operation,
        int? lastEventSequence)
    {
        if (_activeMultiplayerRecovery is not { Completed: false } recovery) return;
        var api = ApiFailure(error);
        UpdateOnlineRecovery(recovery with
        {
            LastFailure = new MultiplayerRecoveryFailure(
                DateTimeOffset.UtcNow,
                _online.Stage.ToString(),
                operation,
                api is null ? null : (int)api.Status,
                api?.Reason,
                api?.RequestId,
                _online.PlanningTurn,
                lastEventSequence)
        });
    }

    private static MultiplayerApiException? ApiFailure(Exception? exception) => exception switch
    {
        MultiplayerApiException api => api,
        RetryExhaustedException { LastError: MultiplayerApiException api } => api,
        _ => null,
    };
}
