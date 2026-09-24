using System.Text.Json;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The public list offers only matches this build can play.
/// </summary>
public sealed class MultiplayerLobbyListTests
{
    [Fact]
    public async Task AsksForAndKeepsOnlyThisBuildsSessionVersion()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var client = new MultiplayerClient(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches", new LobbyList(
        [
            Listing("current", MultiplayerSessionVersion.Current),
            Listing("other", MultiplayerSessionVersion.Current + 1),
        ]));

        var list = await client.ListLobbiesAsync(cancellationToken);

        Assert.Equal("current", Assert.Single(list.Matches).Id);
        Assert.Equal(
            $"?sessionVersion={MultiplayerSessionVersion.Current}",
            Assert.Single(server.Requests, request => request.Path.EndsWith("/matches", StringComparison.Ordinal))
                .Query);
    }

    private static LobbyListing Listing(string id, int sessionVersion) => new(
        id,
        "CODE1234",
        id,
        "HOST",
        1,
        4,
        false,
        MatchStatus.Lobby,
        sessionVersion,
        new MatchSettings(id, 4, 0, MatchVisibility.Public, new Dictionary<string, JsonElement>()),
        [],
        [],
        "2026-09-24T00:00:00.000Z");
}
