using System.Net;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Votes to remove a seat (docs/DECISIONS.md, "Let the other players remove a seat by unanimous
/// vote"): what the session sends, and what it tells the interface about an open one.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    [Fact]
    public async Task ARemovalVoteIsReportedAsItIsCastAndWhenItCloses()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;

        server.Events.Write(Frame(8, "match.removalVoteCast",
            """{"playerId":"p2","voterPlayerId":"p1","decision":"remove"}"""));
        var opened = await WaitFor<MultiplayerNotice.RemovalVoteChanged>(session);

        Assert.Equal("p2", opened.PlayerId);
        Assert.Equal(RemovalChoice.Remove, Assert.Single(opened.Votes).Value);

        server.Events.Write(Frame(9, "match.removalVoteClosed", """{"playerId":"p2","removed":false}"""));
        var closed = await WaitFor<MultiplayerNotice.RemovalVoteClosed>(session);

        Assert.Equal("p2", closed.PlayerId);
        Assert.False(closed.Removed);
    }

    [Fact]
    public async Task ARemovalVoteIsPostedForTheNamedSeat()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Post, $"/matches/{MatchId}/players/p2/removal-vote", null,
            HttpStatusCode.NoContent);

        await session.VoteOnRemovalAsync(
            "p2", RemovalChoice.Remove, TestContext.Current.CancellationToken);

        var request = Assert.Single(server.Requests,
            recorded => recorded.Path.EndsWith("/players/p2/removal-vote", StringComparison.Ordinal));
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("""{"decision":"remove"}""", request.Body);
    }

    /// <summary>A refused vote comes back as a notice, so the player can cast it again.</summary>
    [Fact]
    public async Task ARefusedRemovalVoteIsReportedToTheInterface()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(HttpMethod.Post, $"/matches/{MatchId}/players/p2/removal-vote",
            """{"error":{"code":"conflict","message":"gone","details":{"reason":"already_removed"}}}""",
            HttpStatusCode.Conflict);

        await session.VoteOnRemovalAsync(
            "p2", RemovalChoice.Keep, TestContext.Current.CancellationToken);
        var failed = await WaitFor<MultiplayerNotice.RemovalVoteFailed>(session);

        Assert.Equal("p2", failed.PlayerId);
        Assert.Equal(RemovalChoice.Keep, failed.Choice);
    }
}
