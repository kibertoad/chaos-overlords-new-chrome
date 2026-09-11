using Rechaos.Multiplayer.Generated;

namespace Rechaos.Game;

/// <summary>Where the online flow has got to, which is what the two online screens draw from.</summary>
internal enum MultiplayerStage
{
    /// <summary>Choosing a server and a name, and whether to host or join.</summary>
    Connect,

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
    internal MultiplayerStage Stage { get; set; } = MultiplayerStage.Connect;

    internal TextField Server { get; } = new("SERVER", 96, "http://localhost:8787");
    internal TextField DisplayName { get; } = new("NAME", 32, "PLAYER");
    internal TextField JoinCode { get; } = new("JOIN CODE", 8);

    /// <summary>
    /// The lobby password, optional on both sides.
    /// </summary>
    /// <remarks>
    /// Empty means none: hosting with it blank opens an unprotected lobby, and joining one that is
    /// protected without it is refused by the server rather than guessed at here.
    /// </remarks>
    internal TextField Password { get; } = new("PASSWORD (OPTIONAL)", 64) { IsMasked = true };

    /// <summary>The lobby as the server last described it, or null before there is one.</summary>
    internal MatchView? Match { get; set; }

    /// <summary>The code the host reads out; empty until there is a lobby.</summary>
    internal string JoinCodeShown { get; set; } = string.Empty;

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

    /// <summary>
    /// How many ops had been sent to the server the last time a draft went out.
    /// </summary>
    /// <remarks>
    /// The order document is a whole-document replace, so a draft costs one small request and saves a
    /// player's planning from a deadline they did not notice. Counting ops is enough to know whether
    /// anything has changed since: the document only ever grows within a turn.
    /// </remarks>
    internal int SentOpCount { get; set; }

    /// <summary>Time left before the next draft of the open turn is sent.</summary>
    internal TimeSpan DraftDue { get; set; }

    /// <summary>The last thing that went wrong, for the player to read.</summary>
    internal string Status { get; set; } = string.Empty;

    /// <summary>Forgets everything a finished match leaves behind, so nothing outlives it.</summary>
    internal void Reset()
    {
        Stage = MultiplayerStage.Connect;
        Match = null;
        JoinCodeShown = string.Empty;
        IsHost = false;
        PlanningTurn = 0;
        DeadlineAt = null;
        ReadySeats = 0;
        SeatedSeats = 0;
        IsConnected = true;
        SentOpCount = 0;
        DraftDue = TimeSpan.Zero;
        Password.Set(string.Empty);
        JoinCode.Set(string.Empty);
    }

    /// <summary>Whether the player may still change the turn they are planning.</summary>
    /// <remarks>
    /// Only while the turn is genuinely theirs to plan. Once it is submitted the document the server
    /// holds is the turn, so a command queued after that would show in the interface, never be sent,
    /// and leave the player believing they ordered something they did not.
    /// </remarks>
    internal bool PlanningIsOpen => Stage == MultiplayerStage.Playing;
}
