using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

public sealed partial class ChaosGame
{
    private static readonly Rectangle OnlineServerField = new(120, 120, 400, 22);
    private static readonly Rectangle OnlineNameField = new(120, 156, 400, 22);
    private static readonly Rectangle OnlineJoinCodeField = new(120, 192, 400, 22);
    private static readonly Rectangle OnlineHost = new(120, 232, 124, 30);
    private static readonly Rectangle OnlineJoin = new(258, 232, 124, 30);
    private static readonly Rectangle OnlineBack = new(396, 232, 124, 30);
    private static readonly Rectangle LobbyStart = new(160, 372, 148, 32);
    private static readonly Rectangle LobbyLeave = new(332, 372, 148, 32);

    private static readonly TimeSpan LobbyPollInterval = TimeSpan.FromSeconds(1);

    private readonly MultiplayerUiState _online = new();
    private readonly HttpClient _http = new();
    private MultiplayerMatchSession? _session;
    private TimeSpan _lobbyPollDue;

    private TextField[] OnlineFields => [_online.Server, _online.DisplayName, _online.JoinCode];

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

    private void FocusOnlineField(Point point)
    {
        var hits = new[]
        {
            (OnlineServerField, _online.Server),
            (OnlineNameField, _online.DisplayName),
            (OnlineJoinCodeField, _online.JoinCode),
        };
        foreach (var (bounds, field) in hits) field.IsFocused = bounds.Contains(point);
    }

    private MultiplayerClient NewClient()
    {
        if (!Uri.TryCreate(_online.Server.Value.Trim(), UriKind.Absolute, out var baseAddress)
            || baseAddress.Scheme is not ("http" or "https"))
        {
            throw new InvalidOperationException("The server address must be an http or https URL.");
        }
        return new MultiplayerClient(_http, new MultiplayerClientOptions(baseAddress));
    }

    /// <summary>
    /// Opens a lobby with the settings the setup screen is showing.
    /// </summary>
    /// <remarks>
    /// The scenario, duration, mentality and portraits ride the server's opaque settings blob,
    /// because the seed alone does not generate a city: every client needs the same choices to
    /// bootstrap the same match.
    /// </remarks>
    private void BeginHost() => RunOnline("HOSTING", async client =>
    {
        var settings = new MultiplayerGameSettings(
            _selectedScenario, _selectedDuration, _selectedAiMentality, _playerPortraits);
        var request = new CreateMatchRequest(
            new MatchSettings(
                $"{_online.DisplayName.Value.Trim()}'S CITY",
                Math.Max(MinimumOnlinePlayers, _selectedPlayerCount),
                OnlineTurnTimerSeconds,
                MatchVisibility.Private,
                settings.ToWire()),
            _online.DisplayName.Value.Trim(),
            Password: null);
        return await client.CreateMatchAsync(request, CancellationToken.None).ConfigureAwait(false);
    });

    private void BeginJoin() => RunOnline("JOINING", async client =>
        await client.JoinAsync(
            new JoinMatchRequest(
                _online.JoinCode.Value.Trim(), _online.DisplayName.Value.Trim(), Password: null),
            CancellationToken.None).ConfigureAwait(false));

    /// <summary>
    /// Runs one lobby call and adopts the membership it answers.
    /// </summary>
    /// <remarks>
    /// It blocks the game loop, which is the honest thing for a menu: nothing can be drawn until
    /// the answer arrives, and a lobby call is one round trip. The turn barrier, which runs for the
    /// length of a match, does not block — see <see cref="MultiplayerMatchSession"/>.
    /// </remarks>
    private void RunOnline(string activity, Func<MultiplayerClient, Task<MembershipView>> call)
    {
        _online.Stage = MultiplayerStage.Busy;
        _online.Status = activity;
        try
        {
            var membership = call(NewClient()).GetAwaiter().GetResult();
            _online.Token = membership.Token;
            _online.OwnPlayerId = membership.Player.Id;
            _online.IsHost = membership.Player.IsHost;
            _online.JoinCodeShown = membership.JoinCode;
            _online.Match = membership.Match;
            _online.Stage = MultiplayerStage.Lobby;
            _online.Status = _online.IsHost ? "READ OUT THE JOIN CODE" : "WAITING FOR THE HOST";
            _screens.Show(ClientScreen.Lobby);
        }
        catch (Exception exception) when (exception is MultiplayerApiException
            or HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            _online.Stage = MultiplayerStage.Connect;
            _online.Status = OnlineFailure(exception);
        }
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
        _lobbyPollDue -= gameTime.ElapsedGameTime;
        if (_lobbyPollDue > TimeSpan.Zero) return;
        _lobbyPollDue = LobbyPollInterval;
        RefreshLobby();
    }

