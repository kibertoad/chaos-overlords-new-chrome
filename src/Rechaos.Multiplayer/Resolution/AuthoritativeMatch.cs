using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Multiplayer.Resolution;

/// <summary>
/// One online match held by a party that resolves it without playing a seat: the coordination
/// server's turn resolver (docs/MULTIPLAYER.md, "Resolving turns on the server").
/// </summary>
/// <remarks>
/// <para>
/// Every step goes through the code a client runs for the same fact, so a resolver and a client
/// that are given the same facts in the same order reach the same state hash: the bootstrap is
/// <see cref="MatchBootstrapFactory"/> followed by <see cref="CommandPhase.Enter"/>, the event log
/// is folded by <see cref="MatchHistory"/>, which a reconnecting client's session folds it with
/// (sealed turns through <see cref="SealedTurnApplier.Apply"/>, seats changing hands through
/// <see cref="SeatControl.HandOver"/>), and a snapshot is the archive
/// <see cref="MatchStateClone.ToBase64"/> writes for a desync repair. Nothing here talks to a
/// network. The caller feeds the log's events in order, each seal with its sealed set.
/// </para>
/// <para>
/// The coordination server is TypeScript, so it reaches this class through the WebAssembly build
/// in <c>src/Rechaos.Resolver.Wasm</c>, which takes and returns the wire's JSON.
/// </para>
/// </remarks>
public sealed class AuthoritativeMatch
{
    private readonly MatchHistory _history;

    private AuthoritativeMatch(MatchHistory history) => _history = history;

    private MatchReplayRecorder Replay => _history.Replay;

    /// <summary>The session version this build resolves: see <see cref="MultiplayerSessionVersion"/>.</summary>
    /// <remarks>
    /// A resolver can stand in for the clients only on a match stored under the session version it
    /// plays, because that number is what names the rules, the order schema and the state hashing.
    /// </remarks>
    public static int SessionVersion => MultiplayerSessionVersion.Current;

    /// <summary>The native save format version of the snapshots <see cref="Snapshot"/> writes.</summary>
    public static int SnapshotFormatVersion => NativeSaveSerializer.CurrentFormatVersion;

    /// <summary>The turn the match is planning: the next sealed set must be for this turn.</summary>
    public int Turn => Replay.State.Coordinator.Turn;

    /// <summary>Whether the match has reached its outcome.</summary>
    public bool IsFinished => Replay.State.Outcome is not null;

    /// <summary>The fingerprint a client reports for the same state.</summary>
    public string StateHash => MatchStateHasher.ComputeFingerprint(Replay.State);

    /// <summary>
    /// The match as every client builds it on <c>match.started</c>: generated from the seed, the
    /// host's settings and the seated roster, and advanced to the first Command phase.
    /// </summary>
    public static AuthoritativeMatch Bootstrap(
        OriginalData definitions,
        int seed,
        MultiplayerGameSettings settings,
        IReadOnlyList<PlayerView> players)
    {
        var state = MatchBootstrapFactory.Create(definitions, seed, settings, players);
        var replay = new MatchReplayRecorder(state);
        CommandPhase.Enter(replay);
        return new AuthoritativeMatch(new MatchHistory(replay, MatchHistory.SeatsOf(players), logTurn: 1));
    }

    /// <summary>
    /// A match picked up from a snapshot archive, refused unless it hashes to what it is stored
    /// under.
    /// </summary>
    /// <param name="definitions">The original data the match plays.</param>
    /// <param name="body">The archive, as a client uploads it.</param>
    /// <param name="stateHash">The hash the snapshot is stored under.</param>
    /// <param name="players">
    /// The roster as the server holds it now, so that the events fed after the snapshot find every
    /// seat that has ever been human, late joins included.
    /// </param>
    /// <param name="logTurn">
    /// The turn the log is on at the first event that will be fed: the turn the state is on when the
    /// feed starts after the last event the snapshot holds (the default), and 1 when it starts at the
    /// log's beginning, as a reconnecting client's does. See <see cref="MatchHistory.LogTurn"/>.
    /// </param>
    /// <exception cref="MultiplayerProtocolException">
    /// The body is not an archive this build reads, or its state does not hash to
    /// <paramref name="stateHash"/>.
    /// </exception>
    public static AuthoritativeMatch FromSnapshot(
        OriginalData definitions,
        string body,
        string stateHash,
        IReadOnlyList<PlayerView> players,
        int? logTurn = null)
    {
        ArgumentNullException.ThrowIfNull(body);
        ArgumentNullException.ThrowIfNull(stateHash);
        MatchState state;
        try
        {
            state = MatchStateClone.FromBase64(body, definitions);
        }
        catch (Exception exception) when (exception is FormatException or InvalidDataException)
        {
            throw new MultiplayerProtocolException(
                $"the snapshot is not a match this build can read: {exception.Message}", exception);
        }
        return Verified(state, stateHash, players, logTurn);
    }

