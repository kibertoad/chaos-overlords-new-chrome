using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed class MultiplayerProtocolHandshakeTests
{
    [Fact]
    public async Task HandshakesOnlyOnceAcrossAnonymousAndTokenBoundClients()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        var client = new MultiplayerClient(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches", new LobbyList([]));
        server.Answer(HttpMethod.Get, "/matches/m1", "not-json");

        await client.ListLobbiesAsync(cancellationToken);
        await Assert.ThrowsAsync<MultiplayerProtocolException>(() => client
            .WithToken("cop_test")
            .Match("m1")
            .GetAsync(cancellationToken));

        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/handshake"));
        Assert.Contains(
            $"\"protocolVersion\":{MultiplayerProtocolVersion.Current}",
            Assert.Single(server.BodiesSentTo(HttpMethod.Post, "/handshake")),
            StringComparison.Ordinal);
    }

    /// <param name="versionOffset">
    /// How far the server is from this build, relative to <see cref="MultiplayerProtocolVersion.Current"/>:
    /// a literal would silently become "the same version" the day the protocol reached it.
    /// </param>
    [Theory]
    [InlineData(-1, "SERVER IS OUTDATED")]
    [InlineData(+1, "UPDATE YOUR GAME")]
    public async Task DisplaysBothVersionsAndTheRequiredUpdate(
        int versionOffset,
        string expectedAction)
    {
        var serverVersion = MultiplayerProtocolVersion.Current + versionOffset;
        var cancellationToken = TestContext.Current.CancellationToken;
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        server.Answer(
            HttpMethod.Post,
            "/handshake",
            new HandshakeResponse(serverVersion));
        await using var lobby = new MultiplayerLobbySession(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));

        lobby.Browse();
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
        LobbyNotice.Failed? failure = null;
        while (DateTime.UtcNow < deadline && failure is null)
        {
            if (lobby.TryDequeueNotice(out var notice)) failure = notice as LobbyNotice.Failed;
            else await Task.Delay(10, cancellationToken);
        }

        Assert.NotNull(failure);
        Assert.Contains(
            $"CLIENT VERSION {MultiplayerProtocolVersion.Current}",
            failure.Reason,
            StringComparison.Ordinal);
        Assert.Contains($"SERVER VERSION {serverVersion}", failure.Reason, StringComparison.Ordinal);
        Assert.Contains(expectedAction, failure.Reason, StringComparison.Ordinal);
    }
}
