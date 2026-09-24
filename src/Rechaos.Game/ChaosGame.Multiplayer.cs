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
    /// <summary>
    /// How long every seat may be ready with no sealed turn arriving before the client goes and
    /// looks for itself.
    /// </summary>
    /// <remarks>
    /// It has to sit ABOVE the stream's own idle detector plus its first reconnect, or it fires
    /// first and pre-empts the recovery that was already on its way. The server seals in the same
    /// request that completes the roster, so the ready `PUT` succeeds on a fresh connection while
    /// `turn.sealed` goes out on a stream a suspended laptop or an expired NAT entry has silently
    /// killed; the stream notices at <see cref="MatchEventStream.DefaultIdleTimeout"/>, fifty
    /// seconds, and comes back from its `Last-Event-ID`. At thirty seconds this watchdog was tearing
    /// the session down twenty seconds before the mechanism that fixes it even woke up.
    /// <para>
    /// The margin covers the stream's detector only while the pump is reading: time a handler spends
    /// on an event is not silence, so a dead socket found during a long handler — a desync repair
    /// waiting on its reports — can outlast this grace. Losing that race costs a resync and nothing
    /// more: <see cref="MultiplayerMatchSession.RequestResync"/> also ends that wait.
    /// </para>
    /// </remarks>
    private static readonly TimeSpan OnlineResolutionGrace =
        MatchEventStream.DefaultIdleTimeout + TimeSpan.FromSeconds(25);

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
    private CancellationTokenSource? _recoveryReconciliationCancellation;
    private Task<IReadOnlyList<MultiplayerRecovery>>? _recoveryReconciliation;
    private readonly List<MultiplayerRecovery> _multiplayerRecoveries = [];

    /// <summary>
    /// Bumped whenever <see cref="_multiplayerRecoveries"/> changes, so the filtered view over it
    /// can tell whether it is stale without comparing the lists.
    /// </summary>
    private int _multiplayerRecoveryVersion;
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
        BeginRecoveryReconciliation();
        _screens.Show(ClientScreen.Online);
    }

    private void UpdateOnline(KeyboardState keyboard)
    {
        PumpServerProbe();
        PumpRecoveryReconciliation();
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
            else if (Pressed(keyboard, Keys.F5)) RefreshOnlineDiscovery();
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
        if (_online.Stage != MultiplayerStage.Connect) return;
        // The face is the one control on the form that is neither a field nor a button, so the
        // arrow keys can turn it whichever field currently owns the caret.
        if (Pressed(keyboard, Keys.Left)) CycleOnlinePortrait(-1);
        if (Pressed(keyboard, Keys.Right)) CycleOnlinePortrait(1);
        if (Pressed(keyboard, Keys.Enter)) ContinueOnline();
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
            else if (EditingLobbyName) _online.DisplayName.Type(character);
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
            _online.Portrait,
            password,
            MultiplayerProtocolVersion.Current,
            MultiplayerSessionVersion.Current));
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
            _online.JoinCode.Value.Trim(), _online.DisplayName.Value.Trim(),
            _online.Portrait, password));
    }

    private void ResumeSelectedOnlineMatch()
    {
        if (SelectedOnlineRecovery is not { } recovery) return;
        // The match view settles this again on the way in; refusing here only spares the player a
        // round trip that ends in the same answer, beside the row that caused it.
        if (!recovery.IsCompatible)
        {
            _online.Status = OnlineHistoryPresentation.IncompatibleReason;
            return;
        }
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
        if (!CanConfigureOnlineLobby() || _lobby is null || _lobby.IsBusy) return;
        _online.Status = "STARTING THE GAME";
        _lobby.Start();
    }

    /// <summary>
    /// Starts the match once the server has finished starting it; false while it has not.
    /// </summary>
    /// <remarks>
    /// The server commits `running` on turn 0 before it seats anyone or opens turn 1, and a lobby
    /// poll or a resumed seat can read the match in between. Bootstrapping that view would fail, and
    /// a failed bootstrap is final, so an early view is refused here, before anything is changed,
    /// and the caller keeps the player in the lobby, whose poll brings the finished match back.
    /// </remarks>
    /// <param name="view">The match as the server last described it.</param>
    /// <param name="resumingSeat">
    /// A saved seat taken back in a running match: it is restored on the online screen, as a
    /// player who joined in progress, rather than opened from the lobby.
    /// </param>
    private bool TryStartOnlineMatch(MatchView view, bool resumingSeat = false)
    {
        if (!MultiplayerMatchSession.HasFinishedStarting(view)) return false;
        if (resumingSeat)
        {
            _online.JoinedInProgress = true;
            _online.Stage = MultiplayerStage.Busy;
            _online.Status = "RESTORING THE MATCH";
            _screens.Show(ClientScreen.Online);
        }
        BootstrapOnlineMatch(view);
        return true;
    }

    /// <summary>Bootstraps the match from the server's seed and roster, and opens turn 1.</summary>
    /// <remarks>
    /// A bootstrap that fails is the end of this client's match: the seed, the roster or the settings
    /// were something it cannot build a city from, and there is no version of that which playing on
    /// would improve. It says so and lets the player leave rather than starting a match it knows is
    /// not the one everyone else is in. Reached only through <see cref="TryStartOnlineMatch"/>, so a
    /// view that is merely early never gets here.
    /// </remarks>
    private void BootstrapOnlineMatch(MatchView view)
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
        _online.SelfPlayerId = _session.PlayerId;
        _online.DeadlineAt = _session.Bootstrap.Deadline;
        _online.AwaitedSlots = AwaitedSeats(view.Players);
        ResetMatchPresentation(_session.Bootstrap.State, view.Id);
        if (_session.IsRestoring)
        {
            _online.Status = "RESTORING THE MATCH";
            return;
        }
        if (!AdoptOnlineState(_session.Bootstrap.State)) return;
        _message = string.Empty;
        _screens.Show(ClientScreen.City);
    }

    /// <summary>
    /// The seats the turn waits on, as the server's roster describes them.
    /// </summary>
    /// <remarks>
    /// The same rule the session applies to the event stream, for the two moments the interface
    /// holds a roster before any readiness has been reported. A seat handed to the computer is no
    /// longer waited on; a temporarily absent one still is, until its takeover vote says otherwise.
    /// A seat that left is waited on while its vote is open too, which the roster cannot show: the
    /// session corrects the count with a readiness notice once it knows the votes.
    /// </remarks>
    private static IReadOnlySet<int> AwaitedSeats(IEnumerable<PlayerView> players) =>
        players
            .Where(player => player.Slot is >= 0 and < MatchLimits.PlayerCount
                && player.Status is WirePlayerStatus.Active or WirePlayerStatus.TakeoverPending)
            .Select(player => player.Slot)
            .ToHashSet();

    /// <summary>
    /// Forgets what the previous match left on screen.
    /// </summary>
    /// <remarks>
    /// The same clearing <see cref="StartMatch"/> and <see cref="LoadGameFromSlot"/> do, for the
    /// path that starts a match from the server instead. Without it a hot-seat game played first
    /// leaves its combat progress behind — the new match's events carry lower sequence numbers, so
    /// they read as already seen and their animations never play — along with its site-search
    /// markers and its last-turn reports, which the events panel matches on player and turn number
    /// alone and would happily show from the wrong match, and the gangs it remembers for combat,
    /// whose ids every match hands out again from the start.
    /// </remarks>
    /// <param name="onlineMatch">
    /// The server's id for an online match. A resume of the match the gangs were remembered from
    /// keeps them: the turn that sealed while this client was away wiped out gangs only its planning
    /// frames ever saw.
    /// </param>
    private void ResetMatchPresentation(MatchState state, string? onlineMatch = null)
    {
        _combatPresentationProgress.ResetTo(
            state.Players.Select(player => player.Id),
            state.Events.LastOrDefault()?.Sequence ?? -1);
        _combatants.ResetTo(onlineMatch);
        ResetTransientMatchUi();
        _siteSearchSelections.Reset();
        _lastTurnEventArchive.Clear();
    }

    /// <summary>
    /// Clears interface state that belongs to one turn and must not outlive it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The idle-gang warning was cleared by Confirm, Cancel and the local planning timer, and by
    /// nothing else. Online, a deadline that sealed the turn while the warning was open left it on
    /// screen over the next turn, where OK submitted that turn as ready with no orders. After the
    /// match ended it kept swallowing F1, O and Escape on the title screen and then reappeared over
    /// turn 1 of the next match.
    /// </para>
    /// <para>
    /// The automatic combat presentation has the same shape: left running when a match ends, Update
    /// keeps returning early with no state to draw, and the queue draining shows the city screen
    /// with no match behind it.
    /// </para>
    /// <para>
    /// A gang drag is the same again: the match it was aimed at is gone, so the token under the
    /// pointer belongs to nothing and the release would drop it on whatever replaced it.
    /// </para>
    /// </remarks>
    private void ResetTransientMatchUi()
    {
        _idleGangWarningOpen = false;
        CancelHireReject();
        ForgetGangDrag();
        _combatAnimationPlayer.Clear();
        _automaticDetailedCombatPresentation = false;
        _openEventsAfterCombat = false;
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
    /// <param name="authoritative">The state as the session last settled it; the interface owns this copy.</param>
    /// <param name="submission">What the server holds for this seat on the open turn, on a restore.</param>
    /// <param name="restored">
    /// The planning copy the session already built on a restore, with the saved draft replayed
    /// onto it. Built there rather than here because building it is what proves the draft still
    /// applies, and a draft that does not is a protocol failure rather than a crash on this thread.
    /// </param>
    private bool AdoptOnlineState(
        MatchState authoritative,
        OwnSubmissionView? submission = null,
        SpeculativeTurn? restored = null)
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
        var turn = restored ?? SpeculativeTurn.For(authoritative, _definitions, _session.Slot);
        _actions = new MatchActions(turn);
        _submittedPlanning = null;
        _state = turn.State;
        // The session drains every notice in one frame, so a turn adopted here can be replaced by the
        // next one before the frame's own observation runs; its roster is the only sight of a gang
        // hired on it and wiped out on the turn after.
        _combatants.Observe(_state);
        _online.PlanningTurn = authoritative.Coordinator.Turn;
        _online.Stage = submission?.Ready == true
            ? MultiplayerStage.WaitingForSeal
            : MultiplayerStage.Playing;
        _online.SentOrderDigest = submission?.OrdersHash;
        // A restored draft is already the document the server holds; a fresh turn has sent nothing.
        _sentOrderVersion = submission?.Orders is null ? UnsentOrders : turn.Orders.Version;
        _online.ReadySubmissionPending = false;
        _online.ReadySubmissionAcknowledged = submission?.Ready == true;
        _online.ResolutionExpectedSince = null;
        _online.TurnSyncError = string.Empty;
        _online.ReadySlots = MultiplayerUiState.NoSeats;
        // The turn on screen is being replaced, so picks made on the old one are spent whether or
        // not the player submitted it — the authoritative clock can seal a turn out from under them,
        // and the gangs they picked may have moved since.
        _gangSelection.Clear();
        // A drag in progress was aimed at the turn being replaced, and this is the one path that
        // replaces it without going through ResetTransientMatchUi. Left alone it would keep
        // painting the old turn's destinations over the new board until the button came up.
        ForgetGangDrag();
        // The idle-gang warning belongs to the turn that is being replaced. Left open, OK on it
        // submits the new turn as ready with no orders, and there is no taking that back.
        _idleGangWarningOpen = false;
        CancelHireReject();
        _selectedGangIndex = 0;
        _cursor = _state.FindPlayer(new PlayerId(_session.Slot))?.Gangs
            .FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
        if (submission?.Ready == true) CloseOnlinePlanning();
        TouchOnlineRecovery();
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
        _online.ConcludeMatch();
        CloseOnlinePlanning();
        ResetTransientMatchUi();
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
    /// Leaves the match and forgets the token; the seat stops being waited on.
    /// </summary>
    /// <remarks>
    /// The player is out as soon as they ask. Telling the server is started here and finished behind
    /// them, because a network that is already failing is the likeliest reason somebody is leaving and
    /// holding them in the lobby until it answers would be the wrong way round.
    /// </remarks>
    private void LeaveOnlineMatch()
    {
        _pendingLeave = _lobby?.LeaveAsync();
        Forget(_pendingLeave, "multiplayer.leave.failed");
        if (_activeMultiplayerRecovery is { Completed: false } recovery)
            UpdateOnlineRecovery(recovery with { CleanExit = true });
        EndOnlineMatch("LEFT THE MATCH");
    }

    private bool HandleReconnectPopupClick(Point point)
    {
        if (_session is null || !_online.ReconnectPopupShown) return false;
        if (ReconnectPopupLayout.StopRetrying.Contains(point))
        {
            EndOnlineMatch("AUTOMATIC RECONNECT CANCELLED");
        }
        else if (ReconnectPopupLayout.CopyErrorAt(point, _online.ReconnectLog.Count) is { } row)
        {
            var attempt = _online.ReconnectLog[row];
            _online.ReconnectCopyStatus = DesktopClipboard.TrySetText(attempt.Details)
                ? $"ATTEMPT {attempt.Attempt} ERROR COPIED"
                : "COULD NOT ACCESS THE CLIPBOARD";
        }
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
        _recoveryReconciliationCancellation?.Cancel();
        _recoveryReconciliationCancellation?.Dispose();
        _recoveryReconciliationCancellation = null;
        _recoveryReconciliation = null;
        CancelServerProbe();
        // Only an online match's state is this method's to throw away. Opening the online screen from
        // a hot-seat match in progress and backing out of it again must leave that match alone.
        if (_session is not null)
        {
            CloseOnlinePlanning();
            ResetTransientMatchUi();
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
        else if (OnlineConnectLayout.PortraitPrevious.Contains(point)) CycleOnlinePortrait(-1);
        else if (OnlineConnectLayout.PortraitNext.Contains(point)
            || OnlineConnectLayout.Portrait.Contains(point)) CycleOnlinePortrait(1);
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
        else if (OnlineConnectLayout.DiscoveryRefresh.Contains(point)) RefreshOnlineDiscovery();
        else if (OnlineConnectLayout.DiscoveryBack.Contains(point)) CloseOnlineDiscovery();
        else if (RowClicked(point, OnlineConnectLayout.DiscoveryRow,
                     DiscoveredListingWindow()) is { } listing)
            _online.DiscoverySelection = listing;
    }

    /// <summary>The window of listings the browser is showing, which drawing and clicking both read.</summary>
    private ListScrollWindow DiscoveredListingWindow() => ListScrollWindow.Of(
        FilteredOnlineListings().Count, _online.DiscoverySelection, OnlineScreenLayout.ListRows);

    /// <summary>
    /// The entry a click landed on, or null when it landed somewhere else.
    /// </summary>
    /// <remarks>
    /// The rows a screen draws and the rows it can be clicked on are the same rows, so both ask the
    /// window rather than each recomputing the scroll offset. They had drifted apart once already:
    /// the browser drew five rows and read a click on the sixth as a click on the fifth.
    /// </remarks>
    private static int? RowClicked(
        Point point, Func<int, Rectangle> row, ListScrollWindow window)
    {
        for (var index = 0; index < window.VisibleRows; index++)
            if (row(index).Contains(point)) return window.IndexAt(index);
        return null;
    }

    private void HandleOnlineHistoryClick(Point point)
    {
        var window = ListScrollWindow.Of(
            RecoverableOnlineSessions.Count, _online.RecoverySelection, OnlineScreenLayout.ListRows);
        if (RowClicked(point, OnlineConnectLayout.HistoryRow, window) is { } session)
            _online.RecoverySelection = session;
        if (OnlineConnectLayout.HistoryRejoin.Contains(point)) ResumeSelectedOnlineMatch();
        else if (OnlineConnectLayout.HistoryBack.Contains(point)) CloseOnlineHistory();
    }

    private void HandleLobbyClick(Point point)
    {
        if (CanConfigureOnlineLobby() && LobbySessionName.Contains(point))
        {
            FinishLobbyNameEdit(cancel: false);
            _online.SessionName.IsFocused = true;
            return;
        }
        if (HandleLobbyProfileClick(point)) return;
        // Anywhere else finishes an edit of either name: the setting it belongs to is about to be
        // sent, or the player is leaving the screen the caret was on.
        CommitLobbySessionName();
        FinishLobbyNameEdit(cancel: false);
        if (LobbyCopyCode.Contains(point)) CopyLobbyJoinCode();
        else if (LobbySetup.Contains(point)) OpenOnlineSetup();
        else if (LobbyStart.Contains(point)) StartHostedMatch();
        else if (LobbyLeave.Contains(point)) LeaveOnlineMatch();
        else if (!CanConfigureOnlineLobby()) return;
        else if (LobbyPublicChoice.Contains(point)) ChangeLobbyListing(publicly: true);
        else if (LobbyPrivateChoice.Contains(point)) ChangeLobbyListing(publicly: false);
        else if (LobbyLateJoinAllowed.Contains(point)) ChangeLobbyLateJoin(allowed: true);
        else if (LobbyLateJoinRefused.Contains(point)) ChangeLobbyLateJoin(allowed: false);
    }

    private bool UsesClassicLobby => _onlineLobbyPresentation == OnlineLobbyPresentation.Classic;
    private Rectangle LobbySessionName => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.SessionName : OnlineLobbyLayout.SessionName;
    private Rectangle LobbyCopyCode => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.CopyCode : OnlineLobbyLayout.CopyCode;
    private Rectangle LobbySetup => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.Setup : OnlineLobbyLayout.Setup;
    private Rectangle LobbyStart => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.Start : OnlineLobbyLayout.Start;
    private Rectangle LobbyLeave => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.Leave : OnlineLobbyLayout.Leave;
    private Rectangle LobbyPublicChoice => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.PublicChoice : OnlineLobbyLayout.PublicChoice;
    private Rectangle LobbyPrivateChoice => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.PrivateChoice : OnlineLobbyLayout.PrivateChoice;
    private Rectangle LobbyLateJoinAllowed => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.LateJoinAllowed : OnlineLobbyLayout.LateJoinAllowed;
    private Rectangle LobbyLateJoinRefused => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.LateJoinRefused : OnlineLobbyLayout.LateJoinRefused;
    private Rectangle LobbyRosterPortrait(int row) => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.RosterPortrait(row) : OnlineLobbyLayout.RosterPortrait(row);
    private Rectangle LobbyRosterName(int row) => UsesClassicLobby
        ? ClassicOnlineLobbyLayout.RosterName(row) : OnlineLobbyLayout.RosterName(row);

    private void UpdateLobby(KeyboardState keyboard, GameTime gameTime)
    {
        // The match started under an edit: the roster is final, so what was being typed goes.
        if (_online.DisplayName.IsFocused && !CanEditLobbyProfile())
            FinishLobbyNameEdit(cancel: true);
        if (_online.DisplayName.IsFocused)
        {
            if (Pressed(keyboard, Keys.Enter)) FinishLobbyNameEdit(cancel: false);
            else if (Pressed(keyboard, Keys.Escape)) FinishLobbyNameEdit(cancel: true);
            PollLobby(gameTime);
            return;
        }
        SendPendingLobbyProfile();
        if (_online.SessionName.IsFocused)
        {
            if (Pressed(keyboard, Keys.Enter)) CommitLobbySessionName();
            PollLobby(gameTime);
            return;
        }
        // The arrows turn the player's own face, as they do on the connect form.
        if (Pressed(keyboard, Keys.Left)) CycleLobbyPortrait(-1);
        else if (Pressed(keyboard, Keys.Right)) CycleLobbyPortrait(1);
        if (Pressed(keyboard, Keys.Enter)) StartHostedMatch();
        else PollLobby(gameTime);
    }

    /// <summary>
    /// Whether there is a join code to read out, which drawing and clicking both ask.
    /// </summary>
    /// <remarks>
    /// COPY is drawn disabled until the server has answered with a code, but ran all the same when
    /// pressed: it put an empty string on the clipboard and reported JOIN CODE COPIED, sending the
    /// host off to paste nothing to the people waiting on it.
    /// </remarks>
    private bool HasLobbyJoinCode => _online.JoinCodeShown.Length > 0;

    private void CopyLobbyJoinCode()
    {
        if (!HasLobbyJoinCode) return;
        _online.Status = DesktopClipboard.TrySetText(_online.JoinCodeShown)
            ? "JOIN CODE COPIED"
            : "COULD NOT COPY JOIN CODE";
    }

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
        MultiplayerRecoveryStore.TrySaveAll(_multiplayerRecoveryPath, _multiplayerRecoveries, durable);
    }

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
