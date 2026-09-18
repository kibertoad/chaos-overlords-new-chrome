using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>Where the online flow has got to, which is what the two online screens draw from.</summary>
internal enum MultiplayerStage
{
    /// <summary>Choosing a server and a name, and whether to host or join.</summary>
    Connect,

    /// <summary>Browsing locally remembered unfinished online memberships.</summary>
    History,

    /// <summary>Browsing public waiting and ongoing sessions.</summary>
    Discover,

    /// <summary>Choosing which never-human computer empire to take over.</summary>
    LateJoinSeat,

    /// <summary>A request is in flight; the player can only wait.</summary>
    Busy,

    /// <summary>Seated in a lobby that has not started.</summary>
    Lobby,

    /// <summary>The match is running and the interface is the ordinary one.</summary>
    Playing,

    /// <summary>Planning is submitted; the turn seals when everyone is ready or the clock runs out.</summary>
    WaitingForSeal,

    /// <summary>Clients disagreed about a turn and the match is paused until it is repaired.</summary>
    Desynced,

    /// <summary>The match is over. Nothing more will seal, and there is nothing left to plan.</summary>
    Finished,
}

internal enum OnlineConnectRole
{
    Host,
    Join
}

/// <summary>
/// Everything the online screens need that is not the match itself.
/// </summary>
/// <remarks>
/// It is a plain mutable bag on purpose: the game loop reads it every frame to draw, and the
/// sessions' notices are drained into it on the same thread, so nothing here is shared with the
/// background tasks that talk to the server.
/// </remarks>
internal sealed class MultiplayerUiState
{
    internal Dictionary<string, TakeoverVotePrompt> TakeoverVotes { get; } = new(StringComparer.Ordinal);

    internal TakeoverVotePrompt? CurrentTakeoverVote => TakeoverVotes.Values
        .OrderBy(vote => vote.Turn)
        .ThenBy(vote => vote.PlayerId, StringComparer.Ordinal)
        .FirstOrDefault();
    internal MultiplayerStage Stage { get; set; } = MultiplayerStage.Connect;
    internal int RecoverySelection { get; set; }
    internal int DiscoverySelection { get; set; }
    internal int DiscoveryStatusFilter { get; set; }
    internal int DiscoveryScenarioFilter { get; set; } = -1;
    internal int DiscoveryAiFilter { get; set; } = -1;

    /// <summary>Which filter dropdown is open, or -1 when none is.</summary>
    internal int OpenDiscoveryFilter { get; set; } = -1;

    /// <summary>The row an open dropdown has under the keyboard cursor.</summary>
    internal int DiscoveryFilterHighlight { get; set; }

    internal int LateJoinSeatSelection { get; set; }
    internal LobbyListing? PendingLateJoin { get; set; }

    /// <summary>
    /// Whether this client took its seat in a match that was already running.
    /// </summary>
    /// <remarks>
    /// The session needs it at bootstrap: a late joiner is on the roster before it has built a
    /// city, so it has to take the host's snapshot and the event log rather than generate one that
    /// seats itself where every peer seated a computer player.
    /// </remarks>
    internal bool JoinedInProgress { get; set; }
    internal IReadOnlyList<LobbyListing> Listings { get; set; } = [];

    internal OnlineServiceMode Service { get; set; } = OnlineServiceMode.Central;
    internal OnlineConnectRole Role { get; set; } = OnlineConnectRole.Host;
    internal bool PublicListing { get; set; }
    internal bool AllowLateJoin { get; set; }

    internal TextField Server { get; } = new(
        "SERVER", 96, GamePreferences.DefaultCustomMultiplayerServer);
    internal TextField DisplayName { get; } = new("NAME", 32, "PLAYER");
    /// <summary>Empty until the lobby is made: the host is given a name, and renames it there.</summary>
    internal TextField SessionName { get; } = new("SESSION NAME", 64);
    internal TextField JoinCode { get; } = new("JOIN CODE", 8);

    /// <summary>
    /// The lobby password, optional on both sides.
    /// </summary>
    /// <remarks>
    /// Empty means none: hosting with it blank opens an unprotected lobby, and joining one that is
    /// protected without it is refused by the server rather than guessed at here.
    /// </remarks>
    internal TextField Password { get; } = new("PASSWORD (OPTIONAL)", 64);

    /// <summary>The lobby as the server last described it, or null before there is one.</summary>
    internal MatchView? Match { get; set; }

    /// <summary>The code the host reads out; empty until there is a lobby.</summary>
    internal string JoinCodeShown { get; set; } = string.Empty;

