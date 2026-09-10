using System.Globalization;

namespace Rechaos.Multiplayer.Http;

/// <summary>
/// The paths of <c>multiplayer/packages/contracts/src/contracts.ts</c>, under <c>/api/v1</c>.
/// </summary>
/// <remarks>
/// <para>
/// This is the one part of the protocol the generator cannot mirror: an endpoint definition is a
/// TypeScript value with a resolver function in it, and there is no record for that. It is spelled
/// out here instead, and <c>RouteTemplateTests</c> pins every template against the contracts so a
/// path that moves on the server fails a test here rather than a request in a lobby.
/// </para>
/// <para>
/// Ids are interpolated raw, as the contracts' own resolvers do. What makes that safe is the
/// server's <c>resourceIdSchema</c>: an id that could carry a slash or a query never gets as far as
/// being one of these.
/// </para>
/// </remarks>
public static class ApiRoutes
{
    /// <summary>The prefix every route sits under.</summary>
    public const string Prefix = "/api/v1";

    public const string Matches = "/matches";
    public const string JoinMatch = "/matches/join";

    public static string Match(string matchId) => $"/matches/{matchId}";
    public static string StartMatch(string matchId) => $"/matches/{matchId}/start";
    public static string LeaveMatch(string matchId) => $"/matches/{matchId}/leave";

    public static string KickPlayer(string matchId, string playerId) =>
        $"/matches/{matchId}/players/{playerId}/kick";

    public static string Orders(string matchId, int turn) =>
        $"/matches/{matchId}/turns/{Number(turn)}/orders";

    public static string OwnOrders(string matchId, int turn) =>
        $"/matches/{matchId}/turns/{Number(turn)}/orders/mine";

    public static string Report(string matchId, int turn) =>
        $"/matches/{matchId}/turns/{Number(turn)}/report";

    public static string Snapshots(string matchId) => $"/matches/{matchId}/snapshots";
    public static string LatestSnapshot(string matchId) => $"/matches/{matchId}/snapshots/latest";

    public static string Snapshot(string matchId, int turn) =>
        $"/matches/{matchId}/snapshots/{Number(turn)}";

    public static string Events(string matchId, int after, int limit) =>
        $"/matches/{matchId}/events?after={Number(after)}&limit={Number(limit)}";

    public static string Stream(string matchId) => $"/matches/{matchId}/stream";

    /// <summary>Invariant digits: a culture that groups thousands would write an unroutable path.</summary>
    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);
}
