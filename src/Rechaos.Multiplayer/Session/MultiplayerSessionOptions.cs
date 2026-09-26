using Rechaos.Core.Assets;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;

namespace Rechaos.Multiplayer.Session;

/// <summary>What a session needs to start driving a match that has already been seated.</summary>
/// <param name="Match">The token-bound handle for this player.</param>
/// <param name="Definitions">The bundled gameplay tables the city is generated from.</param>
/// <param name="View">The match as the server last described it; the seed and roster come from it.</param>
/// <param name="OwnPlayerId">This client's player id, for reading its own row out of the roster.</param>
/// <param name="ResumeAfterSeq">
/// The last event sequence this client has already handled. Delivery is at least once, so resuming
/// from it may repeat facts already applied; every handler here is idempotent for that reason.
/// </param>
/// <param name="JoinedInProgress">
/// Whether this client took its seat after the match had started.
/// <para>
/// A late joiner's own row is on the roster by the time it bootstraps, so the city it would
/// generate seats itself under its own name where every peer seated a computer player under a
/// derived one — and both the name and the controller feed the state hash, so the first turn it
/// plays desyncs. It therefore starts from the host's snapshot and the event log like any other
/// client reconciling a match that has moved on without it.
/// </para>
/// </param>
/// <param name="StreamIdleTimeout">
/// How long the event stream may carry nothing — not even a keepalive — before the connection is
/// dropped and reopened from the last sequence seen, or null for
/// <see cref="MatchEventStream.DefaultIdleTimeout"/>. A socket the network has forgotten about
/// never says so; this is how the session finds out. Only time spent waiting on the connection
/// counts: while the session handles an event nothing is timed, so a dead socket is found at most
/// this long after the handler returns.
/// </param>
/// <param name="StreamOutageBudget">
/// How long the event stream may stay down before the session gives up, or null to keep trying for
/// as long as the player leaves the match open.
/// <para>
/// Null is the game's own answer, and it is deliberate. <see cref="RetryPolicy.Stream"/>'s five
/// minutes bound how long the client waits <em>silently</em>: when that window closes the
/// connection modal says so and goes on trying at the same backoff ceiling. It used to end the
/// session instead, so a six-minute sleep or a slow redeploy cost the player their city screen and
/// their planning copy — while the outbox on the same session, meeting the same exhausted window,
/// simply requeued the draft and opened another one. The recovery record made the manual reconnect
/// work; what it could not give back was the sense that nothing had gone wrong.
/// </para>
/// <para>
/// A bounded value is for a caller that owns the whole match and has somewhere to report to,
/// which is what the headless smoke test is.
/// </para>
/// </param>
/// <param name="StreamRetryPolicy">
/// How one connection's worth of reconnecting is paced, or null for <see cref="RetryPolicy.Stream"/>.
/// <para>
/// Its <see cref="RetryPolicy.MaxElapsed"/> is the bound on how long the client waits silently
/// before the connection modal says how long it has been trying; it is not a bound on the session,
/// which is <paramref name="StreamOutageBudget"/>. A caller overrides this to make a test reach the
/// end of a window without waiting out the real one.
/// </para>
/// </param>
/// <param name="CallRetryPolicy">
/// How one idempotent protocol call's worth of reconnecting is paced, or null for
/// <see cref="RetryPolicy.Call"/>.
/// <para>
/// The same bound on silent waiting the stream has, for the calls a received fact leads to and for
/// the two idempotent writes. Neither the outbox nor the reporter ends a match when a window
/// closes unanswered — they open another — so a caller overrides this to reach the end of one in a
/// test without waiting out the real five minutes.
/// </para>
/// </param>
/// <param name="BackgroundRetryPolicy">
/// How uploads nothing waits on — the host's checkpoint and bootstrap snapshots — retry, or null
/// for <see cref="RetryPolicy.Background"/>. A few attempts, then the upload is given up; a
/// reconnect replays from an older snapshot instead.
/// </param>
/// <param name="ReportFlushGrace">
/// How long disposing the session waits for state-hash reports it has not sent, or null for
/// <see cref="MultiplayerMatchSession.DefaultReportFlushGrace"/>.
/// <para>
/// A turn settles only once every human seat has reported it, so a hash abandoned on the way out
/// holds the rest of the table at that turn until its deadline. The wait is bounded because
/// leaving a match whose server is not answering has to stay quick, and nothing is lost by giving
/// up on it: a client that returns replays the seal from history and reports it again.
/// </para>
/// </param>
public sealed record MultiplayerSessionOptions(
    MatchHandle Match,
    OriginalData Definitions,
    MatchView View,
    string OwnPlayerId,
    int ResumeAfterSeq,
    bool JoinedInProgress = false,
    TimeSpan? StreamIdleTimeout = null,
    TimeSpan? StreamOutageBudget = null,
    RetryPolicy? StreamRetryPolicy = null,
    RetryPolicy? CallRetryPolicy = null,
    TimeSpan? ReportFlushGrace = null,
    RetryPolicy? BackgroundRetryPolicy = null);
