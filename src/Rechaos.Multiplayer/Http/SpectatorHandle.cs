using Rechaos.Multiplayer.Generated;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// The reads a spectator token opens on one match, all held behind the match's delay by the server.
/// </summary>
/// <remarks>
/// A spectator token works on these routes and nowhere else, and a player token does not work here:
/// the server keeps the two doors apart, so a spectator cannot submit, vote, report or chat.
/// </remarks>
public sealed class SpectatorHandle
{
    private readonly MultiplayerClient _client;

    internal SpectatorHandle(MultiplayerClient client, string matchId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(matchId);
        _client = client;
        MatchId = matchId;
    }

    public string MatchId { get; }

    /// <summary>The match as a spectator may see it, with the newest released turn.</summary>
    public Task<SpectatorMatchView> GetAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<SpectatorMatchView>(
            HttpMethod.Get, ApiRoutes.SpectatorMatch(MatchId), body: null, cancellationToken);

    /// <summary>
    /// The released events that decide who controls each seat, after <paramref name="after"/>.
    /// The page's cursor is where the next one starts.
    /// </summary>
    public Task<SpectatorEventPage> EventsAsync(
        int after,
        int limit,
        CancellationToken cancellationToken) =>
        _client.SendAsync<SpectatorEventPage>(
            HttpMethod.Get, ApiRoutes.SpectatorEvents(MatchId, after, limit), body: null,
            cancellationToken);

    /// <summary>A released turn's sealed set, read exactly so its digest can be checked.</summary>
    public Task<SealedOrdersView> SealedOrdersAsync(int turn, CancellationToken cancellationToken) =>
        _client.SendAsync<SealedOrdersView>(
            HttpMethod.Get, ApiRoutes.SpectatorOrders(MatchId, turn), body: null,
            cancellationToken, exactRoundTrip: true);

    /// <summary>The newest snapshot at or below the released turn.</summary>
    public Task<SnapshotView> LatestSnapshotAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<SnapshotView>(
            HttpMethod.Get, ApiRoutes.SpectatorSnapshot(MatchId), body: null, cancellationToken);

    /// <summary>Stops watching; the token stops working.</summary>
    public Task<Unit> LeaveAsync(CancellationToken cancellationToken) =>
        _client.SendAsync<Unit>(
            HttpMethod.Post, ApiRoutes.StopSpectating(MatchId), body: null, cancellationToken);
}
