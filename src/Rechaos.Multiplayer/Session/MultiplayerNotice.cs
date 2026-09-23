using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Session;

public enum TakeoverChoice
{
    Computer,
    Wait,
}

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

    /// <summary>A vote is open, or its latest votes changed.</summary>
    public sealed record TakeoverVoteChanged(
        string PlayerId,
        int Turn,
        IReadOnlyDictionary<string, TakeoverChoice> Votes) : MultiplayerNotice;

    /// <summary>The player returned or the vote transferred their seat, closing the prompt.</summary>
    public sealed record TakeoverVoteClosed(string PlayerId, bool ComputerControl) : MultiplayerNotice;

    /// <summary>
    /// The session reconstructed the authoritative state — at startup, or after the live stream
    /// proved to have skipped something — and recovered this seat's current whole-document
    /// submission, if one exists.
    /// </summary>
    /// <param name="Match">The match as the server described it at the moment of reconstruction.</param>
    /// <param name="State">A copy of the reconstructed state; the interface owns it outright.</param>
    /// <param name="Submission">What the server holds for this seat on the open turn.</param>
    /// <param name="Turn">
    /// The planning copy for the open turn, with the saved draft already replayed onto it, or null
    /// when the match is over. Built by the session because building it is also what proves the
    /// draft still applies: a draft that does not is a protocol failure, and it is refused as one
    /// rather than thrown on the game thread.
    /// </param>
    public sealed record Resumed(
        MatchView Match,
        MatchState State,
        OwnSubmissionView Submission,
        SpeculativeTurn? Turn) : MultiplayerNotice;

    /// <summary>
    /// A turn resolved on every client that has reported so far, and here is the state after it.
    /// </summary>
    /// <param name="Turn">The turn that was applied.</param>
    /// <param name="State">A copy for the interface; the session keeps its own.</param>
    /// <param name="StateHash">What this client reported for that turn.</param>
    /// <param name="Planning">
    /// The copy the player plans the NEXT turn on, built here rather than on the game thread, or
    /// null when the turn ended the match and there is no next one.
    /// <para>
    /// Building it is a full native save and load of the match plus a walk of the coordinator up to
    /// the local seat, and it used to happen in the frame the new turn appeared — on top of a clone
    /// this notice had already made, at the moment the player is looking at the screen. The restore
    /// path always did it this way and handed the finished turn over; this is the same handover for
    /// the ordinary case.
    /// </para>
    /// </param>
    /// <param name="IncludedOwnOrders">
    /// Whether the sealed set carried a document for this client's seat.
    /// </param>
    /// <remarks>
    /// <para>
    /// <paramref name="IncludedOwnOrders"/> is the sealed set's own word for it, not what this
    /// client believes it sent. A turn that seals on the clock takes whatever draft had reached the
    /// server, and a draft still in flight when the deadline passed did not — so the interface can
    /// only tell the player which of those happened by reading the set the turn was actually
    /// resolved from.
    /// </para>
    /// </remarks>
    public sealed record TurnResolved(
        int Turn,
        MatchState State,
        string StateHash,
        bool IncludedOwnOrders,
        SpeculativeTurn? Planning) : MultiplayerNotice;

    /// <summary>
    /// Clients disagreed about the state after a turn, and the match is paused until it is repaired.
    /// </summary>
    /// <param name="Turn">The disputed turn.</param>
    /// <param name="IsRepairing">
    /// True when this client is the one posting the snapshot everyone else converges on: it holds
    /// the state the players reported most often, and the server will accept it from this seat.
    /// False means waiting for somebody who does, which is not a failure and never ends a session.
    /// </param>
    /// <param name="Details">Short hashes reported by each client, suitable for diagnostics.</param>
    public sealed record Desynced(
        int Turn,
        bool IsHost,
        bool IsRepairing,
        string Details) : MultiplayerNotice;

    /// <summary>
    /// A vote this client cast did not reach the server, so the question is still open.
    /// </summary>
    /// <remarks>
    /// The interface fires a vote and forgets it, which is right — nothing about the turn waits on
    /// the round trip — but "forgot" used to mean a diagnostics line and nothing else, so a
    /// <c>403 not_active</c> or an exhausted retry window left the modal saying the vote had been
    /// cast. The player is the only one who can cast it again.
    /// </remarks>
    public sealed record TakeoverVoteFailed(
        string PlayerId,
        TakeoverChoice Choice,
        string Reason) : MultiplayerNotice;

    /// <summary>A repaired state arrived and was adopted; this client is back in step.</summary>
    public sealed record Resynced(
        int Turn,
        MatchState State,
        string StateHash,
        SpeculativeTurn? Planning) : MultiplayerNotice;

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
    /// Which seats have said they are done with the open turn.
    /// </summary>
    /// <remarks>
    /// Seats rather than a tally, because the interface marks the opponents that are still
    /// drafting under their own portraits: a count says how many the turn is waiting on, and the
    /// player wants to know which.
    /// </remarks>
    /// <param name="Turn">The turn being counted.</param>
    /// <param name="ReadySlots">Seats that have submitted and marked themselves ready.</param>
    /// <param name="AwaitedSlots">
    /// Seats the server is waiting on, which is the roster it seals against.
    /// </param>
    public sealed record ReadinessChanged(
        int Turn,
        IReadOnlySet<int> ReadySlots,
        IReadOnlySet<int> AwaitedSlots) : MultiplayerNotice
    {
        /// <summary>How many seats have finished planning the turn.</summary>
        public int Ready => ReadySlots.Count;

        /// <summary>How many seats the turn seals against.</summary>
        public int Seated => AwaitedSlots.Count;
    }

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
    /// <param name="Turn">The turn the document was for.</param>
    /// <param name="Reason">What the server said, for the player.</param>
    /// <param name="ReadinessWithdrawn">
    /// The refused document was this player's finished turn, and the server refused the document
    /// itself rather than the turn: it failed validation or was too large. The turn is still open
    /// and still waiting on this seat, and the session has stopped carrying readiness forward for
    /// it, so the player can change the turn and end it again.
    /// </param>
    public sealed record OrdersRefused(int Turn, string Reason, bool ReadinessWithdrawn = false)
        : MultiplayerNotice;

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
    public sealed record ConnectionChanged(
        bool IsConnected,
        string? Detail,
        int Attempt = 0) : MultiplayerNotice;

    /// <summary>
    /// The session stopped and will not recover on its own.
    /// </summary>
    /// <param name="Reason">Text for the player; the exception is for the log.</param>
    /// <param name="Operation">The last protocol operation on the failing worker, if known.</param>
    /// <param name="LastEventSequence">The most recent event sequence the session applied.</param>
    public sealed record Failed(
        string Reason,
        Exception? Error,
        string? Operation = null,
        int? LastEventSequence = null) : MultiplayerNotice;
}