    /// <summary>
    /// The password this client is seated with, empty when the session has none.
    /// </summary>
    /// <remarks>
    /// Kept apart from <see cref="Password"/>, which is the connect screen's field and holds
    /// whatever was last typed into it. This is what the session was actually opened or entered
    /// with, so the escape menu can put it in front of the player who has to read it out, and a
    /// private lobby the host never set one on shows none.
    /// </remarks>
    internal string PasswordShown { get; set; } = string.Empty;

    /// <summary>Whether this client is the host, and so the one that repairs a desync.</summary>
    internal bool IsHost { get; set; }

    /// <summary>The turn the player is planning, which is the one their orders are submitted for.</summary>
    internal int PlanningTurn { get; set; }

    /// <summary>When the open turn seals regardless of readiness, or null without a timer.</summary>
    internal DateTimeOffset? DeadlineAt { get; set; }

    /// <summary>Seats that have finished planning the open turn, and how many there are to wait on.</summary>
    internal int ReadySeats { get; set; }

    /// <summary>How many seats the server waits on before it seals on readiness alone.</summary>
    internal int SeatedSeats { get; set; }

    /// <summary>
    /// Whether the server is answering.
    /// </summary>
    /// <remarks>
    /// Both sessions reconnect on their own, so this is shown rather than acted on. A player who can
    /// see the connection is down knows why nothing is happening; one who cannot is left wondering
    /// which opponent is taking so long.
    /// </remarks>
    internal bool IsConnected { get; set; } = true;

    /// <summary>Recent automatic reconnect attempts, newest last, for the modal status log.</summary>
    internal List<string> ReconnectLog { get; } = [];
    internal int ReconnectAttempt { get; set; }

    /// <summary>Digest of the last draft queued for the server, or null before the first change.</summary>
    internal string? SentOrderDigest { get; set; }

    /// <summary>The last thing that went wrong, for the player to read.</summary>
    internal string Status { get; set; } = string.Empty;

    /// <summary>The complete failed online request shown by the modal and copied verbatim.</summary>
    internal string ConnectionError { get; set; } = string.Empty;

    /// <summary>Feedback from the error modal's clipboard action.</summary>
    internal string ConnectionErrorCopyStatus { get; set; } = string.Empty;

    /// <summary>The result of the selected service's lightweight health check.</summary>
    internal string ServerStatus { get; set; } = string.Empty;

    /// <summary>
    /// Whether building this match from the server's description failed.
    /// </summary>
    /// <remarks>
    /// Sticky, because the lobby keeps being re-read while it is on screen and the answer will not
    /// change: a seed, roster or settings blob this build cannot make a city from is the same one a
    /// second later. Without it the player watches the same refusal reappear every second with no way
    /// to tell whether anything is being attempted.
    /// </remarks>
    internal bool BootstrapFailed { get; set; }

    /// <summary>Forgets everything a finished match leaves behind, so nothing outlives it.</summary>
    internal void Reset()
    {
        Stage = MultiplayerStage.Connect;
        RecoverySelection = 0;
        DiscoverySelection = 0;
        OpenDiscoveryFilter = -1;
        DiscoveryFilterHighlight = 0;
        LateJoinSeatSelection = 0;
        PendingLateJoin = null;
        JoinedInProgress = false;
        Listings = [];
        Role = OnlineConnectRole.Host;
        Match = null;
        JoinCodeShown = string.Empty;
        PasswordShown = string.Empty;
        IsHost = false;
        PlanningTurn = 0;
        DeadlineAt = null;
        ReadySeats = 0;
        SeatedSeats = 0;
        IsConnected = true;
        ReconnectLog.Clear();
        ReconnectAttempt = 0;
        SentOrderDigest = null;
        BootstrapFailed = false;
        ConnectionError = string.Empty;
        ConnectionErrorCopyStatus = string.Empty;
        ServerStatus = string.Empty;
        TakeoverVotes.Clear();
        Password.Set(string.Empty);
        JoinCode.Set(string.Empty);
        SessionName.Set(string.Empty);
    }

    /// <summary>Whether the player may still change the turn they are planning.</summary>
    /// <remarks>
    /// Only while the turn is genuinely theirs to plan. Once it is submitted the document the server
    /// holds is the turn, so a command queued after that would show in the interface, never be sent,
    /// and leave the player believing they ordered something they did not.
    /// </remarks>
    internal bool PlanningIsOpen => Stage == MultiplayerStage.Playing;
}

internal sealed record TakeoverVotePrompt(
    string PlayerId,
    string DisplayName,
    int Turn,
    IReadOnlyDictionary<string, TakeoverChoice> Votes);
