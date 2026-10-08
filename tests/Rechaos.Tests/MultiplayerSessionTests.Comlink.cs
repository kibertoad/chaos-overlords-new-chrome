using System.Net;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The Comlink keys of a running session (DEV-NET-001): the seat publishes its own when the
/// roster shows none, and learns the keys the other seats publish while it plays.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task PublishesItsComlinkKeyWhenTheRosterShowsNone()
    {
        var (session, server, http) = Running(configure: fake =>
            fake.Answer(HttpMethod.Put, "/comlink-key", null, HttpStatusCode.NoContent));
        using var _ = http;
        await using var __ = session;

        await Until(() => server.CallsTo(HttpMethod.Put, "/comlink-key") == 1, "the key publish");

        var publish = server.Requests.Single(request => request.Path.EndsWith("/comlink-key", StringComparison.Ordinal));
        Assert.Contains(session.Comlink.Own.PublicKey, publish.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LearnsAKeyAnotherSeatPublishesDuringTheMatch()
    {
        using var grace = ComlinkKeyPair.Generate();
        var (session, server, http) = Running(configure: fake =>
            fake.Answer(HttpMethod.Put, "/comlink-key", null, HttpStatusCode.NoContent));
        using var _ = http;
        await using var __ = session;
        Assert.Null(session.Comlink.KeyFor(new PlayerId(1)));

        server.Events.Write(Frame(
            8, "match.comlinkKeyPublished", $$"""{"playerId":"p2","comlinkKey":"{{grace.PublicKey}}"}"""));

        await Until(() => session.Comlink.KeyFor(new PlayerId(1)) == grace.PublicKey, "the learned key");
    }
}
