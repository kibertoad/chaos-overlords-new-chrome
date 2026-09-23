using System.Net;
using System.Text.Json;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed class MultiplayerLobbySessionTests
{
    [Theory]
    [InlineData("host", "/matches")]
    [InlineData("join", "/matches/join")]
    [InlineData("join-running", "/matches/join-running")]
    public async Task LeavingDuringSeatRequestReleasesTheReturnedSeat(string kind, string route)
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        var view = View(kind == "join-running" ? MatchStatus.Running : MatchStatus.Lobby);
        server.Answer(HttpMethod.Post, route,
            new MembershipView(view, view.Players[0], "cop_test", "CODE1234"));
        server.Answer(HttpMethod.Post, "/matches/m1/leave", null, HttpStatusCode.NoContent);
        var blocked = server.BlockOnce(HttpMethod.Post, route);

        switch (kind)
        {
            case "host":
                lobby.Host(new CreateMatchRequest(view.Settings, "HOST", 0, null, null, null));
                break;
            case "join":
                lobby.Join(new JoinMatchRequest("CODE1234", "HOST", 0, null));
                break;
            default:
                lobby.JoinRunning(new JoinRunningMatchRequest(
                    "CODE1234", "HOST", 0, null, 0));
                break;
        }
        await Until(() => server.CallsTo(HttpMethod.Post, route) == 1, cancellationToken);
        await lobby.LeaveAsync();
        var stopping = lobby.StopAsync();
        blocked.SetResult();
        await stopping;
        await Until(() => server.CallsTo(HttpMethod.Post, "/matches/m1/leave") == 1
            && !lobby.IsBusy, cancellationToken);

        Assert.Null(lobby.Handle);
        Assert.Equal(string.Empty, lobby.OwnPlayerId);
        Assert.False(lobby.TryDequeueNotice(out _));
    }

    [Fact]
    public async Task StartConflictReconcilesWhenTheLobbyAlreadyBecameRunning()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));

        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);

        server.Answer(HttpMethod.Post, "/start", """
            {"error":{"code":"conflict","message":"The match has already started","details":{"reason":"match_not_in_lobby"}}}
            """, HttpStatusCode.Conflict);
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(View(MatchStatus.Running), "CODE1234", "p1"));

        lobby.Start();

        var updated = await WaitFor<LobbyNotice.Updated>(lobby, cancellationToken);
        Assert.Equal(MatchStatus.Running, updated.Match.Status);
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/start"));
        Assert.Equal(2, server.CallsTo(HttpMethod.Get, "/matches/m1"));
    }

    private static MatchView View(MatchStatus status) => new(
        "m1",
        MultiplayerProtocolVersion.Current,
        MultiplayerSessionVersion.Current,
        status,
        new MatchSettings("TEST", 6, 0, MatchVisibility.Private,
            new Dictionary<string, JsonElement>()),
        "p1",
        status == MatchStatus.Lobby ? null : 123,
        status == MatchStatus.Lobby ? 0 : 1,
        [new PlayerView("p1", status == MatchStatus.Lobby ? -1 : 0, "HOST", 0,
            PlayerStatus.Active, true)],
        null,
        null,
        0,
        "2026-09-19T00:00:00.000Z");

    private static async Task<TNotice> WaitFor<TNotice>(
        MultiplayerLobbySession lobby,
        CancellationToken cancellationToken)
        where TNotice : LobbyNotice
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (lobby.TryDequeueNotice(out var notice) && notice is TNotice typed) return typed;
            await Task.Delay(10, cancellationToken);
        }
        throw new TimeoutException($"Timed out waiting for {typeof(TNotice).Name}.");
    }

    private static async Task Until(Func<bool> condition, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10, cancellationToken);
        }
        throw new TimeoutException("Timed out waiting for the lobby request.");
    }
}
