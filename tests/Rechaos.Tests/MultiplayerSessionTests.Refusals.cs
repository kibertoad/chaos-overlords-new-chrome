using System.Net;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// What the session does when the server refuses an order document.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A finished turn whose document the server refuses outright is handed back, not left ready.
    /// </summary>
    /// <remarks>
    /// The server never recorded the document, so the turn is still waiting on this seat. Readiness
    /// accumulates for a turn, and without being withdrawn every later draft of it would say
    /// "done" again for a player who is changing it.
    /// </remarks>
    [Fact]
    public async Task WithdrawsReadinessWhenTheFinishedDocumentItselfIsRefused()
    {
        var (session, server, http) = Running(configure: fake => fake.AnswerOnce(
            HttpMethod.Put,
            "/orders",
            new ErrorEnvelope(new ErrorEnvelopeError(
                ErrorCode.ValidationFailed,
                "Orders may only act for your own slot",
                new ErrorEnvelopeErrorDetails("foreign_slot_ops"),
                RequestId: null)),
            HttpStatusCode.UnprocessableEntity));
        using var _ = http;
        await using var __ = session;

        session.QueueOrders(1, EmptyOrders, ready: true);
        var refused = await WaitFor<MultiplayerNotice.OrdersRefused>(session);
        Assert.True(refused.ReadinessWithdrawn);

        session.QueueOrders(1, EmptyOrders, ready: false);
        await WaitFor<MultiplayerNotice.OrdersAccepted>(session);
        Assert.Contains(
            "\"ready\":false",
            server.BodiesSentTo(HttpMethod.Put, "/turns/1/orders")[^1],
            StringComparison.Ordinal);
    }
}
