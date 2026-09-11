using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

/// <summary>
/// Something the session wants the interface to know about, polled from the game loop.
/// </summary>
/// <remarks>
/// The session runs its protocol on a background task while MonoGame draws frames on another, so
/// nothing is handed over by callback. Notices are queued and drained on the game thread, and every
/// state they carry is a copy the interface owns outright.
/// </remarks>
public abstract record MultiplayerNotice
{
    private MultiplayerNotice()
    {
    }

    /// <summary>The lobby's roster or status changed.</summary>
    public sealed record MatchUpdated(MatchView Match) : MultiplayerNotice;

    /// <summary>
    /// A turn resolved on every client that has reported so far, and here is the state after it.
    /// </summary>
    /// <param name="Turn">The turn that was applied.</param>
    /// <param name="State">A copy for the interface; the session keeps its own.</param>
    /// <param name="StateHash">What this client reported for that turn.</param>
    public sealed record TurnResolved(int Turn, MatchState State, string StateHash) : MultiplayerNotice;

    /// <summary>
    /// Clients disagreed about the state after a turn, and the match is paused until it is repaired.
    /// </summary>
    /// <param name="Turn">The disputed turn.</param>
    /// <param name="IsHostRepair">
    /// True when this client is the host and is therefore the one that has to upload the snapshot
    /// everyone else converges on.
    /// </param>
    public sealed record Desynced(int Turn, bool IsHostRepair) : MultiplayerNotice;

    /// <summary>A repaired state arrived and was adopted; this client is back in step.</summary>
    public sealed record Resynced(int Turn, MatchState State, string StateHash) : MultiplayerNotice;

    /// <summary>The turn's countdown moved, because the match resumed after a desync pause.</summary>
    public sealed record DeadlineChanged(int Turn, DateTimeOffset? DeadlineAt) : MultiplayerNotice;

    /// <summary>Every active player reported a finished match.</summary>
    public sealed record MatchFinished : MultiplayerNotice;

    /// <summary>
    /// The server gave up on the match.
    /// </summary>
    /// <remarks>
    /// As final as a finish and easier to miss: nobody is going to seal another turn, so a client
    /// that did not act on this would wait out a match that has already stopped existing.
    /// </remarks>
    public sealed record MatchAbandoned : MultiplayerNotice;

    /// <summary>
    /// How many seats have said they are done with the open turn.
    /// </summary>
    /// <param name="Turn">The turn being counted.</param>
    /// <param name="Ready">Seats that have submitted and marked themselves ready.</param>
    /// <param name="Seated">Seats the server is waiting on, which is the roster it seals against.</param>
    public sealed record ReadinessChanged(int Turn, int Ready, int Seated) : MultiplayerNotice;

    /// <summary>The server took this client's order document for a turn.</summary>
    /// <param name="Turn">The turn it was taken for.</param>
    /// <param name="Ready">Whether it also said the player is done planning.</param>
    public sealed record OrdersAccepted(int Turn, bool Ready) : MultiplayerNotice;

    /// <summary>
    /// The server would not take this client's order document.
    /// </summary>
    /// <remarks>
    /// Not fatal. The ordinary cause is a turn that sealed while the player was still planning it,
    /// which costs them that turn and nothing more — so this says what happened and the match
    /// carries on.
    /// </remarks>
    public sealed record OrdersRefused(int Turn, string Reason) : MultiplayerNotice;

    /// <summary>
    /// Whether the server is answering.
    /// </summary>
    /// <remarks>
    /// Raised on a change, not per attempt. A dropped stream and a call being retried both reconnect
    /// on their own, so this exists to be shown rather than acted on: a player who can see that the
    /// connection is down knows why nothing is happening, and a player who cannot is left guessing
    /// whether they are waiting on a peer.
    /// </remarks>
    /// <param name="IsConnected">Whether the last attempt reached the server.</param>
    /// <param name="Detail">What went wrong, when it did not.</param>
    public sealed record ConnectionChanged(bool IsConnected, string? Detail) : MultiplayerNotice;

    /// <summary>
    /// The session stopped and will not recover on its own.
    /// </summary>
    /// <param name="Reason">Text for the player; the exception is for the log.</param>
    public sealed record Failed(string Reason, Exception? Error) : MultiplayerNotice;
}