    private static AuthoritativeMatch Verified(
        MatchState state,
        string stateHash,
        IReadOnlyList<PlayerView> players,
        int? logTurn)
    {
        ArgumentNullException.ThrowIfNull(players);
        var actual = MatchStateHasher.ComputeFingerprint(state);
        if (!string.Equals(actual, stateHash, StringComparison.Ordinal))
            throw new MultiplayerProtocolException("the snapshot does not hash to the state it claims");
        var history = new MatchHistory(
            new MatchReplayRecorder(state), MatchHistory.SeatsOf(players), logTurn ?? state.Coordinator.Turn);
        return new AuthoritativeMatch(history);
    }

    /// <summary>
    /// A match picked up from a native save payload, the bytes a snapshot archive compresses,
    /// refused unless it hashes to what it is stored under.
    /// </summary>
    /// <remarks>
    /// The WebAssembly build exchanges snapshots in this form, because the browser-wasm runtime has
    /// no Brotli codec: its host compresses and decompresses the archive around it. The other
    /// parameters are those of <see cref="FromSnapshot"/>.
    /// </remarks>
    /// <exception cref="MultiplayerProtocolException">
    /// The payload is not a save this build reads, or its state does not hash to
    /// <paramref name="stateHash"/>.
    /// </exception>
    public static AuthoritativeMatch FromSavePayload(
        OriginalData definitions,
        byte[] payload,
        string stateHash,
        IReadOnlyList<PlayerView> players,
        int? logTurn = null)
    {
        ArgumentNullException.ThrowIfNull(payload);
        ArgumentNullException.ThrowIfNull(stateHash);
        if (payload.Length > NativeSaveSerializer.MaximumSaveBytes)
            throw new MultiplayerProtocolException("the snapshot exceeds the save size limit");
        MatchState state;
        try
        {
            using var stream = new MemoryStream(payload, writable: false);
            state = NativeSaveSerializer.Load(stream, definitions);
        }
        catch (InvalidDataException exception)
        {
            throw new MultiplayerProtocolException(
                $"the snapshot is not a match this build can read: {exception.Message}", exception);
        }
        return Verified(state, stateHash, players, logTurn);
    }

    /// <summary>
    /// Folds one event of the match's log, in log order, and returns the state hash after it.
    /// </summary>
    /// <param name="event">The event, as the log stores it.</param>
    /// <param name="sealedOrders">
    /// For a <c>turn.sealed</c> event, the sealed set as <c>GET /turns/:n/orders</c> answers it.
    /// Not read for a seal the state already holds, nor for any other event.
    /// </param>
    /// <remarks>
    /// Only the facts that change the state do anything: <c>match.playerTakenOver</c>,
    /// <c>match.playerReturned</c> that replaced the computer, <c>match.latePlayerJoined</c>,
    /// <c>turn.opened</c> and <c>turn.sealed</c>. Every other event is accepted and ignored, so the
    /// caller can feed the log as it is.
    /// </remarks>
    /// <exception cref="MultiplayerProtocolException">
    /// A seal is for a turn ahead of the state, comes without its set, or the set is for another
    /// turn, does not match the digest the event announced or its own, or carries something this
    /// build cannot apply.
    /// </exception>
    public string Apply(MatchEvent @event, SealedOrdersView? sealedOrders = null)
    {
        ArgumentNullException.ThrowIfNull(@event);
        if (_history.Apply(@event) is { } seal)
        {
            if (sealedOrders is null)
            {
                throw new MultiplayerProtocolException(
                    $"the log sealed turn {seal.Turn}, and its sealed set was not given");
            }
            return _history.ApplySealedSet(sealedOrders, seal.OrderSetHash);
        }
        return StateHash;
    }

    /// <summary>The state as a snapshot archive, in the form a desync repair is uploaded in.</summary>
    public string Snapshot() => MatchStateClone.ToBase64(Replay.State);

    /// <summary>
    /// The state as a native save payload: what <see cref="Snapshot"/> compresses, for a host that
    /// compresses it itself (see <see cref="FromSavePayload"/>).
    /// </summary>
    public byte[] SavePayload()
    {
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, Replay.State);
        return stream.ToArray();
    }
}
