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
    /// The session stopped and will not recover on its own.
    /// </summary>
    /// <param name="Reason">Text for the player; the exception is for the log.</param>
    public sealed record Failed(string Reason, Exception? Error) : MultiplayerNotice;
}