    private void RefreshLobby()
    {
        if (_online.Match is null) return;
        try
        {
            var detail = Handle().GetAsync(CancellationToken.None).GetAwaiter().GetResult();
            _online.Match = detail.Match;
            if (detail.Match.Status == MatchStatus.Running) StartOnlineMatch(detail.Match);
        }
        catch (Exception exception) when (exception is MultiplayerApiException or HttpRequestException)
        {
            _online.Status = OnlineFailure(exception);
        }
    }

    private void StartHostedMatch()
    {
        if (!_online.IsHost || _online.Match is null) return;
        try
        {
            Handle().StartAsync(CancellationToken.None).GetAwaiter().GetResult();
            RefreshLobby();
        }
        catch (Exception exception) when (exception is MultiplayerApiException or HttpRequestException)
        {
            _online.Status = OnlineFailure(exception);
        }
    }

    /// <summary>Bootstraps the match from the server's seed and roster, and opens turn 1.</summary>
    private void StartOnlineMatch(MatchView view)
    {
        if (_definitions is null || _session is not null) return;
        _session = MultiplayerMatchSession.Start(new MultiplayerSessionOptions(
            Handle(), _definitions, view, _online.OwnPlayerId, view.LastEventSeq));
        _online.Match = view;
        _online.Stage = MultiplayerStage.Playing;
        AdoptOnlineTurn(_session.InitialState);
        _message = "PLAN YOUR TURN";
        _screens.Show(ClientScreen.City);
    }

    /// <summary>Takes a fresh authoritative state and starts a turn on a copy of it.</summary>
    private void AdoptOnlineTurn(MatchState authoritative)
    {
        if (_definitions is null || _session is null) return;
        var turn = SpeculativeTurn.For(authoritative, _definitions, _session.Slot);
        _actions = new MatchActions(turn);
        _state = turn.State;
        _online.PlanningTurn = authoritative.Coordinator.Turn;
        _online.Stage = MultiplayerStage.Playing;
        _selectedGangIndex = 0;
        _cursor = _state.FindPlayer(new PlayerId(_session.Slot))?.Gangs
            .FirstOrDefault(gang => gang.IsActive)?.SectorId ?? _cursor;
    }

    /// <summary>Sends what the player planned and marks them ready; the turn seals on the last one.</summary>
    private void SubmitOnlineTurn()
    {
        if (_session is null || _actions?.OnlineTurn is not { } turn) return;
        try
        {
            _session.SubmitOrdersAsync(
                    _online.PlanningTurn, turn.Build(), ready: true, CancellationToken.None)
                .GetAwaiter().GetResult();
            _online.Stage = MultiplayerStage.WaitingForSeal;
            _message = "WAITING FOR THE OTHER PLAYERS";
        }
        catch (Exception exception) when (exception is MultiplayerApiException or HttpRequestException)
        {
            _message = OnlineFailure(exception);
        }
    }

    /// <summary>
    /// Drains what the session has to say, on the game thread.
    /// </summary>
    /// <remarks>
    /// Every notice carries a state the interface owns outright, so adopting one is an assignment
    /// rather than a lock: the session keeps its own authoritative copy and never hands it over.
    /// </remarks>
    private void PumpOnlineNotices()
    {
        if (_session is null) return;
        while (_session.TryDequeueNotice(out var notice)) Apply(notice);
    }

