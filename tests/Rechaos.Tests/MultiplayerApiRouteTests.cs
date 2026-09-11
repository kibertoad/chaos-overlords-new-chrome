using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The client's paths, held against the ones the server actually mounts.
/// </summary>
/// <remarks>
/// <see cref="RouteTemplates"/> is generated from <c>packages/contracts/src/contracts.ts</c> with the
/// same <c>mapApiContractToPath</c> the Hono routes are derived from, so a path that moves on the
/// server fails here rather than in a lobby.
/// </remarks>
public sealed class MultiplayerApiRouteTests
{
    private const string MatchId = "m1";
    private const string PlayerId = "p2";

    /// <summary>The template each builder produces, with the sample ids put back as `:params`.</summary>
    private static readonly (string Template, string Built)[] Routes =
    [
        (RouteTemplates.ListLobbies, $"GET {ApiRoutes.Matches}"),
        (RouteTemplates.CreateMatch, $"POST {ApiRoutes.Matches}"),
        (RouteTemplates.JoinMatch, $"POST {ApiRoutes.JoinMatch}"),
        (RouteTemplates.GetMatch, $"GET {ApiRoutes.Match(MatchId)}"),
        (RouteTemplates.StartMatch, $"POST {ApiRoutes.StartMatch(MatchId)}"),
        (RouteTemplates.LeaveMatch, $"POST {ApiRoutes.LeaveMatch(MatchId)}"),
        (RouteTemplates.KickPlayer, $"POST {ApiRoutes.KickPlayer(MatchId, PlayerId)}"),
        (RouteTemplates.SubmitOrders, $"PUT {ApiRoutes.Orders(MatchId, 7)}"),
        (RouteTemplates.OwnSubmission, $"GET {ApiRoutes.OwnOrders(MatchId, 7)}"),
        (RouteTemplates.SealedOrders, $"GET {ApiRoutes.Orders(MatchId, 7)}"),
        (RouteTemplates.ReportTurn, $"POST {ApiRoutes.Report(MatchId, 7)}"),
        (RouteTemplates.UploadSnapshot, $"POST {ApiRoutes.Snapshots(MatchId)}"),
        (RouteTemplates.LatestSnapshot, $"GET {ApiRoutes.LatestSnapshot(MatchId)}"),
        (RouteTemplates.Snapshot, $"GET {ApiRoutes.Snapshot(MatchId, 7)}"),
        (RouteTemplates.ListEvents, $"GET {ApiRoutes.Events(MatchId, 0, 200)}"),
        (RouteTemplates.StreamEvents, $"GET {ApiRoutes.Stream(MatchId)}"),
    ];

    [Fact]
    public void EveryBuiltPathMatchesTheRouteTheServerMounts()
    {
        foreach (var (template, built) in Routes)
        {
            Assert.Equal(template, AsTemplate(built));
        }
    }

    /// <summary>A route the client cannot reach is a route somebody has to notice was added.</summary>
    [Fact]
    public void CoversEveryRouteTheServerMounts()
    {
        Assert.Equal(
            RouteTemplates.All.Order(StringComparer.Ordinal),
            Routes.Select(route => route.Template).Distinct().Order(StringComparer.Ordinal));
    }

    /// <summary>Turn numbers are written in invariant digits, never grouped by a culture.</summary>
    [Fact]
    public void WritesTurnNumbersAsPlainDigits()
    {
        Assert.Equal("/matches/m1/turns/1234567/orders", ApiRoutes.Orders(MatchId, 1_234_567));
    }

    /// <summary>
    /// A server published under a path keeps it.
    /// </summary>
    /// <remarks>
    /// A reverse proxy that mounts the server at <c>/game/</c> is the ordinary self-hosting shape, and
    /// an absolute reference (<c>/api/v1/...</c>) resolves from the host root — so the prefix has to be
    /// joined as a relative one against a base that ends in a slash. A player typing the address has no
    /// reason to add the slash, which is why the client adds it rather than requiring it.
    /// </remarks>
    [Theory]
    [InlineData("http://host", "/api/v1/matches/m1/start")]
    [InlineData("http://host/", "/api/v1/matches/m1/start")]
    [InlineData("http://host/game", "/game/api/v1/matches/m1/start")]
    [InlineData("http://host/game/", "/game/api/v1/matches/m1/start")]
    [InlineData("http://host/deep/path/", "/deep/path/api/v1/matches/m1/start")]
    public async Task BuildsRequestsUnderThePathTheServerIsPublishedAt(string origin, string expected)
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var match = new MultiplayerClient(http, new MultiplayerClientOptions(new Uri(origin)))
            .WithToken("cop_test")
            .Match(MatchId);
        server.Answer(HttpMethod.Post, "/start", null, System.Net.HttpStatusCode.NoContent);

        await match.StartAsync(CancellationToken.None);

        Assert.Equal(expected, Assert.Single(server.Requests).Path);
    }

    private static string AsTemplate(string built) => built
        .Replace($"/{MatchId}", "/:matchId", StringComparison.Ordinal)
        .Replace($"/{PlayerId}/", "/:playerId/", StringComparison.Ordinal)
        .Replace("/turns/7/", "/turns/:turn/", StringComparison.Ordinal)
        .Replace("/snapshots/7", "/snapshots/:turn", StringComparison.Ordinal)
        .Replace("/events?after=0&limit=200", "/events", StringComparison.Ordinal);
}
