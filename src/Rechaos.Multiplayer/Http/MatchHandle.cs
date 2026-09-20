using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;

namespace Rechaos.Multiplayer.Http;

/// <summary>The calls that name a match, bound to one id and one membership token.</summary>
public sealed class MatchHandle
{
    private readonly MultiplayerClient _client;

    internal MatchHandle(MultiplayerClient client, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        _client = client;
        MatchId = matchId;
    }

    public string MatchId { get; }

    /// <summary>The match view, which is the authority whenever it disagrees with an event.</summary>
    public Task<MatchDetail> GetAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<MatchDetail>(
            HttpMethod.Get, ApiRoutes.Match(MatchId), body: null, cancellationToken);

    /// <summary>Host only: seats the players, draws the seed and opens turn 1.</summary>
    public Task<Unit> StartAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.StartMatch(MatchId), body: null, cancellationToken);

    public Task<Unit> UpdateSettingsAsync(
        MatchSettings settings,
        CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Put, ApiRoutes.UpdateMatchSettings(MatchId), settings, cancellationToken);

    /// <summary>
    /// Leaves the active roster while retaining the durable membership needed to rejoin.
    /// </summary>
    /// <remarks>
    /// The match stops waiting on this player's readiness and opens a vote. Their gangs remain under
    /// an idle human controller unless every present player approves the replay-recorded transfer.
    /// See <c>docs/MULTIPLAYER.md</c>.
    /// </remarks>
    public Task<Unit> LeaveAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.LeaveMatch(MatchId), body: null, cancellationToken);

    /// <summary>Returns this authenticated player to a former human seat.</summary>
    public Task<Unit> RejoinAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.RejoinMatch(MatchId), body: null, cancellationToken);

    /// <summary>Host only: removes a player, exactly as if they had left.</summary>
    public Task<Unit> KickAsync(string playerId, CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.KickPlayer(MatchId, playerId), body: null, cancellationToken);

    /// <summary>Votes to keep waiting for an absent player or approve computer control.</summary>
    public Task<Unit> VoteOnTakeoverAsync(
        string playerId,
        TakeoverVoteRequest request,
        CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.TakeoverVote(MatchId, playerId), request, cancellationToken);

    /// <summary>
    /// Replaces this player's order document for the open turn and sets readiness.
    /// </summary>
    /// <remarks>
    /// The write is refused once the turn has sealed (<c>409 turn_not_open</c>) rather than folded
    /// in late, so a caller that loses this race has to re-plan against the next turn.
    /// </remarks>
    public Task<OwnSubmissionView> SubmitOrdersAsync(
        int turn,
        SubmitOrdersRequest request,
        CancellationToken cancellationToken) =>
        _client.SendAsync<OwnSubmissionView>(
            HttpMethod.Put, ApiRoutes.Orders(MatchId, turn), request, cancellationToken);

    /// <summary>This player's own submission, for a client that reconnected mid-turn.</summary>
    public Task<OwnSubmissionView> OwnSubmissionAsync(int turn, CancellationToken cancellationToken) =>
        _client.SendAsync<OwnSubmissionView>(
            HttpMethod.Get, ApiRoutes.OwnOrders(MatchId, turn), body: null, cancellationToken);

    /// <summary>
    /// The sealed set in slot order, with its digest. Refused while the turn is open.
    /// </summary>
    /// <remarks>
    /// Read exactly: the order documents in it are re-serialized to check the digest the seal
    /// announced, so a field dropped on the way in would make the hash unverifiable. This is the
    /// only payload strict enough to refuse a server that has grown one — see
    /// <see cref="WireJson.ReadExact{T}"/>.
    /// </remarks>
    public Task<SealedOrdersView> SealedOrdersAsync(int turn, CancellationToken cancellationToken) =>
        _client.SendAsync<SealedOrdersView>(
            HttpMethod.Get, ApiRoutes.Orders(MatchId, turn), body: null, cancellationToken,
            exactRoundTrip: true);

    /// <summary>The state hash this client reached after applying the sealed turn.</summary>
    public Task<Unit> ReportAsync(
        int turn,
        TurnReportRequest request,
        CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.Report(MatchId, turn), request, cancellationToken);

    /// <summary>Host only: the native snapshot that repairs a desynced turn.</summary>
    public Task<Unit> UploadSnapshotAsync(
        UploadSnapshotRequest request,
        CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.Snapshots(MatchId), request, cancellationToken);

    public Task<SnapshotView> LatestSnapshotAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<SnapshotView>(
            HttpMethod.Get, ApiRoutes.LatestSnapshot(MatchId), body: null, cancellationToken);

    public Task<SnapshotView> SnapshotAsync(int turn, CancellationToken cancellationToken) =>
        _client.SendAsync<SnapshotView>(
            HttpMethod.Get, ApiRoutes.Snapshot(MatchId, turn), body: null, cancellationToken);

    /// <summary>A page of the event log: the fallback for a network that cannot hold a stream.</summary>
    public Task<EventPage> EventsAsync(int after, int limit, CancellationToken cancellationToken) =>
        _client.SendAsync<EventPage>(
            HttpMethod.Get, ApiRoutes.Events(MatchId, after, limit), body: null, cancellationToken);

    /// <summary>Opens one connection's worth of the event stream. The caller disposes it.</summary>
    internal Task<HttpResponseMessage> OpenStreamAsync(int afterSeq, CancellationToken cancellationToken) =>
        _client.OpenStreamAsync(ApiRoutes.Stream(MatchId), afterSeq, cancellationToken);

    /// <summary>How far the server's clock is ahead of this machine's; see `HandshakeState`.</summary>
    public TimeSpan ServerTimeOffset => _client.ServerTimeOffset;

    /// <summary>Forgets the handshake, so the next call establishes the protocol again.</summary>
    internal void ForgetHandshake() => _client.ForgetHandshake();
}