    private void Apply(MultiplayerNotice notice)
    {
        switch (notice)
        {
            case MultiplayerNotice.TurnResolved resolved:
                AdoptOnlineTurn(resolved.State);
                _message = $"TURN {resolved.Turn} RESOLVED";
                if (_state?.Outcome is not null) _screens.Show(ClientScreen.Endgame);
                else _screens.Show(ClientScreen.City);
                return;
            case MultiplayerNotice.Resynced resynced:
                AdoptOnlineTurn(resynced.State);
                _message = $"RESYNCED ON TURN {resynced.Turn}";
                _screens.Show(ClientScreen.City);
                return;
            case MultiplayerNotice.Desynced desynced:
                _online.Stage = MultiplayerStage.Desynced;
                _message = desynced.IsHostRepair
                    ? $"DESYNC ON TURN {desynced.Turn}  SENDING A SNAPSHOT"
                    : $"DESYNC ON TURN {desynced.Turn}  WAITING FOR THE HOST";
                return;
            case MultiplayerNotice.MatchUpdated updated:
                _online.Match = updated.Match;
                if (_session is null && updated.Match.Status == MatchStatus.Running)
                    StartOnlineMatch(updated.Match);
                return;
            case MultiplayerNotice.DeadlineChanged deadline:
                _online.DeadlineAt = deadline.DeadlineAt;
                return;
            case MultiplayerNotice.MatchFinished:
                _message = "MATCH COMPLETE";
                _screens.Show(ClientScreen.Endgame);
                return;
            case MultiplayerNotice.Failed failed:
                _diagnostics?.Write("multiplayer.failed", new Dictionary<string, string?>
                {
                    ["reason"] = failed.Reason,
                    ["error"] = failed.Error?.ToString(),
                });
                _message = failed.Reason;
                EndOnlineMatch();
                return;
            default:
                return;
        }
    }

    /// <summary>Leaves the match and forgets the token; the seat becomes a computer player.</summary>
    private void LeaveOnlineMatch()
    {
        try
        {
            if (_online.Match is not null && _online.Token.Length > 0)
            {
                Handle().LeaveAsync(CancellationToken.None).GetAwaiter().GetResult();
            }
        }
        catch (Exception exception) when (exception is MultiplayerApiException or HttpRequestException)
        {
            // Leaving is best-effort: the seat becomes a computer player either way, and a player
            // who has decided to go should not be held in a lobby by a network error.
            _diagnostics?.Write("multiplayer.leave.failed", new Dictionary<string, string?>
            {
                ["reason"] = exception.Message,
            });
        }
        EndOnlineMatch();
    }

    private void EndOnlineMatch()
    {
        _session?.DisposeAsync().AsTask().GetAwaiter().GetResult();
        _session = null;
        _online.Reset();
        _online.Status = "LEFT THE MATCH";
        _screens.Show(ClientScreen.Title);
    }

    private void HandleOnlineClick(Point point)
    {
        FocusOnlineField(point);
        if (_online.Stage == MultiplayerStage.Busy) return;
        if (OnlineHost.Contains(point)) BeginHost();
        else if (OnlineJoin.Contains(point)) BeginJoin();
        else if (OnlineBack.Contains(point)) _screens.Show(ClientScreen.Title);
    }

    private void HandleLobbyClick(Point point)
    {
        if (LobbyStart.Contains(point)) StartHostedMatch();
        else if (LobbyLeave.Contains(point)) LeaveOnlineMatch();
    }

    private MatchHandle Handle() =>
        NewClient().WithToken(_online.Token).Match(
            _online.Match?.Id ?? throw new InvalidOperationException("There is no match to act on."));

    /// <summary>Text a player can act on, rather than an exception's own words.</summary>
    private static string OnlineFailure(Exception exception) => exception switch
    {
        MultiplayerApiException { Reason: "invalid_token" } => "THAT MEMBERSHIP IS NO LONGER VALID",
        MultiplayerApiException { Reason: "unknown_match" } => "NO MATCH WITH THAT CODE",
        MultiplayerApiException { Reason: "match_full" } => "THAT LOBBY IS FULL",
        MultiplayerApiException { Reason: "host_only" } => "ONLY THE HOST CAN DO THAT",
        MultiplayerApiException api => api.Message.ToUpperInvariant(),
        InvalidOperationException invalid => invalid.Message.ToUpperInvariant(),
        _ => "COULD NOT REACH THE SERVER",
    };

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
