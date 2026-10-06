using System.Net;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A desync that settles without the host: the designated tie-breaker, and a client that corrects
/// its own report from the server's facts.
/// </summary>
public sealed partial class MultiplayerSessionTests
{
    private static readonly string OtherHash = new('7', 64);

    /// <summary>
    /// A tie is broken by the player the announcement names. With five or six seats that is not
    /// always the host, and a host whose own report is outside the tie cannot claim either hash.
    /// </summary>
    [Fact]
    public async Task TheDesignatedTieBreakerRepairsATieWhenItIsNotTheHost()
    {
        var (session, server, http) = Running(ownPlayerId: "p2", configure: NoSnapshots);
        using var _ = http;
        await using var __ = session;
        Assert.False(session.IsHost);
        var ours = await ResolveTurnOneAsync(session, server);

        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(ours, "p2", ownReport: ours, "p2")));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.True(desynced.IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the repair upload");
        Assert.Contains(ours, server.BodiesSentTo(HttpMethod.Post, "/snapshots")[0], StringComparison.Ordinal);
    }

    /// <summary>The other side of the same rule: holding a tied hash is not enough on its own.</summary>
    [Fact]
    public async Task AClientTheTieDoesNotDesignateWaitsForTheRepair()
    {
        var (session, server, http) = Running(ownPlayerId: "p2", configure: NoSnapshots);
        using var _ = http;
        await using var __ = session;
        var ours = await ResolveTurnOneAsync(session, server);

        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(ours, "p2", ownReport: ours, "p1")));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.False(desynced.IsRepairing);
        Assert.Equal(0, server.CallsTo(HttpMethod.Post, "/snapshots"));
    }

    /// <summary>
    /// A client whose report does not match what the server's own facts rebuild to was wrong about
    /// its own state. It adopts the rebuild and reports again, and it does not impose the state it
    /// had on anybody, even as the host designated to break the tie.
    /// </summary>
    [Fact]
    public async Task AClientWhoseReportWasWrongReportsTheRebuiltStateInsteadOfRepairing()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var correct = await ResolveTurnOneAsync(session, server);
        var reportsBefore = server.CallsTo(HttpMethod.Post, "/turns/1/report");
        server.Answer(HttpMethod.Get, "/snapshots/latest", BootstrapSnapshot(session));

        // The announcement says this client reported a hash its rules do not reach from the
        // bootstrap and the sealed set.
        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(correct, "p1", ownReport: OtherHash, "p1")));
        var resynced = await WaitFor<MultiplayerNotice.Resynced>(session);

        Assert.Equal(correct, resynced.StateHash);
        await Until(
            () => server.CallsTo(HttpMethod.Post, "/turns/1/report") > reportsBefore,
            "turn 1 reported again");
        Assert.Contains(
            correct,
            server.BodiesSentTo(HttpMethod.Post, "/turns/1/report")[^1],
            StringComparison.Ordinal);
        Assert.Equal(1, server.CallsTo(HttpMethod.Post, "/snapshots"));
    }

    /// <summary>
    /// A rebuild that agrees with the report shows the divergence is not this client's own, so the
    /// designated client goes on to post its state as before.
    /// </summary>
    [Fact]
    public async Task AClientWhoseReportHoldsUpStillBreaksTheTie()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 1, "the initial snapshot");
        var ours = await ResolveTurnOneAsync(session, server);
        server.Answer(HttpMethod.Get, "/snapshots/latest", BootstrapSnapshot(session));

        server.Events.Write(Frame(9, "turn.desynced", TiedDesync(ours, "p1", ownReport: ours, "p1")));
        var desynced = await WaitFor<MultiplayerNotice.Desynced>(session);

        Assert.True(desynced.IsRepairing);
        await Until(() => server.CallsTo(HttpMethod.Post, "/snapshots") == 2, "the repair upload");
    }

    private static void NoSnapshots(FakeMultiplayerServer server)
    {
        server.Answer(HttpMethod.Get, "/snapshots/latest", Envelope("no_snapshot"), HttpStatusCode.NotFound);
        server.Answer(HttpMethod.Get, "/snapshots/0", Envelope("no_snapshot"), HttpStatusCode.NotFound);
    }

    /// <summary>
    /// A two-way tie for turn 1 between <paramref name="ours"/> and <see cref="OtherHash"/>, in
    /// which <paramref name="ownId"/> reported <paramref name="ownReport"/>, the other seat the
    /// other hash, and <paramref name="tieBreaker"/> breaks the tie.
    /// </summary>
    private static string TiedDesync(string ours, string ownId, string ownReport, string tieBreaker)
    {
        var peerReport = string.Equals(ownReport, ours, StringComparison.Ordinal) ? OtherHash : ours;
        var (p1, p2) = ownId == "p1" ? (ownReport, peerReport) : (peerReport, ownReport);
        var candidates = new[] { ours, OtherHash }.Order(StringComparer.Ordinal).ToArray();
        return $$"""
            {"turn":1,"reports":[{"playerId":"p1","stateHash":"{{p1}}"},{"playerId":"p2","stateHash":"{{p2}}"}],"candidateStateHashes":["{{candidates[0]}}","{{candidates[1]}}"],"tieBreakerPlayerId":"{{tieBreaker}}"}
            """;
    }
}
