using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Protocol;
using Xunit;

namespace Rechaos.Tests;

public sealed class RetryPolicyTests
{
    [Fact]
    public void MatchTrafficRetriesForFiveMinutes() =>
        Assert.Equal(TimeSpan.FromMinutes(5), RetryPolicy.Call.MaxElapsed);

    [Fact]
    public void EventStreamRetriesForFiveMinutes() =>
        Assert.Equal(TimeSpan.FromMinutes(5), RetryPolicy.Stream.MaxElapsed);

    [Fact]
    public void ProtocolErrorsAreNotRetried() =>
        Assert.False(TransientFailure.IsTransient(new MultiplayerProtocolException("bad frame")));

    [Fact]
    public async Task ExhaustionReportsAttemptCountAndLastFailure()
    {
        var calls = 0;
        var exception = await Assert.ThrowsAsync<RetryExhaustedException>(() =>
            TransientFailure.CallAsync<int>(
                _ =>
                {
                    calls++;
                    throw new HttpRequestException("connection refused");
                },
                new RetryPolicy(TimeSpan.Zero, TimeSpan.Zero, MaxAttempts: 3),
                onRetry: null,
                CancellationToken.None));

        Assert.Equal(3, calls);
        Assert.Equal(3, exception.Attempts);
        Assert.Contains("connection refused", exception.Message, StringComparison.Ordinal);
    }
}
