using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A refusal's body is read under a bound of its own, whatever sent it and whatever client it
/// arrived on: bounded in size, bounded in time, and never allowed to cost the status it came with.
/// </summary>
/// <remarks>
/// Every body here is unbuffered, as a real connection's is. A buffered fake would hand the client a
/// body that was already complete, and none of what these tests guard — a page that never finishes,
/// a body larger than the client will hold — could happen.
/// </remarks>
public sealed class MultiplayerRefusalBodyTests
{
    private const string RevokedEnvelope =
        """{"error":{"code":"forbidden","message":"kicked","details":{"reason":"kicked"}}}""";

    /// <summary>
    /// A proxy that sends a 502 and never finishes its page yields the 502, not a hang and not a
    /// timeout claiming the server said nothing.
    /// </summary>
    [Fact]
    public async Task StreamRefusalBodyThatNeverFinishesKeepsItsStatus()
    {
        using var server = new FakeMultiplayerServer();
        server.AnswerOnceUnbuffered(HttpMethod.Get, "/stream", HttpStatusCode.BadGateway)
            .Write("<html>");
        using var http = new HttpClient(server);
        var match = Client(http, TimeSpan.FromMilliseconds(500)).Match("m1");

        var refusal = await Assert.ThrowsAsync<MultiplayerApiException>(
            () => match.OpenStreamAsync(0, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, refusal.Status);
        Assert.False(refusal.FromEnvelope);
        // A handshake that outran the deadline would have thrown a timeout before the stream was
        // asked for; these pin that the refusal body is what was waited on.
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/handshake"));
        Assert.Equal(1, server.CallsTo(HttpMethod.Get, "/stream"));
    }

    /// <summary>The body's own deadline never swallows the caller asking to stop.</summary>
    [Fact]
    public async Task CallerCancellingWhileARefusalBodyStallsStillCancels()
    {
        using var server = new FakeMultiplayerServer();
        server.AnswerOnceUnbuffered(HttpMethod.Get, "/stream", HttpStatusCode.BadGateway);
        using var http = new HttpClient(server);
        var match = Client(http).Match("m1");
        using var cancellation = new CancellationTokenSource();

        var opening = match.OpenStreamAsync(0, cancellation.Token);
        await WaitUntilAsync(() => server.CallsTo(HttpMethod.Get, "/stream") == 1);
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => opening);
    }

    /// <summary>
    /// The bound must not cost the envelope: a verdict that arrives in pieces over an unbuffered
    /// body is still the server's verdict, and still ends the stream.
    /// </summary>
    [Fact]
    public async Task EnvelopeOnAnUnbufferedStreamRefusalIsRead()
    {
        using var server = new FakeMultiplayerServer();
        var body = server.AnswerOnceUnbuffered(
            HttpMethod.Get, "/stream", HttpStatusCode.Forbidden, "application/json");
        body.Write(RevokedEnvelope[..20]);
        body.Write(RevokedEnvelope[20..]);
        body.End();
        using var http = new HttpClient(server);
        var match = Client(http).Match("m1");

        var refusal = await Assert.ThrowsAsync<MultiplayerApiException>(
            () => match.OpenStreamAsync(0, CancellationToken.None));

        Assert.True(refusal.FromEnvelope);
        Assert.Equal(ErrorCode.Forbidden, refusal.Code);
        Assert.Equal("kicked", refusal.Reason);
        Assert.True(refusal.EndsTheStream);
    }

    /// <summary>
    /// A body past the ceiling is not an envelope, on any route, and on a client whose own buffer
    /// ceiling is the two-gigabyte default.
    /// </summary>
    [Theory]
    [InlineData("/stream")]
    [InlineData("/matches")]
    public async Task OversizedRefusalBodyBecomesAStatusOnlyRefusal(string route)
    {
        using var server = new FakeMultiplayerServer();
        var body = server.AnswerOnceUnbuffered(HttpMethod.Get, route, HttpStatusCode.BadGateway);
        body.Write(new string('x', MultiplayerApiException.MaximumBodyBytes + 1));
        body.End();
        using var http = new HttpClient(server);
        var client = Client(http);

        var refusal = await Assert.ThrowsAsync<MultiplayerApiException>(() => route == "/stream"
            ? client.Match("m1").OpenStreamAsync(0, CancellationToken.None)
            : client.ListLobbiesAsync(CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, refusal.Status);
        Assert.False(refusal.FromEnvelope);
    }

    /// <summary>
    /// A proxy page naming a charset .NET does not know is still a proxy page: a status-only
    /// refusal the stream rides out, not an exception no retry policy recognises.
    /// </summary>
    [Fact]
    public async Task RefusalWithAnUnknownCharsetBecomesAStatusOnlyRefusal()
    {
        using var server = new FakeMultiplayerServer();
        var body = server.AnswerOnceUnbuffered(
            HttpMethod.Get, "/stream", HttpStatusCode.BadGateway, "text/html; charset=bogus");
        body.Write("<html>Bad Gateway</html>");
        body.End();
        using var http = new HttpClient(server);
        var match = Client(http).Match("m1");

        var refusal = await Assert.ThrowsAsync<MultiplayerApiException>(
            () => match.OpenStreamAsync(0, CancellationToken.None));

        Assert.Equal(HttpStatusCode.BadGateway, refusal.Status);
        Assert.False(refusal.FromEnvelope);
        Assert.False(refusal.EndsTheStream);
    }

    /// <summary>
    /// A successful answer is bounded by the client's own ceiling even on an HttpClient that was
    /// not built with one.
    /// </summary>
    [Fact]
    public async Task OversizedAnswerIsRefusedOnAClientWithoutItsOwnBound()
    {
        using var server = new FakeMultiplayerServer();
        var body = server.AnswerOnceUnbuffered(HttpMethod.Get, "/matches", HttpStatusCode.OK);
        body.Write(new string(' ', (int)MultiplayerClientOptions.MaximumResponseBytes + 1));
        body.End();
        using var http = new HttpClient(server);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => Client(http).ListLobbiesAsync(CancellationToken.None));
    }

    private static MultiplayerClient Client(HttpClient http, TimeSpan? timeout = null) =>
        new MultiplayerClient(http, new MultiplayerClientOptions(new Uri("http://server.test"), timeout))
            .WithToken("cop_test");

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var patience = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        while (!condition()) await Task.Delay(10, patience.Token);
    }
}
