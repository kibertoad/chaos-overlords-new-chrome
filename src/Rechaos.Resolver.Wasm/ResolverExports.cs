using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Resolution;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Resolver.Wasm;

/// <summary>
/// What the coordination server calls: <see cref="AuthoritativeMatch"/> over the wire's JSON.
/// </summary>
/// <remarks>
/// <para>
/// A match lives here between calls under an integer handle, because a state is a few megabytes of
/// managed objects and copying it across the JavaScript boundary on every turn would cost more
/// than resolving the turn. <see cref="Release"/> drops it. A host that loses its runtime loses
/// every handle with it and rebuilds the matches it still needs from a snapshot and the events
/// after it.
/// </para>
/// <para>
/// The payloads are the ones the server already holds: the stored <c>gameSettings</c> blob, the
/// roster as <c>playerView</c> rows, an event as the log stores it, a sealed set as
/// <c>GET /turns/:n/orders</c> answers it. A payload this build cannot read throws, and the host
/// sees the message as a JavaScript error.
/// </para>
/// </remarks>
[SupportedOSPlatform("browser")]
public static partial class ResolverExports
{
    private static readonly Lazy<OriginalData> Definitions = new(BundledOriginalData.Load);
    private static readonly Dictionary<int, AuthoritativeMatch> Matches = [];
    private static int _nextHandle = 1;

    /// <summary>The session version this resolver plays.</summary>
    [JSExport]
    public static int SessionVersion() => AuthoritativeMatch.SessionVersion;

    /// <summary>The native save format of the snapshots this resolver writes.</summary>
    [JSExport]
    public static int SnapshotFormatVersion() => AuthoritativeMatch.SnapshotFormatVersion;

    /// <summary>Builds a match the way every client does on <c>match.started</c>.</summary>
    /// <returns>The handle the other exports take.</returns>
    [JSExport]
    public static int Bootstrap(int seed, string gameSettingsJson, string playersJson)
    {
        var blob = WireJson.Read<Dictionary<string, JsonElement>>(gameSettingsJson);
        var players = WireJson.Read<PlayerView[]>(playersJson);
        var settings = MultiplayerGameSettings.FromWire(blob);
        return Hold(AuthoritativeMatch.Bootstrap(Definitions.Value, seed, settings, players));
    }

    /// <summary>
    /// Picks a match up from a native save payload stored under <paramref name="stateHash"/>.
    /// </summary>
    /// <remarks>
    /// The payload, not the archive a client uploads: the browser-wasm runtime has no Brotli
    /// codec, so the host takes the archive's header and compression off (and puts them back on
    /// what <see cref="SavePayload"/> returns) with its own. <paramref name="playersJson"/> is the
    /// roster as the server holds it now, and <paramref name="logTurn"/> the turn the log is on at
    /// the first event that will be fed, or 0 for the turn the state is on; see
    /// <see cref="AuthoritativeMatch.FromSnapshot"/>.
    /// </remarks>
    [JSExport]
    public static int Restore(byte[] savePayload, string stateHash, string playersJson, int logTurn) =>
        Hold(AuthoritativeMatch.FromSavePayload(
            Definitions.Value,
            savePayload,
            stateHash,
            WireJson.Read<PlayerView[]>(playersJson),
            logTurn > 0 ? logTurn : null));

    /// <summary>
    /// Folds one event of the match's log and returns the state hash after it.
    /// </summary>
    /// <remarks>
    /// <paramref name="sealedOrdersJson"/> is the sealed set for a <c>turn.sealed</c> event, and
    /// may be null for any other; see <see cref="AuthoritativeMatch.Apply"/>.
    /// </remarks>
    [JSExport]
    public static string ApplyEvent(int handle, string eventJson, string? sealedOrdersJson) =>
        Match(handle).Apply(
            WireJson.Read<MatchEvent>(eventJson),
            sealedOrdersJson is null ? null : WireJson.ReadExact<SealedOrdersView>(sealedOrdersJson));

    /// <summary>The current state hash.</summary>
    [JSExport]
    public static string StateHash(int handle) => Match(handle).StateHash;

    /// <summary>The turn the match is planning.</summary>
    [JSExport]
    public static int Turn(int handle) => Match(handle).Turn;

    /// <summary>Whether the match has reached its outcome.</summary>
    [JSExport]
    public static bool IsFinished(int handle) => Match(handle).IsFinished;

    /// <summary>The state as a native save payload; see <see cref="Restore"/>.</summary>
    [JSExport]
    public static byte[] SavePayload(int handle) => Match(handle).SavePayload();

    /// <summary>
    /// The seat in <paramref name="slot"/>'s view as a native save payload, or an empty array when
    /// the seat has none (eliminated, or the match has ended); see
    /// <see cref="AuthoritativeMatch.SeatViewPayload"/>. The host compresses it as it does
    /// <see cref="SavePayload"/>.
    /// </summary>
    [JSExport]
    public static byte[] SeatView(int handle, int slot) => Match(handle).SeatViewPayload(slot) ?? [];

    /// <summary>
    /// The bytes the managed heap holds, after a full collection when <paramref name="collect"/> is
    /// set.
    /// </summary>
    /// <remarks>
    /// The WebAssembly memory only ever grows, so its size says how much was once needed, not how
    /// much is in use. A host that keeps its matches under a memory budget reads this instead.
    /// </remarks>
    [JSExport]
    public static double ManagedHeapBytes(bool collect) => GC.GetTotalMemory(collect);

    /// <summary>Forgets a match. Releasing an unknown handle does nothing.</summary>
    [JSExport]
    public static void Release(int handle) => Matches.Remove(handle);

    private static int Hold(AuthoritativeMatch match)
    {
        var handle = _nextHandle++;
        Matches.Add(handle, match);
        return handle;
    }

    private static AuthoritativeMatch Match(int handle) =>
        Matches.TryGetValue(handle, out var match)
            ? match
            : throw new ArgumentException($"no match is held under handle {handle}", nameof(handle));
}
