using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle OnlineServerField = new(120, 116, 400, 22);
    private static readonly Rectangle OnlineNameField = new(120, 150, 400, 22);
    private static readonly Rectangle OnlineJoinCodeField = new(120, 184, 400, 22);
    private static readonly Rectangle OnlinePasswordField = new(120, 218, 400, 22);
    private static readonly Rectangle OnlineHost = new(120, 252, 124, 30);
    private static readonly Rectangle OnlineJoin = new(258, 252, 124, 30);
    private static readonly Rectangle OnlineBack = new(396, 252, 124, 30);
    private static readonly Rectangle LobbyStart = new(160, 372, 148, 32);
    private static readonly Rectangle LobbyLeave = new(332, 372, 148, 32);

    private static readonly TimeSpan LobbyPollInterval = TimeSpan.FromSeconds(1);

    /// <summary>
    /// How often an unfinished turn's plan is sent to the server as a draft.
    /// </summary>
    /// <remarks>
    /// The document is a whole-document replace, so a draft costs one small request and means a turn
    /// that seals on the clock seals with what the player had actually planned rather than with
    /// nothing. Ten seconds is short enough that little is ever lost and long enough that a player
    /// reordering six gangs does not send six requests.
    /// </remarks>
    private static readonly TimeSpan DraftInterval = TimeSpan.FromSeconds(10);

    private readonly MultiplayerUiState _online = new();
    private readonly HttpClient _http = new();
    private MultiplayerLobbySession? _lobby;
    private MultiplayerMatchSession? _session;
    private TimeSpan _lobbyPollDue;

    private TextField[] OnlineFields =>
        [_online.Server, _online.DisplayName, _online.JoinCode, _online.Password];

    private void OpenOnline()
    {
        if (_session is not null) return;
        _online.Stage = MultiplayerStage.Connect;
        _online.Status = "HOST A MATCH OR JOIN ONE BY CODE";
        _online.Server.IsFocused = true;
        _screens.Show(ClientScreen.Online);
    }

    private void UpdateOnline(KeyboardState keyboard)
    {
        if (Pressed(keyboard, Keys.Tab)) FocusNextOnlineField();
        if (Pressed(keyboard, Keys.Enter) && _online.Stage == MultiplayerStage.Connect)
        {
            if (_online.JoinCode.Value.Length > 0) BeginJoin();
            else BeginHost();
        }
    }

    private void FocusNextOnlineField()
    {
        var fields = OnlineFields;
        var current = Array.FindIndex(fields, field => field.IsFocused);
        foreach (var field in fields) field.IsFocused = false;
        fields[Mod(current + 1, fields.Length)].IsFocused = true;
    }

    /// <summary>Routes typed characters to the focused field; only the online screens have any.</summary>
    private void HandleTextInput(char character)
    {
        if (_screens.Current != ClientScreen.Online) return;
        foreach (var field in OnlineFields) field.Type(character);
    }

    /// <summary>
    /// Moves focus to the field that was clicked, if one was.
    /// </summary>
    /// <remarks>
    /// A click that misses every field — on a button, or on the panel — leaves focus alone. Clearing
    /// it would mean a player who pressed HOST and then carried on typing had their keystrokes go
    /// nowhere, with a caret still blinking somewhere to say they had not.
    /// </remarks>
    private void FocusOnlineField(Point point)
    {
        (Rectangle Bounds, TextField Field)[] hits =
        [
            (OnlineServerField, _online.Server),
            (OnlineNameField, _online.DisplayName),
            (OnlineJoinCodeField, _online.JoinCode),
            (OnlinePasswordField, _online.Password),
        ];
        if (!Array.Exists(hits, hit => hit.Bounds.Contains(point))) return;
        foreach (var (bounds, field) in hits) field.IsFocused = bounds.Contains(point);
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
        var address = _online.Server.Value.Trim();
        if (!Uri.TryCreate(address, UriKind.Absolute, out var baseAddress)
            || baseAddress.Scheme is not ("http" or "https"))
        {
            _online.Stage = MultiplayerStage.Connect;
            _online.Status = "THE SERVER ADDRESS MUST BE AN HTTP OR HTTPS URL";
            return false;
        }
        _lobby = new MultiplayerLobbySession(_http, new MultiplayerClientOptions(baseAddress));
        return true;
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
        var name = _online.DisplayName.Value.Trim();
        var settings = new MultiplayerGameSettings(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _playerPortraits);
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "HOSTING";
        _lobby!.Host(new CreateMatchRequest(
            new MatchSettings(
                $"{name}'S CITY",
                Math.Max(MinimumOnlinePlayers, _localSetupRoster.Count),
                OnlineTurnTimerSeconds,
                MatchVisibility.Private,
                settings.ToWire()),
            name,
            OptionalPassword()));
    }

    private void BeginJoin()
    {
        if (!TryBeginLobby() || !RequireUsableName()) return;
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = "JOINING";
        _lobby!.Join(new JoinMatchRequest(
            _online.JoinCode.Value.Trim(), _online.DisplayName.Value.Trim(), OptionalPassword()));
    }

    private string? OptionalPassword() =>
        _online.Password.Value.Length > 0 ? _online.Password.Value : null;

    /// <summary>
    /// Refuses a name the original rules read as a cheat code before the server has to.
    /// </summary>
    /// <remarks>
    /// The server refuses these too, and its refusal is the one that counts — but saying so here
    /// turns a round trip into an immediate answer, and names which field is wrong while the player
    /// is still looking at it. See <see cref="ReservedPlayerNames"/> for why they cannot be allowed
    /// through: online, one player's name changes what every client computes.
    /// </remarks>
    private bool RequireUsableName()
    {
        var name = _online.DisplayName.Value.Trim();
        if (name.Length == 0)
        {
            _online.Status = "ENTER A NAME";
            return false;
        }
        if (ReservedPlayerNames.IsReserved(name))
        {
            _online.Status = "THAT NAME IS A CHEAT CODE  PICK ANOTHER";
            return false;
        }
        return true;
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
        if (!_online.IsHost || _lobby is null || _online.Match is null) return;
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
                _lobby.Handle, _definitions, view, _lobby.OwnPlayerId, view.LastEventSeq));
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
                ["error"] = exception.ToString(),
            });
            return;
        }
        _online.Match = view;
        _online.DeadlineAt = _session.InitialDeadline;
        _online.SeatedSeats = view.Players.Count(player => player.Slot >= 0);
        if (!AdoptOnlineState(_session.InitialState)) return;
        _message = "PLAN YOUR TURN";
        _screens.Show(ClientScreen.City);
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
    private bool AdoptOnlineState(MatchState authoritative)
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
        var turn = SpeculativeTurn.For(authoritative, _definitions, _session.Slot);
        _actions = new MatchActions(turn);
        _state = turn.State;
        _online.PlanningTurn = authoritative.Coordinator.Turn;
        _online.Stage = MultiplayerStage.Playing;
        _online.SentOpCount = 0;
        _online.DraftDue = DraftInterval;
        _online.ReadySeats = 0;
        _selectedGangIndex = 0;
        _cursor = _state.FindPlayer(new PlayerId(_session.Slot))?.Gangs
            .FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
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
        _message = "MATCH COMPLETE";
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

    /// <summary>Sends what the player planned and marks them ready; the turn seals on the last one.</summary>
    private void SubmitOnlineTurn()
    {
        if (_session is null || _actions?.OnlineTurn is not { } turn) return;
        if (!_online.PlanningIsOpen) return;
        _session.QueueOrders(_online.PlanningTurn, turn.Build(), ready: true);
        _online.SentOpCount = turn.Orders.Count;
        _online.Stage = MultiplayerStage.WaitingForSeal;
        CloseOnlinePlanning();
        _message = "WAITING FOR THE OTHER PLAYERS";
    }

    /// <summary>
    /// Sends the turn so far, without saying the player is done.
    /// </summary>
    /// <remarks>
    /// So that a turn the clock seals seals with what the player had planned. The server replaces the
    /// document it holds, so a draft is never additive and never has to be reconciled with the one
    /// that follows it; sending nothing at all would make a missed deadline cost the player their
    /// whole turn.
    /// </remarks>
    private void SendOnlineDraft(GameTime gameTime)
    {
        if (_session is null || !_online.PlanningIsOpen) return;
        if (_actions?.OnlineTurn is not { } turn) return;
        _online.DraftDue -= gameTime.ElapsedGameTime;
        if (_online.DraftDue > TimeSpan.Zero) return;
        _online.DraftDue = DraftInterval;
        if (turn.Orders.Count == _online.SentOpCount) return;
        _online.SentOpCount = turn.Orders.Count;
        _session.QueueOrders(_online.PlanningTurn, turn.Build(), ready: false);
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
    }

    private void Apply(LobbyNotice notice)
    {
        switch (notice)
        {
            case LobbyNotice.Seated seated:
                _online.IsHost = seated.Membership.Player.IsHost;
                _online.JoinCodeShown = seated.Membership.JoinCode;
                _online.Match = seated.Membership.Match;
                _online.Stage = MultiplayerStage.Lobby;
                _online.Status = _online.IsHost
                    ? "READ OUT THE JOIN CODE"
                    : "WAITING FOR THE HOST";
                _screens.Show(ClientScreen.Lobby);
                return;
            case LobbyNotice.Updated updated:
                _online.Match = updated.Match;
                if (_session is null && updated.Match.Status == MatchStatus.Running)
                    StartOnlineMatch(updated.Match);
                return;
            case LobbyNotice.Failed failed:
                if (_online.Stage == MultiplayerStage.Busy) _online.Stage = MultiplayerStage.Connect;
                _online.Status = failed.Reason;
                return;
            default:
                return;
        }
    }

    private void Apply(MultiplayerNotice notice)
    {
        switch (notice)
        {
            case MultiplayerNotice.TurnResolved resolved:
                if (AdoptOnlineState(resolved.State))
                {
                    _message = $"TURN {resolved.Turn} RESOLVED";
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.Resynced resynced:
                if (AdoptOnlineState(resynced.State))
                {
                    _message = $"RESYNCED ON TURN {resynced.Turn}";
                    _screens.Show(ClientScreen.City);
                }
                return;
            case MultiplayerNotice.Desynced desynced:
                _online.Stage = MultiplayerStage.Desynced;
                _message = desynced.IsHostRepair
                    ? $"DESYNC ON TURN {desynced.Turn}  SENDING A SNAPSHOT"
                    : $"DESYNC ON TURN {desynced.Turn}  WAITING FOR THE HOST";
                return;
            case MultiplayerNotice.MatchUpdated updated:
                _online.Match = updated.Match;
                return;
            case MultiplayerNotice.DeadlineChanged deadline:
                _online.DeadlineAt = deadline.DeadlineAt;
                return;
            case MultiplayerNotice.ReadinessChanged readiness:
                if (readiness.Turn != _online.PlanningTurn) return;
                _online.ReadySeats = readiness.Ready;
                _online.SeatedSeats = readiness.Seated;
                return;
            case MultiplayerNotice.OrdersAccepted accepted:
                // A draft needs no announcement; the submission that ends a turn already said so.
                if (accepted.Ready) _message = "ORDERS SENT  WAITING FOR THE OTHER PLAYERS";
                return;
            case MultiplayerNotice.OrdersRefused refused:
                // Not fatal. The turn may have sealed while the player was still planning it, which
                // costs them that turn and nothing else.
                _message = refused.Reason.ToUpperInvariant();
                if (_online.Stage == MultiplayerStage.WaitingForSeal)
                    _online.Status = refused.Reason.ToUpperInvariant();
                return;
            case MultiplayerNotice.ConnectionChanged connection:
                _online.IsConnected = connection.IsConnected;
                if (!connection.IsConnected && connection.Detail is { } detail)
                    _message = detail.ToUpperInvariant();
                return;
            case MultiplayerNotice.MatchFinished:
                // The outcome usually arrives first, with the turn that produced it. This is the
                // server's own word for it, and the case where a match ended without one.
                if (_online.Stage != MultiplayerStage.Finished)
                {
                    _online.Stage = MultiplayerStage.Finished;
                    CloseOnlinePlanning();
                    _message = "MATCH COMPLETE";
                    if (_state?.Outcome is not null) _screens.Show(ClientScreen.Endgame);
                }
                return;
            case MultiplayerNotice.MatchAbandoned:
                _online.Stage = MultiplayerStage.Finished;
                EndOnlineMatch("THE MATCH WAS ABANDONED");
                return;
            case MultiplayerNotice.Failed failed:
                _diagnostics?.Write("multiplayer.failed", new Dictionary<string, string?>
                {
                    ["reason"] = failed.Reason,
                    ["error"] = failed.Error?.ToString(),
                });
                EndOnlineMatch(failed.Reason.ToUpperInvariant());
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
        EndOnlineMatch("LEFT THE MATCH");
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
        _online.Status = status;
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
                ["error"] = finished.Exception?.ToString(),
            }),
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted,
            TaskScheduler.Default);
    }

    private void HandleOnlineClick(Point point)
    {
        FocusOnlineField(point);
        if (_online.Stage == MultiplayerStage.Busy) return;
        if (OnlineHost.Contains(point)) BeginHost();
        else if (OnlineJoin.Contains(point)) BeginJoin();
        else if (OnlineBack.Contains(point)) EndOnlineMatch("HOST A MATCH OR JOIN ONE BY CODE");
    }

    private void HandleLobbyClick(Point point)
    {
        if (LobbyStart.Contains(point)) StartHostedMatch();
        else if (LobbyLeave.Contains(point)) LeaveOnlineMatch();
    }

    /// <summary>
    /// What to say when the player acts on a turn that is no longer theirs to change.
    /// </summary>
    /// <remarks>
    /// Reached whenever there is no handle on a match to mutate, which online means the turn has been
    /// submitted and in a hot-seat match means there is no match at all. The online reading is the
    /// one worth a message, and it is the only one a player can arrive at by pressing a key.
    /// </remarks>
    private const string OnlinePlanningClosed = "THIS TURN IS SENT  WAITING FOR THE OTHER PLAYERS";

    /// <summary>A lobby needs two humans to be worth sealing a turn for.</summary>
    private const int MinimumOnlinePlayers = 2;

    /// <summary>
    /// Five minutes a turn.
    /// </summary>
    /// <remarks>
    /// A timer is what keeps a match from stalling on a player who closed the game, and this is the
    /// only place it is chosen; the protocol takes 0 (no timer) or 30 seconds upwards.
    /// </remarks>
    private const int OnlineTurnTimerSeconds = 300;
}
