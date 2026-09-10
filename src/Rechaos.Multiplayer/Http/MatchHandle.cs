using Rechaos.Multiplayer.Generated;

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

    /// <summary>Gives up the seat. The slot becomes a computer player from the next turn.</summary>
    public Task<Unit> LeaveAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.LeaveMatch(MatchId), body: null, cancellationToken);

    /// <summary>Host only: removes a player, exactly as if they had left.</summary>
    public Task<Unit> KickAsync(string playerId, CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.KickPlayer(MatchId, playerId), body: null, cancellationToken);

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

    /// <summary>The sealed set in slot order, with its digest. Refused while the turn is open.</summary>
    public Task<SealedOrdersView> SealedOrdersAsync(int turn, CancellationToken cancellationToken) =>
        _client.SendAsync<SealedOrdersView>(
            HttpMethod.Get, ApiRoutes.Orders(MatchId, turn), body: null, cancellationToken);

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
}
