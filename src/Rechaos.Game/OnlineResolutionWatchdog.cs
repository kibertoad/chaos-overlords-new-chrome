using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;

namespace Rechaos.Game;

/// <summary>
/// Notices a turn every seat has finished whose seal does not arrive.
/// </summary>
/// <remarks>
/// The game asks for a resynchronisation when this fires; see
/// <c>ChaosGame.CheckOnlineResolutionWatchdog</c>. The state it keeps is
/// <see cref="MultiplayerUiState.ResolutionExpectedSince"/>, which the notices that settle a turn
/// (a seal, a desync pause, a lost connection) also clear.
/// </remarks>
internal static class OnlineResolutionWatchdog
{
    /// <summary>
    /// How long every seat may be ready with no sealed turn arriving before the client goes and
    /// looks for itself.
    /// </summary>
    /// <remarks>
    /// It has to sit ABOVE the stream's own idle detector plus its first reconnect, or it fires
    /// first and pre-empts the recovery that was already on its way. The server seals in the same
    /// request that completes the roster, so the ready `PUT` succeeds on a fresh connection while
    /// `turn.sealed` goes out on a stream a suspended laptop or an expired NAT entry has silently
    /// killed; the stream notices at <see cref="MatchEventStream.DefaultIdleTimeout"/>, fifty
    /// seconds, and comes back from its `Last-Event-ID`. At thirty seconds this watchdog was tearing
    /// the session down twenty seconds before the mechanism that fixes it even woke up.
    /// <para>
    /// The margin covers the stream's detector only while the pump is reading: time a handler spends
    /// on an event is not silence, so a dead socket found during a long handler — a desync repair
    /// waiting on its reports — can outlast this grace. Losing that race costs a resync and nothing
    /// more: <see cref="MultiplayerMatchSession.RequestResync"/> also ends that wait.
    /// </para>
    /// </remarks>
    internal static readonly TimeSpan Grace =
        MatchEventStream.DefaultIdleTimeout + TimeSpan.FromSeconds(25);

    /// <summary>
    /// Starts the grace when the turn can only be waiting on its seal, and clears it otherwise.
    /// </summary>
    /// <remarks>
    /// The turn is waiting on its seal once this client is connected, its own final submission has
    /// been acknowledged, and every seated seat is ready. A grace already running keeps its start.
    /// </remarks>
    internal static void Track(MultiplayerUiState online, TimeSpan monotonicNow)
    {
        var expectsResolution = online.IsConnected
            && online.Stage == MultiplayerStage.WaitingForSeal
            && online.ReadySubmissionAcknowledged
            && online.SeatedSeats > 0
            && online.ReadySeats >= online.SeatedSeats;
        if (expectsResolution)
            online.ResolutionExpectedSince ??= monotonicNow;
        else
            online.ResolutionExpectedSince = null;
    }

    /// <summary>
    /// Whether the grace has run out, re-arming it when it has.
    /// </summary>
    /// <remarks>
    /// Re-armed rather than cleared, so a second silence of the same length asks again.
    /// </remarks>
    internal static bool Expire(MultiplayerUiState online, TimeSpan monotonicNow)
    {
        if (online.ResolutionExpectedSince is not { } since || monotonicNow - since < Grace)
            return false;
        online.ResolutionExpectedSince = monotonicNow;
        return true;
    }
}
