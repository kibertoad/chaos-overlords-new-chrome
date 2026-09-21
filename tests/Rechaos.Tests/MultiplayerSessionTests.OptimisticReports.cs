using System.Net;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task HandsOverTheNextTurnWhileItsHashReportIsStillInFlight()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        var report = server.BlockOnce(HttpMethod.Post, "/turns/1/report");
        server.Answer(HttpMethod.Get, "/turns/1/orders", SealedOrders(1));
        server.Answer(HttpMethod.Get, "/turns/2/orders", SealedOrders(2));

        server.Events.Write(SealedFrame(8, 1));
        var first = await WaitFor<MultiplayerNotice.TurnResolved>(session);
        await Until(() => server.CallsTo(HttpMethod.Post, "/turns/1/report") == 1, "the blocked report");

        // The report has not received an HTTP response, but it must not hold the event pump or the
        // player's next planning handover hostage.
        server.Events.Write(SealedFrame(9, 2));
        var second = await WaitFor<MultiplayerNotice.TurnResolved>(session);

        Assert.Equal(1, first.Turn);
        Assert.Equal(2, second.Turn);
        report.SetResult();
    }
}
