using Rechaos.Game;
using Rechaos.Multiplayer.Http;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// When every seat is ready and no sealed turn arrives, the client resynchronises, but only after
/// the stream has had the chance to notice its own dead connection and come back.
/// </summary>
public sealed class OnlineResolutionWatchdogTests
{
    private static readonly TimeSpan Start = TimeSpan.FromMinutes(10);

    /// <summary>
    /// The grace outlasts the stream's idle detector plus its first reconnect, including that
    /// reconnect's own connect deadline, so the recovery already on its way is not pre-empted.
    /// </summary>
    [Fact]
    public void TheGraceOutlastsTheStreamsOwnRecovery()
    {
        var firstReconnect = RetryPolicy.Stream.InitialDelay
            + new MultiplayerClientOptions(new Uri("http://server.test")).EffectiveTimeout;

        Assert.True(
            OnlineResolutionWatchdog.Grace > MatchEventStream.DefaultIdleTimeout + firstReconnect,
            $"grace {OnlineResolutionWatchdog.Grace} against {MatchEventStream.DefaultIdleTimeout} + {firstReconnect}");
    }

    [Fact]
    public void ExpectsASealOnlyOnceEverySeatIsReadyAndTheOwnSubmissionLanded()
    {
        var online = Waiting();
        OnlineResolutionWatchdog.Track(online, Start);
        Assert.Equal(Start, online.ResolutionExpectedSince);

        // A grace already running keeps its start.
        OnlineResolutionWatchdog.Track(online, Start + TimeSpan.FromSeconds(5));
        Assert.Equal(Start, online.ResolutionExpectedSince);

        foreach (var notYet in new Action<MultiplayerUiState>[]
                 {
                     state => state.IsConnected = false,
                     state => state.Stage = MultiplayerStage.Playing,
                     state => state.ReadySubmissionAcknowledged = false,
                     state => state.ReadySlots = new HashSet<int> { 0 },
                     state => state.AwaitedSlots = new HashSet<int>(),
                 })
        {
            var state = Waiting();
            state.ResolutionExpectedSince = Start;
            notYet(state);
            OnlineResolutionWatchdog.Track(state, Start + TimeSpan.FromSeconds(1));
            Assert.Null(state.ResolutionExpectedSince);
        }
    }

    [Fact]
    public void FiresAtTheGraceAndAgainAfterAnotherOne()
    {
        var online = Waiting();
        OnlineResolutionWatchdog.Track(online, Start);
        var grace = OnlineResolutionWatchdog.Grace;

        Assert.False(OnlineResolutionWatchdog.Expire(online, Start + grace - TimeSpan.FromMilliseconds(1)));
        Assert.Equal(Start, online.ResolutionExpectedSince);
        Assert.True(OnlineResolutionWatchdog.Expire(online, Start + grace));

        // Re-armed rather than cleared: the same silence asks again one grace later, not every frame.
        Assert.Equal(Start + grace, online.ResolutionExpectedSince);
        Assert.False(OnlineResolutionWatchdog.Expire(online, Start + grace + TimeSpan.FromSeconds(1)));
        Assert.True(OnlineResolutionWatchdog.Expire(online, Start + grace + grace));
    }

    [Fact]
    public void NeverFiresWithoutAnExpectation()
    {
        var online = new MultiplayerUiState();

        Assert.False(OnlineResolutionWatchdog.Expire(online, Start + TimeSpan.FromDays(1)));
        Assert.Null(online.ResolutionExpectedSince);
    }

    /// <summary>A two-seat turn this client has finished, with the other seat ready too.</summary>
    private static MultiplayerUiState Waiting() => new()
    {
        IsConnected = true,
        Stage = MultiplayerStage.WaitingForSeal,
        ReadySubmissionAcknowledged = true,
        ReadySlots = new HashSet<int> { 0, 1 },
        AwaitedSlots = new HashSet<int> { 0, 1 },
    };
}
