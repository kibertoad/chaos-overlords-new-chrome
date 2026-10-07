using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Every deadline the protocol carries is an instant on the server's clock, and the client learns
/// how far that clock is from its own from the <c>Date</c> header of the responses it reads.
/// </summary>
public sealed class MultiplayerClockSkewTests
{
    /// <summary>The header has one-second resolution, so a reading is up to a second short.</summary>
    private static readonly TimeSpan HeaderResolution = TimeSpan.FromSeconds(1.5);

    [Theory]
    [InlineData(90)]
    [InlineData(-300)]
    public async Task TakesTheFirstReadingWhole(int serverAheadSeconds)
    {
        using var server = new FakeMultiplayerServer
        {
            ServerClockOffset = TimeSpan.FromSeconds(serverAheadSeconds),
        };
        using var http = new HttpClient(server);
        var client = new MultiplayerClient(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches", new LobbyList([]));

        Assert.Equal(TimeSpan.Zero, client.ServerTimeOffset);
        await client.ListLobbiesAsync(TestContext.Current.CancellationToken);

        AssertNear(TimeSpan.FromSeconds(serverAheadSeconds), client.ServerTimeOffset);
    }

    /// <summary>
    /// Later readings move the offset a quarter of the way, so one slow response cannot jerk the
    /// countdown on screen; the token-bound handles share what the anonymous client learned.
    /// </summary>
    [Fact]
    public async Task SmoothsLaterReadingsAndSharesThemAcrossHandles()
    {
        using var server = new FakeMultiplayerServer { ServerClockOffset = TimeSpan.FromSeconds(60) };
        using var http = new HttpClient(server);
        var client = new MultiplayerClient(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches", new LobbyList([]));
        await client.ListLobbiesAsync(TestContext.Current.CancellationToken);
        var first = client.ServerTimeOffset;

        server.ServerClockOffset = TimeSpan.FromSeconds(100);
        await client.ListLobbiesAsync(TestContext.Current.CancellationToken);

        // A quarter of the forty-second jump, not all of it.
        AssertNear(first + TimeSpan.FromSeconds(10), client.ServerTimeOffset);
        Assert.Equal(client.ServerTimeOffset, client.WithToken("cop_test").Match("m1").ServerTimeOffset);
    }

    /// <summary>A response without a <c>Date</c> leaves what was learned alone.</summary>
    [Fact]
    public async Task IgnoresAResponseWithoutADate()
    {
        using var server = new FakeMultiplayerServer { ServerClockOffset = TimeSpan.FromSeconds(-45) };
        using var http = new HttpClient(server);
        var client = new MultiplayerClient(
            http, new MultiplayerClientOptions(new Uri("http://server.test")));
        server.Answer(HttpMethod.Get, "/matches", new LobbyList([]));
        await client.ListLobbiesAsync(TestContext.Current.CancellationToken);
        var learned = client.ServerTimeOffset;

        server.ServerClockOffset = null;
        await client.ListLobbiesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(learned, client.ServerTimeOffset);
    }

    private static void AssertNear(TimeSpan expected, TimeSpan actual) =>
        Assert.InRange(actual, expected - HeaderResolution, expected + HeaderResolution);
}
