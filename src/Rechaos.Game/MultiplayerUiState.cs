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
    /// <summary>The seat roster before the server has said anything about one.</summary>
    internal static readonly IReadOnlySet<int> NoSeats = new HashSet<int>();

    /// <summary>
    /// The absence votes the server has open, by the seat each one is about.
    /// </summary>
    /// <remarks>
    /// Private, and reached only through <see cref="RecordTakeoverVote"/>,
    /// <see cref="CloseTakeoverVote"/> and <see cref="ConcludeMatch"/>, so that the one thing that
    /// has to stay true of it cannot be broken from outside: every prompt in here is a question the
    /// player can still answer. The two views the interface draws from —
    /// <see cref="CurrentTakeoverVote"/> and <see cref="OwnTakeoverVote"/> — are then correct by
    /// construction rather than by each reader remembering to ask whether the match is still on.
    /// </remarks>
    private readonly Dictionary<string, TakeoverVotePrompt> _takeoverVotes =
        new(StringComparer.Ordinal);

    /// <summary>This client's own player id in the running match, or empty when there is none.</summary>
    internal string SelfPlayerId { get; set; } = string.Empty;

    /// <summary>
    /// The absence the player is being asked to vote on, if any.
    /// </summary>
    /// <remarks>
    /// Never the player's own seat — see <see cref="TakeoverVotePolicy.SeatToVoteOn"/>, which is
    /// where that rule and its reasons live. What their own absence means for them is said on the
    /// turn status line instead; see <see cref="OwnTakeoverVote"/>.
    /// </remarks>
    internal TakeoverVotePrompt? CurrentTakeoverVote =>
        TakeoverVotePolicy.SeatToVoteOn(
                _takeoverVotes.Values.Select(vote => (vote.PlayerId, vote.Turn)), SelfPlayerId)
            is { } playerId && _takeoverVotes.TryGetValue(playerId, out var prompt)
            ? prompt
            : null;

    /// <summary>The open vote about this client's own seat, when the player missed a deadline.</summary>
    internal TakeoverVotePrompt? OwnTakeoverVote =>
        SelfPlayerId.Length > 0 && _takeoverVotes.TryGetValue(SelfPlayerId, out var vote)
            ? vote
            : null;

    /// <summary>
    /// Takes note of an absence vote the server has opened or retallied.
    /// </summary>
    /// <remarks>
    /// Dropped once the match is over. Delivery is at least once and the log is not ordered against
    /// the match ending, so the announcement of a vote — or of one more player casting theirs — can
    /// arrive after the outcome has: a player who runs out of time on the turn that decides the
    /// match is marked absent by that very seal. There is nothing left for such a vote to decide,
    /// and the server refuses one cast on a match that is not running, so drawing it would put a
    /// modal with two dead buttons over the endgame and no way to dismiss it.
    /// </remarks>
    internal void RecordTakeoverVote(TakeoverVotePrompt prompt)
    {
        ArgumentNullException.ThrowIfNull(prompt);
        if (Stage == MultiplayerStage.Finished) return;
        _takeoverVotes[prompt.PlayerId] = prompt;
    }

    /// <summary>Forgets the vote about a seat, once the server has settled it.</summary>
    internal void CloseTakeoverVote(string playerId) => _takeoverVotes.Remove(playerId);

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
    /// <summary>The public sessions the last browse found, with their settings already read.</summary>
    internal IReadOnlyList<DiscoveredListing> Listings { get; set; } = [];

    internal OnlineServiceMode Service { get; set; } = OnlineServiceMode.Central;
    internal OnlineConnectRole Role { get; set; } = OnlineConnectRole.Host;
    /// <summary>
    /// Whether a hosted session is listed for anyone to browse, rather than gated by its join code.
    /// </summary>
    /// <remarks>
    /// Listed by default: a session nobody can find is one that only friends told the code can
    /// join, and a lobby browser with nothing in it is what a private default leaves every player
    /// who opens it. A host who wants a code-only session says so on the same screen, before the
    /// lobby is opened, and can change it in the lobby afterwards.
    /// </remarks>
    internal bool PublicListing { get; set; } = true;
    internal bool AllowLateJoin { get; set; }

    /// <summary>
    /// The overlord face this player takes into the session they create or join.
    /// </summary>
    /// <remarks>
    /// Sent once, with the request that claims the seat, and never changed afterwards: the roster is
    /// what every client generates its city from, and the setup a city was generated from is part of
    /// the state hash a turn is settled on, so a face that moved mid-match would read as a desync.
    /// </remarks>
    internal short Portrait { get; set; }

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

    /// <summary>The seats that have finished planning the open turn.</summary>
    internal IReadOnlySet<int> ReadySlots { get; set; } = NoSeats;

    /// <summary>The seats the server waits on before it seals on readiness alone.</summary>
    internal IReadOnlySet<int> AwaitedSlots { get; set; } = NoSeats;

    /// <summary>How many seats have finished planning the open turn.</summary>
    internal int ReadySeats => ReadySlots.Count;

    /// <summary>How many seats the server waits on before it seals on readiness alone.</summary>
    internal int SeatedSeats => AwaitedSlots.Count;

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

    /// <summary>Whether the ready document is still waiting for the server's HTTP acknowledgement.</summary>
    internal bool ReadySubmissionPending { get; set; }

    /// <summary>Whether the server explicitly acknowledged this turn's ready document.</summary>
    internal bool ReadySubmissionAcknowledged { get; set; }

    /// <summary>When an acknowledged all-ready turn first failed to produce its sealed successor.</summary>
    internal DateTimeOffset? ResolutionExpectedSince { get; set; }

    /// <summary>A refused submission for the turn still shown on the city screen.</summary>
    internal string TurnSyncError { get; set; } = string.Empty;

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

    /// <summary>
    /// Settles the interface on a match that has ended.
    /// </summary>
    /// <remarks>
    /// The three things that stop being true the moment a match is over, in one place, so that no
    /// path which ends a match can carry one of them into the endgame. Nothing will seal, so there
    /// is no clock; and no absence vote can be answered, so none is still being asked. A prompt left
    /// standing over the endgame owned the keyboard and the mouse for a modal whose buttons the
    /// server had already begun refusing, which left the player unable to get past the endgame at
    /// all.
    /// </remarks>
    internal void ConcludeMatch()
    {
        Stage = MultiplayerStage.Finished;
        DeadlineAt = null;
        _takeoverVotes.Clear();
    }

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
        // Back to being discoverable: a lobby this client joined may have been code-only, and its
        // choice was adopted onto the screens that host the next one.
        PublicListing = true;
        Role = OnlineConnectRole.Host;
        Portrait = 0;
        Match = null;
        JoinCodeShown = string.Empty;
        PasswordShown = string.Empty;
        IsHost = false;
        PlanningTurn = 0;
        DeadlineAt = null;
        ReadySlots = NoSeats;
        AwaitedSlots = NoSeats;
        IsConnected = true;
        ReconnectLog.Clear();
        ReconnectAttempt = 0;
        SentOrderDigest = null;
        ReadySubmissionPending = false;
        ReadySubmissionAcknowledged = false;
        ResolutionExpectedSince = null;
        TurnSyncError = string.Empty;
        BootstrapFailed = false;
        ConnectionError = string.Empty;
        ConnectionErrorCopyStatus = string.Empty;
        ServerStatus = string.Empty;
        _takeoverVotes.Clear();
        SelfPlayerId = string.Empty;
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

    /// <summary>Whether the player's turn is sent and only the other seats are still awaited.</summary>
    /// <remarks>
    /// The turn is no longer theirs to change, but the state it was planned against is still the
    /// state every client will resolve from, so it remains worth reading while the wait runs. The
    /// interface uses this to keep the read-only views open without reopening
    /// <see cref="PlanningIsOpen"/>, which is the narrower question every mutation asks.
    /// </remarks>
    internal bool PlanningIsSubmitted => Stage == MultiplayerStage.WaitingForSeal;
}

internal sealed record TakeoverVotePrompt(
    string PlayerId,
    string DisplayName,
    int Turn,
    IReadOnlyDictionary<string, TakeoverChoice> Votes);
