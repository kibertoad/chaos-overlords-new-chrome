using System.Net;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

public sealed class MultiplayerServiceEndpointTests
{
    [Fact]
    public void CentralServiceUsesTheOfficialHttpsOrigin()
    {
        Assert.Equal(new Uri("https://chaos-overlords.dinorefurb.com"),
            MultiplayerServiceEndpoint.Central);
    }

    [Fact]
    public async Task HealthProbeUsesTheUnversionedHealthRoute()
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        server.Answer(HttpMethod.Get, "/health", "{\"ok\":true}");

        var healthy = await MultiplayerServiceEndpoint.IsHealthyAsync(
            http, new Uri("https://server.test/game"), CancellationToken.None);

        Assert.True(healthy);
        Assert.Equal("/game/health", Assert.Single(server.Requests).Path);
    }

    [Theory]
    [InlineData("{\"ok\":false}", HttpStatusCode.OK)]
    [InlineData("not-json", HttpStatusCode.OK)]
    [InlineData("{\"ok\":true}", HttpStatusCode.ServiceUnavailable)]
    public async Task HealthProbeRejectsAnUnhealthyResponse(string body, HttpStatusCode status)
    {
        using var server = new FakeMultiplayerServer();
        using var http = new HttpClient(server);
        server.Answer(HttpMethod.Get, "/health", body, status);

        Assert.False(await MultiplayerServiceEndpoint.IsHealthyAsync(
            http, MultiplayerServiceEndpoint.Central, CancellationToken.None));
    }
}
