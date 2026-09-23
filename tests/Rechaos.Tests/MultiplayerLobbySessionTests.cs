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
    public Task LeavingDuringSeatRequestReleasesTheReturnedSeat(string kind, string route) =>
        AbandoningASeatRequestReleasesTheReturnedSeat(kind, route, leave: true);

    [Theory]
    [InlineData("host", "/matches")]
    [InlineData("join", "/matches/join")]
    [InlineData("join-running", "/matches/join-running")]
    public Task StoppingDuringSeatRequestReleasesTheReturnedSeat(string kind, string route) =>
        AbandoningASeatRequestReleasesTheReturnedSeat(kind, route, leave: false);

    [Fact]
    public async Task LeavingDuringResumeDoesNotRejoinTheAbandonedSeat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        var running = View(MatchStatus.Running);
        var departed = running with { Players = [running.Players[0] with { Status = PlayerStatus.Left }] };
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(departed, "CODE1234", "p1"));
        server.Answer(HttpMethod.Post, "/matches/m1/rejoin", null, HttpStatusCode.NoContent);
        server.Answer(HttpMethod.Post, "/matches/m1/leave", null, HttpStatusCode.NoContent);
        var blocked = server.BlockOnce(HttpMethod.Get, "/matches/m1");

        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await Until(() => server.CallsTo(HttpMethod.Get, "/matches/m1") == 1, cancellationToken);
        await lobby.LeaveAsync();
        var stopping = lobby.StopAsync();
        blocked.SetResult();
        await stopping;

        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/matches/m1/rejoin"));
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/matches/m1/leave"));
        Assert.False(lobby.TryDequeueNotice(out _));
    }

    [Fact]
    public async Task StoppingDuringResumeKeepsTheSavedSeat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        server.Answer(HttpMethod.Post, "/matches/m1/leave", null, HttpStatusCode.NoContent);
        var blocked = server.BlockOnce(HttpMethod.Get, "/matches/m1");

        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await Until(() => server.CallsTo(HttpMethod.Get, "/matches/m1") == 1, cancellationToken);
        var stopping = lobby.StopAsync();
        blocked.SetResult();
        await stopping;

        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/matches/m1/leave"));
        Assert.Null(lobby.Handle);
        Assert.False(lobby.TryDequeueNotice(out _));
    }

    private static async Task AbandoningASeatRequestReleasesTheReturnedSeat(
        string kind, string route, bool leave)
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
        if (leave) await lobby.LeaveAsync();
        var stopping = lobby.StopAsync();
        blocked.SetResult();
        await stopping;

        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/matches/m1/leave"));

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

    [Fact]
    public async Task ProfileChangeIsSentForTheOwnSeatAndTheLobbyIsReadBack()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Put, "/matches/m1/profile", null, HttpStatusCode.NoContent);

        lobby.UpdateProfile(new UpdatePlayerProfileRequest("RENAMED", 7));

        await WaitFor<LobbyNotice.Updated>(lobby, cancellationToken);
        var sent = Assert.Single(server.Requests, request =>
            request.Method == HttpMethod.Put && request.Path.EndsWith("/matches/m1/profile", StringComparison.Ordinal));
        using var body = JsonDocument.Parse(sent.Body);
        Assert.Equal("RENAMED", body.RootElement.GetProperty("displayName").GetString());
        Assert.Equal(7, body.RootElement.GetProperty("portraitId").GetInt32());
        Assert.Equal(2, server.CallsTo(HttpMethod.Get, "/matches/m1"));
    }

    [Fact]
    public async Task RefusedProfileChangeIsReportedUnderItsOwnOperation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Put, "/matches/m1/profile", """
            {"error":{"code":"conflict","message":"Somebody in this match already plays under that name","details":{"reason":"display_name_taken"}}}
            """, HttpStatusCode.Conflict);

        lobby.UpdateProfile(new UpdatePlayerProfileRequest("TAKEN", 0));

        var failed = await WaitFor<LobbyNotice.Failed>(lobby, cancellationToken);
        Assert.Equal(nameof(MultiplayerLobbySession.UpdateProfile), failed.Operation);
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
        TNotice? typed = null;
        await Until(
            () => lobby.TryDequeueNotice(out var notice) && (typed = notice as TNotice) is not null,
            cancellationToken,
            typeof(TNotice).Name);
        return typed!;
    }

    private static async Task Until(
        Func<bool> condition, CancellationToken cancellationToken, string what = "the lobby request")
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        while (DateTime.UtcNow < deadline)
        {
            if (condition()) return;
            await Task.Delay(10, cancellationToken);
        }
        throw new TimeoutException($"Timed out waiting for {what}.");
    }
}
