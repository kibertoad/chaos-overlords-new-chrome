using System.Net;
using System.Text.Json;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>The lobby chat as the lobby session reads and sends it.</summary>
public sealed partial class MultiplayerLobbySessionTests
{
    [Fact]
    public async Task ChatIsReadFromTheLogOnlyWhenTheLobbyLogHasGrown()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        var view = View(MatchStatus.Lobby) with { LastEventSeq = 3 };
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(view, "CODE1234", "p1"));
        server.Answer(HttpMethod.Get, "/events", new EventPage(
        [
            Chat(1, "p1", "HELLO"),
            new LobbyHostChangedEvent(2, "m1", "2026-10-06T00:00:00.000Z",
                new LobbyHostChangedEventPayload("p1")),
            Chat(3, "p2", "READY WHEN YOU ARE"),
        ]));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);

        lobby.Refresh();
        var chatted = await WaitFor<LobbyNotice.Chatted>(lobby, cancellationToken);

        Assert.Equal(
            [new LobbyChatLine(1, "p1", "HELLO"), new LobbyChatLine(3, "p2", "READY WHEN YOU ARE")],
            chatted.Lines);
        lobby.Refresh();
        await WaitFor<LobbyNotice.Updated>(lobby, cancellationToken);
        await Until(() => !lobby.IsBusy, cancellationToken);
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/events"));
    }

    [Fact]
    public async Task ChatReadThatFailsPartWayKeepsWhatWasReadAndLeavesTheCallSucceeded()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1",
            new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(
            View(MatchStatus.Lobby) with { LastEventSeq = 3 }, "CODE1234", "p1"));
        server.Answer(HttpMethod.Put, "/matches/m1/profile", null, HttpStatusCode.NoContent);
        server.AnswerOnce(HttpMethod.Get, "/events", new EventPage([Chat(1, "p1", "HELLO")]));
        server.Answer(HttpMethod.Get, "/events", """
            {"error":{"code":"conflict","message":"Not now","details":{"reason":"busy"}}}
            """, HttpStatusCode.Conflict);

        lobby.UpdateProfile(new UpdatePlayerProfileRequest("RENAMED", 7));
        await Until(() => server.CallsTo(HttpMethod.Get, "/events") == 2, cancellationToken);
        await Until(() => !lobby.IsBusy, cancellationToken);

        var notices = new List<LobbyNotice>();
        while (lobby.TryDequeueNotice(out var notice)) notices.Add(notice);
        Assert.DoesNotContain(notices, notice => notice is LobbyNotice.Failed);
        var chatted = Assert.Single(notices.OfType<LobbyNotice.Chatted>());
        Assert.Equal([new LobbyChatLine(1, "p1", "HELLO")], chatted.Lines);
    }

    [Fact]
    public async Task ChatReadStopsOnAPageThatMovesNothing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(
            View(MatchStatus.Lobby) with { LastEventSeq = 3 }, "CODE1234", "p1"));
        // A page that never reaches the view's last event, however often it is asked for.
        server.Answer(HttpMethod.Get, "/events", new EventPage([Chat(1, "p1", "HELLO")]));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);

        lobby.Refresh();
        await WaitFor<LobbyNotice.Chatted>(lobby, cancellationToken);
        await Until(() => !lobby.IsBusy, cancellationToken);

        Assert.Equal(2, server.CallsTo(HttpMethod.Get, "/events"));
    }

    [Fact]
    public async Task ChatIsNotReadOnceTheMatchHasStarted()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1",
            new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Get, "/matches/m1", new MatchDetail(
            View(MatchStatus.Running) with { LastEventSeq = 40 }, "CODE1234", "p1"));

        lobby.Refresh();
        await WaitFor<LobbyNotice.Updated>(lobby, cancellationToken);
        await Until(() => !lobby.IsBusy, cancellationToken);

        Assert.Equal(0, server.CallsTo(HttpMethod.Get, "/events"));
    }

    [Fact]
    public async Task ChatIsPostedInTheOrderItWasWritten()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1",
            new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Post, "/matches/m1/chat", null, HttpStatusCode.NoContent);

        lobby.SendChat("FIRST");
        lobby.SendChat("SECOND");

        await Until(() => server.CallsTo(HttpMethod.Post, "/matches/m1/chat") == 2, cancellationToken);
        var texts = server.BodiesSentTo(HttpMethod.Post, "/matches/m1/chat")
            .Select(body => JsonDocument.Parse(body).RootElement.GetProperty("text").GetString())
            .ToArray();
        Assert.Equal(["FIRST", "SECOND"], texts);
    }

    [Fact]
    public async Task RefusedChatIsReportedUnderItsOwnOperation()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches/m1",
            new MatchDetail(View(MatchStatus.Lobby), "CODE1234", "p1"));
        lobby.Resume("m1", "p1", "cop_test", "CODE1234");
        await WaitFor<LobbyNotice.Seated>(lobby, cancellationToken);
        server.Answer(HttpMethod.Post, "/matches/m1/chat", """
            {"error":{"code":"rate_limited","message":"Too many chat messages","details":{"reason":"rate_limited","retryAfterSeconds":30}}}
            """, HttpStatusCode.TooManyRequests);

        lobby.SendChat("SPAM");

        var failed = await WaitFor<LobbyNotice.Failed>(lobby, cancellationToken);
        Assert.Equal(nameof(MultiplayerLobbySession.SendChat), failed.Operation);
    }

    [Fact]
    public async Task ChatBeforeTakingASeatIsDropped()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));

        lobby.SendChat("NOBODY HERE");
        await Task.Delay(50, cancellationToken);

        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/chat"));
    }

    private static LobbyChatMessageEvent Chat(int seq, string playerId, string text) =>
        new(seq, "m1", "2026-10-06T00:00:00.000Z", new LobbyChatMessageEventPayload(playerId, text));
}
