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
/// <see cref="MatchBootstrapFactory"/> followed by <see cref="CommandPhase.Enter"/>, a sealed turn
/// is <see cref="SealedTurnApplier.Apply"/>, a seat changing hands is
/// <see cref="SeatControl.HandOver"/>, and a snapshot is the archive
/// <see cref="MatchStateClone.ToBase64"/> writes for a desync repair. Nothing here talks to a
/// network. The caller feeds the facts in event-log order.
/// </para>
/// <para>
/// The coordination server is TypeScript, so it reaches this class through the WebAssembly build
/// in <c>src/Rechaos.Resolver.Wasm</c>, which takes and returns the wire's JSON.
/// </para>
/// </remarks>
public sealed class AuthoritativeMatch
{
    private readonly MatchReplayRecorder _replay;

    private AuthoritativeMatch(MatchReplayRecorder replay) => _replay = replay;

    /// <summary>The session version this build resolves: see <see cref="MultiplayerSessionVersion"/>.</summary>
    /// <remarks>
    /// A resolver can stand in for the clients only on a match stored under the session version it
    /// plays, because that number is what names the rules, the order schema and the state hashing.
    /// </remarks>
    public static int SessionVersion => MultiplayerSessionVersion.Current;

    /// <summary>The native save format version of the snapshots <see cref="Snapshot"/> writes.</summary>
    public static int SnapshotFormatVersion => NativeSaveSerializer.CurrentFormatVersion;

    /// <summary>The turn the match is planning: the next sealed set must be for this turn.</summary>
    public int Turn => _replay.State.Coordinator.Turn;

    /// <summary>Whether the match has reached its outcome.</summary>
    public bool IsFinished => _replay.State.Outcome is not null;

    /// <summary>The fingerprint a client reports for the same state.</summary>
    public string StateHash => MatchStateHasher.ComputeFingerprint(_replay.State);

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
        return new AuthoritativeMatch(replay);
    }

    /// <summary>
    /// A match picked up from a snapshot archive, refused unless it hashes to what it is stored
    /// under.
    /// </summary>
    /// <exception cref="MultiplayerProtocolException">
    /// The body is not an archive this build reads, or its state does not hash to
    /// <paramref name="stateHash"/>.
    /// </exception>
    public static AuthoritativeMatch FromSnapshot(OriginalData definitions, string body, string stateHash)
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
        return Verified(state, stateHash);
    }

    private static AuthoritativeMatch Verified(MatchState state, string stateHash)
    {
        var actual = MatchStateHasher.ComputeFingerprint(state);
        if (!string.Equals(actual, stateHash, StringComparison.Ordinal))
            throw new MultiplayerProtocolException("the snapshot does not hash to the state it claims");
        return new AuthoritativeMatch(new MatchReplayRecorder(state));
    }

    /// <summary>
    /// A match picked up from a native save payload, the bytes a snapshot archive compresses,
    /// refused unless it hashes to what it is stored under.
    /// </summary>
    /// <remarks>
    /// The WebAssembly build exchanges snapshots in this form, because the browser-wasm runtime has
    /// no Brotli codec: its host compresses and decompresses the archive around it.
    /// </remarks>
    /// <exception cref="MultiplayerProtocolException">
    /// The payload is not a save this build reads, or its state does not hash to
    /// <paramref name="stateHash"/>.
    /// </exception>
    public static AuthoritativeMatch FromSavePayload(OriginalData definitions, byte[] payload, string stateHash)
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
        return Verified(state, stateHash);
    }

    /// <summary>
    /// Resolves one sealed turn and returns the hash every client is expected to report for it.
    /// </summary>
    /// <exception cref="MultiplayerProtocolException">
    /// The set does not match its own digest, is for another turn, or carries something this build
    /// cannot apply.
    /// </exception>
    public string ApplySealedTurn(SealedOrdersView sealedOrders)
    {
        ArgumentNullException.ThrowIfNull(sealedOrders);
        if (!OrderDigest.Verifies(sealedOrders, sealedOrders.OrderSetHash))
        {
            throw new MultiplayerProtocolException(
                $"the sealed set for turn {sealedOrders.Turn} does not match its own digest");
        }
        return SealedTurnApplier.Apply(_replay, sealedOrders);
    }

    /// <summary>
    /// A seat changing hands at its place in the event log: <c>match.playerTakenOver</c> hands it
    /// to the computer, <c>match.playerReturned</c> and <c>match.latePlayerJoined</c> to a human.
    /// </summary>
    public void HandOverSeat(int slot, PlayerController controller) =>
        SeatControl.HandOver(_replay, slot, controller);

    /// <summary>The state as a snapshot archive, in the form a desync repair is uploaded in.</summary>
    public string Snapshot() => MatchStateClone.ToBase64(_replay.State);

    /// <summary>
    /// The state as a native save payload: what <see cref="Snapshot"/> compresses, for a host that
    /// compresses it itself (see <see cref="FromSavePayload"/>).
    /// </summary>
    public byte[] SavePayload()
    {
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, _replay.State);
        return stream.ToArray();
    }
}
