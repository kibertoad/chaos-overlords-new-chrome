using System.Net;
using System.Reflection;
using Rechaos.Game;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>What the session hands the game, read through the game's own handling of it.</summary>
public sealed partial class MultiplayerSessionTests
{
    /// <summary>
    /// A vote the server refused reaches the player as a message telling them to vote again.
    /// </summary>
    /// <remarks>
    /// The interface fires a vote and forgets it, so the notice is the only way the player learns
    /// that the modal's answer did not land and has to be given again.
    /// </remarks>
    [Fact]
    public async Task ARefusedTakeoverVoteReachesTheInterface()
    {
        var (session, server, http) = Running();
        using var _ = http;
        await using var __ = session;
        server.Answer(
            HttpMethod.Post,
            "/players/p2/takeover-vote",
            Envelope("takeover_not_pending"),
            HttpStatusCode.Conflict);

        await session.VoteOnTakeoverAsync(
            "p2", TakeoverChoice.Computer, TestContext.Current.CancellationToken);
        var failed = await WaitFor<MultiplayerNotice.TakeoverVoteFailed>(session);

        Assert.Equal("p2", failed.PlayerId);
        Assert.Equal(TakeoverChoice.Computer, failed.Choice);
        Assert.False(string.IsNullOrWhiteSpace(failed.Reason));

        var game = DeviationBehaviourTests.HeadlessGame();
        ApplyNotice(game, failed);

        const string Told = "THE VOTE DID NOT REACH THE SERVER  TRY AGAIN";
        Assert.Equal(Told, DeviationBehaviourTests.Field("_message").GetValue(game));
        var online = (MultiplayerUiState)DeviationBehaviourTests.Field("_online").GetValue(game)!;
        Assert.Equal(Told, online.Status);
    }

    /// <summary>
    /// The countdown is measured on the server's clock, learned from the <c>Date</c> it sends.
    /// </summary>
    /// <remarks>
    /// Here the server runs three minutes behind this machine. Read on the local clock, a turn with
    /// a minute left would already be out of time, and the overdue-seal watchdog would be asking
    /// the server about a seal it has no reason to have made yet.
    /// </remarks>
    [Fact]
    public async Task TheCountdownReadsTheServersClockRatherThanThisMachines()
    {
        var serverAhead = TimeSpan.FromMinutes(-3);
        var deadline = DateTimeOffset.UtcNow + serverAhead + TimeSpan.FromSeconds(60);
        var (session, _, http) = Running(
            deadlineAt: deadline.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
            configure: fake => fake.ServerClockOffset = serverAhead);
        using var client = http;
        await using var running = session;

        await Until(
            () => Math.Abs((session.ServerTimeOffset - serverAhead).TotalSeconds) < 1.5,
            "the server's clock offset was learned");
        var game = DeviationBehaviourTests.HeadlessGame();
        DeviationBehaviourTests.Field("_session").SetValue(game, session);
        var serverNow = (DateTimeOffset)DeviationBehaviourTests.Call(game, "OnlineServerNow")!;

        Assert.InRange(deadline - serverNow, TimeSpan.FromSeconds(55), TimeSpan.FromSeconds(62));
        Assert.False(OnlineDeadlinePolicy.HasPassed(deadline, null, serverNow));
        Assert.True(OnlineDeadlinePolicy.HasPassed(deadline, null, DateTimeOffset.UtcNow));
    }

    private static void ApplyNotice(ChaosGame game, MultiplayerNotice notice) =>
        (typeof(ChaosGame).GetMethod(
                "Apply",
                BindingFlags.Instance | BindingFlags.NonPublic,
                [typeof(MultiplayerNotice)])
            ?? throw new MissingMethodException(nameof(ChaosGame), "Apply(MultiplayerNotice)"))
        .Invoke(game, [notice]);
}
