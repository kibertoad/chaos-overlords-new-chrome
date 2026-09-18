using System.Globalization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly TimeSpan LobbyPollInterval = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan OnlineResolutionGrace = TimeSpan.FromSeconds(30);

    private readonly MultiplayerUiState _online = new();
    /// <summary>
    /// The one client every online call goes through, bounded so a hostile server cannot answer with
    /// a body large enough to take the game down. See <see cref="MultiplayerClientOptions.MaximumResponseBytes"/>.
    /// </summary>
    private readonly HttpClient _http = MultiplayerClientOptions.CreateHttpClient();
    private MultiplayerLobbySession? _lobby;
    private MultiplayerMatchSession? _session;
    private TimeSpan _lobbyPollDue;
    private CancellationTokenSource? _serverProbeCancellation;
    private Task<bool>? _serverProbe;
    private readonly List<MultiplayerRecovery> _multiplayerRecoveries = [];
    private MultiplayerRecovery? _activeMultiplayerRecovery;
    private bool _configuringOnlineLobby;

    /// <summary>
    /// This client's own setup choices while a lobby's are on the screens that edit them.
    /// </summary>
    /// <remarks>Null when no lobby has been joined; see <see cref="RememberLocalSetup"/>.</remarks>
    private LocalSetupChoices? _localSetupBeforeLobby;
    private MultiplayerRecovery? LatestOnlineRecovery =>
        _multiplayerRecoveries.FirstOrDefault(recovery => recovery.CanReconnect);

    private void OpenOnline()
    {
        if (_session is not null) return;
        _online.Stage = MultiplayerStage.Connect;
        _online.Status = _multiplayerRecoveries.Any(recovery => recovery.ShouldSuggestReconnect)
            ? "AN INTERRUPTED MATCH CAN BE RECOVERED"
            : string.Empty;
        foreach (var field in new[]
                 { _online.Server, _online.DisplayName, _online.SessionName, _online.JoinCode, _online.Password })
            field.IsFocused = false;
        OnlineFields[0].IsFocused = true;
        BeginServerProbe();
        _screens.Show(ClientScreen.Online);
    }

    private void UpdateOnline(KeyboardState keyboard)
    {
        PumpServerProbe();
        if (_online.ConnectionError.Length > 0)
        {
            if (Pressed(keyboard, Keys.Escape) || Pressed(keyboard, Keys.Enter))
                DismissOnlineError();
            return;
        }
        if (Pressed(keyboard, Keys.Tab)) FocusNextOnlineField();
        if (_online.Stage == MultiplayerStage.History)
        {
            var count = RecoverableOnlineSessions.Count;
            if (Pressed(keyboard, Keys.Escape)) CloseOnlineHistory();
            else if (count > 0 && Pressed(keyboard, Keys.Up))
                _online.RecoverySelection = Mod(_online.RecoverySelection - 1, count);
            else if (count > 0 && Pressed(keyboard, Keys.Down))
                _online.RecoverySelection = Mod(_online.RecoverySelection + 1, count);
            else if (Pressed(keyboard, Keys.Enter)) ResumeSelectedOnlineMatch();
            return;
        }
        if (_online.Stage == MultiplayerStage.Discover)
        {
            if (_online.OpenDiscoveryFilter >= 0)
            {
                UpdateDiscoveryFilterMenu(keyboard);
                return;
            }
            var count = FilteredOnlineListings().Count;
            if (Pressed(keyboard, Keys.Escape)) CloseOnlineDiscovery();
            else if (count > 0 && Pressed(keyboard, Keys.Up))
                _online.DiscoverySelection = Mod(_online.DiscoverySelection - 1, count);
            else if (count > 0 && Pressed(keyboard, Keys.Down))
                _online.DiscoverySelection = Mod(_online.DiscoverySelection + 1, count);
            else if (Pressed(keyboard, Keys.Enter)) JoinSelectedOnlineListing();
            return;
        }
        if (_online.Stage == MultiplayerStage.LateJoinSeat)
        {
            var count = _online.PendingLateJoin?.AvailableSeatSummaries.Count ?? 0;
            if (Pressed(keyboard, Keys.Escape)) _online.Stage = MultiplayerStage.Discover;
            else if (count > 0 && Pressed(keyboard, Keys.Up))
                _online.LateJoinSeatSelection = Mod(_online.LateJoinSeatSelection - 1, count);
            else if (count > 0 && Pressed(keyboard, Keys.Down))
                _online.LateJoinSeatSelection = Mod(_online.LateJoinSeatSelection + 1, count);
            else if (Pressed(keyboard, Keys.Enter)) ConfirmLateJoin();
            return;
        }
        if (Pressed(keyboard, Keys.Enter) && _online.Stage == MultiplayerStage.Connect)
        {
            ContinueOnline();
        }
    }

    /// <summary>Routes typed characters to whichever text field currently owns focus.</summary>
    private void HandleTextInput(char character)
    {
        // Gated on the menu being open as well, so a panel flag left set by some other exit can
        // never quietly swallow the keystrokes meant for a server address.
        if (_gameMenuOpen && _bugReportOpen)
        {
            if (_bugReportFocus == BugReportFocus.Message) _bugReportText.Type(character);
            return;
        }
        if (_gameMenuOpen && _editingSaveName)
        {
            _saveName.Type(character);
            return;
        }
        if (_screens.Current == ClientScreen.Lobby)
        {
            if (_online.IsHost && _online.SessionName.IsFocused) _online.SessionName.Type(character);
            return;
        }
        if (_screens.Current != ClientScreen.Online) return;
        var previousServer = _online.Server.Value;
        foreach (var field in OnlineFields) field.Type(character);
        if (_online.Service == OnlineServiceMode.Custom
            && !string.Equals(previousServer, _online.Server.Value, StringComparison.Ordinal))
        {
            _online.ServerStatus = "CUSTOM SERVER NOT CHECKED";
        }
    }

    /// <summary>
    /// Starts a lobby session pointed at the typed address, or says why it could not.
    /// </summary>
    /// <remarks>
    /// The address is read once, here. Everything afterwards goes through the session, so a player
    /// who edits the field later cannot change the server a lobby is already running against, and no
    /// call site has to cope with a URL that has stopped parsing.
    /// </remarks>
    private bool TryBeginLobby()
    {
        if (_lobby is not null) return true;
        if (!TrySelectedServer(out var baseAddress))
        {
            _online.Stage = MultiplayerStage.Connect;
            _online.Status = "THE SERVER ADDRESS MUST BE AN HTTP OR HTTPS URL";
            return false;
        }
        _lobby = new MultiplayerLobbySession(_http, new MultiplayerClientOptions(baseAddress));
        SavePreferences();
        return true;
    }

    private void ContinueOnline()
    {
        if (_online.Role == OnlineConnectRole.Host) BeginHost();
        else BeginJoin();
    }

    /// <summary>
    /// Opens a lobby with the settings the setup screen is showing.
    /// </summary>
    /// <remarks>
    /// The scenario, duration, mentality and portraits ride the server's opaque settings blob,
    /// because the seed alone does not generate a city: every client needs the same choices to
    /// bootstrap the same match.
    /// </remarks>
    private void BeginHost()
    {
        if (!TryBeginLobby() || !RequireUsableName()) return;
        var settings = new MultiplayerGameSettings(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _playerPortraits,
            _defaultAiPolicy, _online.AllowLateJoin);
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "HOSTING";
        _online.JoinedInProgress = false;
        var password = OptionalPassword();
        _online.PasswordShown = password ?? string.Empty;
        _lobby!.Host(new CreateMatchRequest(
            new MatchSettings(
                SessionNameOrDefault(),
                MatchLimits.PlayerCount,
                SelectedOnlineTurnTimerSeconds,
                _online.PublicListing ? MatchVisibility.Public : MatchVisibility.Private,
                settings.ToWire()),
            _online.DisplayName.Value.Trim(),
            password,
            MultiplayerProtocolVersion.Current));
    }

    private void BeginJoin()
    {
        if (!RequireUsableName()) return;
        if (string.IsNullOrWhiteSpace(_online.JoinCode.Value))
        {
            _online.Status = "ENTER A JOIN CODE";
            return;
        }
        if (!TryBeginLobby()) return;
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "JOINING";
        _online.JoinedInProgress = false;
        var password = OptionalPassword();
        _online.PasswordShown = password ?? string.Empty;
        _lobby!.Join(new JoinMatchRequest(
            _online.JoinCode.Value.Trim(), _online.DisplayName.Value.Trim(), password));
    }

    private void ResumeSelectedOnlineMatch()
    {
        var sessions = RecoverableOnlineSessions;
        if (sessions.Count == 0) return;
        var recovery = sessions[Math.Clamp(_online.RecoverySelection, 0, sessions.Count - 1)];
        if (!Uri.TryCreate(recovery.Server, UriKind.Absolute, out var server))
        {
            _online.Status = "THE SAVED SERVER ADDRESS IS INVALID";
            return;
        }
        _online.Service = server == MultiplayerServiceEndpoint.Central
            ? OnlineServiceMode.Central
            : OnlineServiceMode.Custom;
        if (_online.Service == OnlineServiceMode.Custom) _online.Server.Set(recovery.Server);
        _serverProbeCancellation?.Cancel();
        _lobby = new MultiplayerLobbySession(_http, new MultiplayerClientOptions(server));
        _online.PasswordShown = recovery.Password;
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "RECONNECTING TO THE INTERRUPTED MATCH";
        _online.JoinedInProgress = true;
        _lobby.Resume(recovery.MatchId, recovery.PlayerId, recovery.Token, recovery.JoinCode);
    }

    /// <summary>
    /// Re-reads the lobby about once a second while it is on screen.
    /// </summary>
    /// <remarks>
    /// A lobby is the one place the client polls. The event stream carries these facts too, but it
    /// belongs to a session and a session belongs to a started match; a handful of reads a minute
    /// while people are still arriving is cheaper than opening one early and unwinding it if the
    /// player backs out.
    /// </remarks>
    private void PollLobby(GameTime gameTime)
    {
        if (_lobby is null) return;
        _lobbyPollDue -= gameTime.ElapsedGameTime;
        if (_lobbyPollDue > TimeSpan.Zero) return;
        _lobbyPollDue = LobbyPollInterval;
        _lobby.Refresh();
    }

    private void StartHostedMatch()
    {
        if (!CanConfigureOnlineLobby() || _lobby is null) return;
        _lobby.Start();
    }

    /// <summary>Bootstraps the match from the server's seed and roster, and opens turn 1.</summary>
    /// <remarks>
    /// A bootstrap that fails is the end of this client's match: the seed, the roster or the settings
    /// were something it cannot build a city from, and there is no version of that which playing on
    /// would improve. It says so and lets the player leave rather than starting a match it knows is
    /// not the one everyone else is in.
    /// </remarks>
    private void StartOnlineMatch(MatchView view)
    {
        if (_session is not null || _online.BootstrapFailed || _lobby?.Handle is null) return;
        if (_definitions is null)
        {
            _online.BootstrapFailed = true;
            _online.Status = "THE GAME'S DATA FILES ARE NOT LOADED";
            return;
        }
        try
        {
            _session = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
                _lobby.Handle, _definitions, view, _lobby.OwnPlayerId, view.LastEventSeq,
                _online.JoinedInProgress));
        }
        catch (Exception exception) when (exception is MultiplayerProtocolException
            or ArgumentOutOfRangeException or InvalidOperationException)
        {
            // Terminal for this client, and said once. The lobby is re-read every second while it is
            // on screen, and a seed or settings blob this build cannot build a city from will be the
            // same one a second later; all retrying would do is overwrite the explanation with itself.
            _online.BootstrapFailed = true;
            _online.Status = $"COULD NOT START THE MATCH  {exception.Message.ToUpperInvariant()}";
            _diagnostics?.Write("multiplayer.bootstrap.failed", new Dictionary<string, string?>
            {
                ["error"] = RuntimeDiagnostics.ExceptionType(exception),
            });
            return;
        }
        _online.Match = view;
        _online.DeadlineAt = _session.InitialDeadline;
        _online.SeatedSeats = view.Players.Count(player => player.Slot >= 0);
        ResetMatchPresentation(_session.InitialState);
        if (_session.IsRestoring)
        {
            _online.Status = "RESTORING THE MATCH";
            return;
        }
        if (!AdoptOnlineState(_session.InitialState)) return;
        _message = string.Empty;
        _screens.Show(ClientScreen.City);
    }

    /// <summary>
    /// Forgets what the previous match left on screen.
    /// </summary>
    /// <remarks>
    /// The same clearing <see cref="StartMatch"/> and <see cref="LoadGameFromSlot"/> do, for the
    /// path that starts a match from the server instead. Without it a hot-seat game played first
    /// leaves its combat progress behind — the new match's events carry lower sequence numbers, so
    /// they read as already seen and their animations never play — along with its site-search
    /// markers and its last-turn reports, which the events panel matches on player and turn number
    /// alone and would happily show from the wrong match.
    /// </remarks>
    private void ResetMatchPresentation(MatchState state)
    {
        _combatPresentationProgress.ResetTo(
            state.Players.Select(player => player.Id),
            state.Events.LastOrDefault()?.Sequence ?? -1);
        _combatAnimationPlayer.Clear();
        _siteSearchSelections.Reset();
        _lastTurnEventArchive.Clear();
    }

    /// <summary>
    /// Takes a fresh authoritative state and decides what there is to do with it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The one place that mapping is made, because there is more than one answer and the wrong one is
    /// a crash. A state in Command is a turn to plan. A state carrying an outcome is a finished match:
    /// the turn structure stops short of Command when the match ends, so trying to open a turn on it
    /// would throw — which is what used to happen at the end of every online match. Anything else is
    /// a state this client cannot account for, and saying so beats drawing it.
    /// </para>
    /// <para>
    /// Answers whether there is a turn to plan, so a caller can tell "adopted, go to the city" from
    /// "adopted, the match is over".
    /// </para>
    /// </remarks>
    private bool AdoptOnlineState(
        MatchState authoritative,
        OwnSubmissionView? submission = null)
    {
        if (_definitions is null || _session is null) return false;
        if (authoritative.Outcome is not null)
        {
            ConcludeOnlineMatch(authoritative);
            return false;
        }
        if (authoritative.Coordinator.Phase != TurnPhase.Command)
        {
            _online.Status = "THE MATCH REACHED A STATE THIS CLIENT CANNOT PLAY ON";
            _message = _online.Status;
            return false;
        }
        var turn = submission?.Orders is { } document
            ? SpeculativeTurn.Restore(authoritative, _definitions, _session.Slot, document)
            : SpeculativeTurn.For(authoritative, _definitions, _session.Slot);
        _actions = new MatchActions(turn);
        _state = turn.State;
        _online.PlanningTurn = authoritative.Coordinator.Turn;
        _online.Stage = submission?.Ready == true
            ? MultiplayerStage.WaitingForSeal
            : MultiplayerStage.Playing;
        _online.SentOrderDigest = submission?.OrdersHash;
        _online.ReadySubmissionPending = false;
        _online.ReadySubmissionAcknowledged = submission?.Ready == true;
        _online.ResolutionExpectedSince = null;
        _online.TurnSyncError = string.Empty;
        _online.ReadySeats = 0;
        _selectedGangIndex = 0;
        _cursor = _state.FindPlayer(new PlayerId(_session.Slot))?.Gangs
            .FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
        if (submission?.Ready == true) CloseOnlinePlanning();
        return true;
    }

    /// <summary>
    /// Settles on the final state and shows how the match ended.
    /// </summary>
    /// <remarks>
    /// No turn is opened on it. There is nothing left to plan, and the state is not in a phase a turn
    /// could be planned on — so the interface holds the final state for the endgame screen to read
    /// and the recorder goes away with the turn it belonged to.
    /// </remarks>
    private void ConcludeOnlineMatch(MatchState final)
    {
        _state = final;
        _online.Stage = MultiplayerStage.Finished;
        _online.DeadlineAt = null;
        CloseOnlinePlanning();
        _message = string.Empty;
        CompleteOnlineRecovery();
        _screens.Show(ClientScreen.Endgame);
    }

    /// <summary>
    /// Takes away the player's handle on the turn in front of them.
    /// </summary>
    /// <remarks>
    /// The lock that stops a command queued after the turn has gone from looking ordered. Every call
    /// site that mutates a match already refuses a null handle, which is what makes this a lock the
    /// interface cannot forget to take — rather than a stage check each of a dozen places has to
    /// remember. <see cref="MultiplayerUiState.PlanningIsOpen"/> is the same fact for the paths that
    /// want to say why.
    /// </remarks>
    private void CloseOnlinePlanning() => _actions = null;

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
                _online.Listings = listed.Matches;
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
                _online.SeatedSeats = resumed.Match.Players.Count(
                    player => player.Slot >= 0
                        && player.Status is WirePlayerStatus.Active or WirePlayerStatus.TakeoverPending);
                _online.Status = string.Empty;
                if (AdoptOnlineState(resumed.State, resumed.Submission))
                {
                    _message = resumed.Submission.Ready
                        ? "ORDERS RESTORED  WAITING FOR THE OTHER PLAYERS"
                        : "MATCH RESTORED";
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.TurnResolved resolved:
                if (AdoptOnlineState(resolved.State))
                {
                    _message = "NEW TURN READY  PLAY AGAIN";
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
                _online.TakeoverVotes[changed.PlayerId] = new TakeoverVotePrompt(
                    changed.PlayerId, name, changed.Turn, changed.Votes);
                return;
            case MultiplayerNotice.TakeoverVoteClosed closed:
                _online.TakeoverVotes.Remove(closed.PlayerId);
                _message = closed.ComputerControl
                    ? "PLAYERS APPROVED COMPUTER CONTROL"
                    : "THE PLAYER RETURNED  TAKEOVER VOTE CANCELLED";
                return;
            case MultiplayerNotice.DeadlineChanged deadline:
                _online.DeadlineAt = deadline.DeadlineAt;
                return;
            case MultiplayerNotice.ReadinessChanged readiness:
                if (readiness.Turn != _online.PlanningTurn) return;
                _online.ReadySeats = readiness.Ready;
                _online.SeatedSeats = readiness.Seated;
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
                    _online.Stage = MultiplayerStage.Finished;
                    CloseOnlinePlanning();
                    _message = string.Empty;
                    if (_state?.Outcome is not null) _screens.Show(ClientScreen.Endgame);
                }
                CompleteOnlineRecovery();
                return;
            case MultiplayerNotice.MatchAbandoned:
                _online.Stage = MultiplayerStage.Finished;
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

    /// <summary>
    /// Leaves the match and forgets the token; the seat stops being waited on.
    /// </summary>
    /// <remarks>
    /// The player is out as soon as they ask. Telling the server is started here and finished behind
    /// them, because a network that is already failing is the likeliest reason somebody is leaving and
    /// holding them in the lobby until it answers would be the wrong way round.
    /// </remarks>
    private void LeaveOnlineMatch()
    {
        Forget(_lobby?.LeaveAsync(), "multiplayer.leave.failed");
        if (_activeMultiplayerRecovery is { Completed: false } recovery)
            UpdateOnlineRecovery(recovery with { CleanExit = true });
        EndOnlineMatch("LEFT THE MATCH");
    }

    private bool HandleTakeoverVoteClick(Point point)
    {
        if (_online.CurrentTakeoverVote is not { } vote || _session is null) return false;
        TakeoverChoice? choice = point switch
        {
            _ when TakeoverVoteWait.Contains(point) => TakeoverChoice.Wait,
            _ when TakeoverVoteComputer.Contains(point) => TakeoverChoice.Computer,
            _ => null,
        };
        if (choice is { } selected)
        {
            Forget(
                _session.VoteOnTakeoverAsync(vote.PlayerId, selected),
                "multiplayer.takeover-vote.failed");
            _message = selected == TakeoverChoice.Wait
                ? "VOTED TO WAIT FOR THE PLAYER"
                : "VOTED TO USE COMPUTER CONTROL";
        }
        return true;
    }

    private bool HandleReconnectPopupClick(Point point)
    {
        if (_session is null || _online.IsConnected) return false;
        if (StopReconnectButton.Contains(point))
            EndOnlineMatch("AUTOMATIC RECONNECT CANCELLED");
        return true;
    }

    /// <summary>
    /// Tears down both sessions and returns to the title screen.
    /// </summary>
    /// <remarks>
    /// Stopping is asked for and not waited on. Both sessions wind down behind the game loop — a
    /// request in flight can take as long as its deadline, and the window must keep drawing — so the
    /// tasks they answer with are observed for their diagnostics and nothing else.
    /// </remarks>
    private void EndOnlineMatch(string status)
    {
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
        _serverProbeCancellation = null;
        _serverProbe = null;
        // Only an online match's state is this method's to throw away. Opening the online screen from
        // a hot-seat match in progress and backing out of it again must leave that match alone.
        if (_session is not null)
        {
            CloseOnlinePlanning();
            _state = null;
        }
        Forget(_session?.StopAsync(), "multiplayer.session.stop.failed");
        Forget(_lobby?.StopAsync(), "multiplayer.lobby.stop.failed");
        _session = null;
        _lobby = null;
        _online.Reset();
        RestoreLocalSetup();
        _online.Status = status;
        _message = status;
        _screens.Show(ClientScreen.Title);
    }

    /// <summary>
    /// Lets go of everything the online flow holds, as the window closes.
    /// </summary>
    /// <remarks>
    /// The sessions are given a moment to wind down rather than abandoned, because the last thing a
    /// player's own client can do for their opponents is stop the match waiting on a seat nobody is
    /// sitting in. It is a courtesy with a deadline, not a guarantee: the server's turn timer is what
    /// actually keeps a match moving when a client vanishes.
    /// </remarks>
    private void ReleaseOnlineResources()
    {
        if ((_session is not null || _lobby?.Handle is not null)
            && _activeMultiplayerRecovery is { Completed: false } recovery)
        {
            UpdateOnlineRecovery(recovery with { CleanExit = true });
        }
        _serverProbeCancellation?.Cancel();
        _serverProbeCancellation?.Dispose();
        _serverProbeCancellation = null;
        _serverProbe = null;
        var stopping = new[] { _session?.StopAsync(), _lobby?.StopAsync() }
            .OfType<Task>()
            .ToArray();
        _session = null;
        _lobby = null;
        try
        {
            Task.WaitAll(stopping, ShutdownGrace);
        }
        catch (AggregateException)
        {
            // Nothing to do about a session that failed on the way out.
        }
        _http.Dispose();
    }

    /// <summary>How long a closing window waits for the sessions to let go.</summary>
    private static readonly TimeSpan ShutdownGrace = TimeSpan.FromSeconds(2);

    /// <summary>Lets a shutdown finish on its own, logging it if it does not.</summary>
    private void Forget(Task? task, string diagnostic)
    {
        if (task is null) return;
        _ = task.ContinueWith(
            finished => _diagnostics?.Write(diagnostic, new Dictionary<string, string?>
            {
                ["error"] = RuntimeDiagnostics.ExceptionType(finished.Exception),
            }),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private void HandleOnlineClick(Point point)
    {
        if (_online.Stage == MultiplayerStage.Busy) return;
        if (_online.Stage == MultiplayerStage.History)
        {
            HandleOnlineHistoryClick(point);
            return;
        }
        if (_online.Stage == MultiplayerStage.Discover)
        {
            HandleOnlineDiscoveryClick(point);
            return;
        }
        if (_online.Stage == MultiplayerStage.LateJoinSeat)
        {
            HandleLateJoinSeatClick(point);
            return;
        }
        if (OnlineConnectLayout.Central.Contains(point))
            SelectOnlineService(OnlineServiceMode.Central);
        else if (OnlineConnectLayout.Custom.Contains(point))
            SelectOnlineService(OnlineServiceMode.Custom);
        else if (OnlineConnectLayout.HostRole.Contains(point))
            SelectOnlineRole(OnlineConnectRole.Host);
        else if (OnlineConnectLayout.JoinRole.Contains(point))
            SelectOnlineRole(OnlineConnectRole.Join);
        else if (_online.Role == OnlineConnectRole.Join
            && OnlineConnectLayout.PasteJoinCode.Contains(point)) PasteJoinCode();
        else if (_online.Role == OnlineConnectRole.Host
            && OnlineConnectLayout.PublicChoice.Contains(point)) SelectOnlineListing(publicly: true);
        else if (_online.Role == OnlineConnectRole.Host
            && OnlineConnectLayout.PrivateChoice.Contains(point)) SelectOnlineListing(publicly: false);
        else if (OnlineConnectLayout.Discover.Contains(point)) OpenOnlineDiscovery();
        else if (OnlineConnectLayout.Reconnect.Contains(point)) OpenOnlineHistory();
        else if (OnlineConnectLayout.Continue.Contains(point)) ContinueOnline();
        else if (OnlineConnectLayout.Back.Contains(point)) EndOnlineMatch(string.Empty);
        else FocusOnlineField(point);
    }

    private bool HandleOnlineErrorPopupClick(Point point)
    {
        if (_screens.Current != ClientScreen.Online || _online.ConnectionError.Length == 0)
            return false;
        if (OnlineConnectLayout.CopyError.Contains(point))
        {
            _online.ConnectionErrorCopyStatus = DesktopClipboard.TrySetText(_online.ConnectionError)
                ? "FULL ERROR COPIED"
                : "COULD NOT ACCESS THE CLIPBOARD";
        }
        else if (OnlineConnectLayout.DismissError.Contains(point))
        {
            DismissOnlineError();
        }
        return true;
    }

    private void DismissOnlineError()
    {
        _online.ConnectionError = string.Empty;
        _online.ConnectionErrorCopyStatus = string.Empty;
    }

    private void HandleOnlineDiscoveryClick(Point point)
    {
        if (_online.OpenDiscoveryFilter >= 0)
        {
            HandleDiscoveryFilterMenuClick(point);
            return;
        }
        if (OnlineConnectLayout.DiscoveryStatus.Contains(point))
            OpenDiscoveryFilterMenu(DiscoveryFilters.Status);
        else if (OnlineConnectLayout.DiscoveryScenario.Contains(point))
            OpenDiscoveryFilterMenu(DiscoveryFilters.Scenario);
        else if (OnlineConnectLayout.DiscoveryAi.Contains(point))
            OpenDiscoveryFilterMenu(DiscoveryFilters.Ai);
        else if (OnlineConnectLayout.DiscoveryJoin.Contains(point)) JoinSelectedOnlineListing();
        else if (OnlineConnectLayout.DiscoveryBack.Contains(point)) CloseOnlineDiscovery();
        else
        {
            var listings = FilteredOnlineListings();
            var offset = Math.Clamp(_online.DiscoverySelection - 4, 0, Math.Max(0, listings.Count - 5));
            for (var row = 0; row < Math.Min(5, listings.Count - offset); row++)
                if (OnlineConnectLayout.DiscoveryRow(row).Contains(point))
                    _online.DiscoverySelection = offset + row;
        }
    }

    private void HandleOnlineHistoryClick(Point point)
    {
        var sessions = RecoverableOnlineSessions;
        var offset = Math.Clamp(_online.RecoverySelection - 5, 0, Math.Max(0, sessions.Count - 6));
        for (var row = 0; row < Math.Min(6, sessions.Count - offset); row++)
            if (OnlineConnectLayout.HistoryRow(row).Contains(point))
                _online.RecoverySelection = offset + row;
        if (OnlineConnectLayout.HistoryRejoin.Contains(point)) ResumeSelectedOnlineMatch();
        else if (OnlineConnectLayout.HistoryBack.Contains(point)) CloseOnlineHistory();
    }

    private void HandleLobbyClick(Point point)
    {
        if (CanConfigureOnlineLobby() && OnlineLobbyLayout.SessionName.Contains(point))
        {
            _online.SessionName.IsFocused = true;
            return;
        }
        // Anywhere else finishes an edit of the name: the setting it belongs to is about to be sent,
        // or the player is leaving the screen the caret was on.
        CommitLobbySessionName();
        if (OnlineLobbyLayout.CopyCode.Contains(point)) CopyLobbyJoinCode();
        else if (OnlineLobbyLayout.Setup.Contains(point)) OpenOnlineSetup();
        else if (OnlineLobbyLayout.Start.Contains(point)) StartHostedMatch();
        else if (OnlineLobbyLayout.Leave.Contains(point)) LeaveOnlineMatch();
        else if (!CanConfigureOnlineLobby()) return;
        else if (OnlineLobbyLayout.PublicChoice.Contains(point)) ChangeLobbyListing(publicly: true);
        else if (OnlineLobbyLayout.PrivateChoice.Contains(point)) ChangeLobbyListing(publicly: false);
        else if (OnlineLobbyLayout.LateJoinAllowed.Contains(point)) ChangeLobbyLateJoin(allowed: true);
        else if (OnlineLobbyLayout.LateJoinRefused.Contains(point)) ChangeLobbyLateJoin(allowed: false);
    }

    private void UpdateLobby(KeyboardState keyboard, GameTime gameTime)
    {
        if (_online.SessionName.IsFocused)
        {
            if (Pressed(keyboard, Keys.Enter)) CommitLobbySessionName();
            PollLobby(gameTime);
            return;
        }
        if (Pressed(keyboard, Keys.Enter)) StartHostedMatch();
        else PollLobby(gameTime);
    }

    private void CopyLobbyJoinCode()
    {
        _online.Status = DesktopClipboard.TrySetText(_online.JoinCodeShown)
            ? "JOIN CODE COPIED"
            : "COULD NOT COPY JOIN CODE";
    }

    private void RememberOnlineMembership(MembershipView membership)
    {
        if (!TrySelectedServer(out var server)) return;
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
            _online.PasswordShown);
        _activeMultiplayerRecovery = recovery;
        _multiplayerRecoveries.RemoveAll(item => SameMembership(item, recovery));
        _multiplayerRecoveries.Insert(0, recovery);
        SaveOnlineRecoveries();
    }

    private void CompleteOnlineRecovery()
    {
        if (_activeMultiplayerRecovery is not { } recovery) return;
        UpdateOnlineRecovery(recovery with { CleanExit = true, Completed = true });
    }

    private void UpdateOnlineRecovery(MultiplayerRecovery recovery)
    {
        var index = _multiplayerRecoveries.FindIndex(item => SameMembership(item, recovery));
        if (index >= 0) _multiplayerRecoveries[index] = recovery;
        else _multiplayerRecoveries.Insert(0, recovery);
        _activeMultiplayerRecovery = recovery;
        SaveOnlineRecoveries();
    }

    private void SaveOnlineRecoveries() =>
        MultiplayerRecoveryStore.TrySaveAll(_multiplayerRecoveryPath, _multiplayerRecoveries);

    private static bool SameMembership(MultiplayerRecovery left, MultiplayerRecovery right) =>
        string.Equals(left.Server, right.Server, StringComparison.OrdinalIgnoreCase)
        && left.MatchId == right.MatchId
        && left.PlayerId == right.PlayerId;

    /// <summary>
    /// What to say when the player acts on a turn that is no longer theirs to change.
    /// </summary>
    /// <remarks>
    /// Reached whenever there is no handle on a match to mutate, which online means the turn has been
    /// submitted and in a hot-seat match means there is no match at all. The online reading is the
    /// one worth a message, and it is the only one a player can arrive at by pressing a key.
    /// </remarks>
    private const string OnlinePlanningClosed = "TURN SENT; WAITING FOR PLAYERS";

}
