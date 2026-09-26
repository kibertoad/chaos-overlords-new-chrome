using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Rechaos.Game;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A rate limit is the server answering, not the connection going: the client waits as long as it
/// was asked to, and says what is happening rather than that the network is down.
/// </summary>
public sealed class MultiplayerRateLimitTests
{
    private const string RateLimitedEnvelope =
        """{"error":{"code":"rate_limited","message":"Too many attempts; slow down","details":{"reason":"rate_limited"},"requestId":"r9"}}""";

    [Fact]
    public async Task ARefusalKeepsTheWaitItsRetryAfterAsksFor()
    {
        var failure = await RateLimitedAsync(TimeSpan.FromSeconds(42));

        Assert.True(failure.IsRateLimited);
        Assert.Equal(TimeSpan.FromSeconds(42), failure.RetryAfter);
    }

    [Fact]
    public async Task ARefusalWithoutRetryAfterAsksForNoWait()
    {
        var failure = await RateLimitedAsync(retryAfter: null);

        Assert.Null(failure.RetryAfter);
    }

    /// <summary>A fixed window refuses until it turns over, so the retry waits for it.</summary>
    [Fact]
    public async Task TheRetryWaitsAtLeastAsLongAsTheServerAsked()
    {
        var policy = new RetryPolicy(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), 0);
        var failure = await RateLimitedAsync(TimeSpan.FromSeconds(20));

        Assert.Equal(TimeSpan.FromSeconds(20), policy.DelayAfter(failure, attempt: 1));
        Assert.Equal(
            TimeSpan.FromSeconds(20),
            policy.DelayAfter(new RetryExhaustedException(3, TimeSpan.Zero, failure), attempt: 1));
    }

    /// <summary>Something between the client and the server asking for an hour does not park the loop that long.</summary>
    [Fact]
    public async Task AnUnreasonableRetryAfterIsCapped()
    {
        var policy = new RetryPolicy(TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), 0);
        var failure = await RateLimitedAsync(TimeSpan.FromHours(1));

        Assert.Equal(RetryPolicy.MaxHonouredRetryAfter, policy.DelayAfter(failure, attempt: 1));
    }

    [Fact]
    public void AFailureWithoutRetryAfterKeepsTheBackoff()
    {
        var policy = new RetryPolicy(TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(4), 0);

        var delay = policy.DelayAfter(new IOException("gone"), attempt: 1);

        Assert.InRange(delay, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4));
    }

    /// <summary>
    /// A wait the server asked for is kept whole, so a window with no room left for it closes now
    /// instead of running past its end.
    /// </summary>
    [Fact]
    public async Task AWaitThatWouldOutlastTheWindowEndsItInstead()
    {
        var policy = new RetryPolicy(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), 0,
            MaxElapsed: TimeSpan.FromMinutes(2));
        var failure = await RateLimitedAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(TimeSpan.FromSeconds(60), policy.NextDelay(failure, 1, TimeSpan.FromSeconds(10)));
        Assert.Equal(TimeSpan.FromSeconds(60), policy.NextDelay(failure, 1, TimeSpan.FromSeconds(60)));
        Assert.Null(policy.NextDelay(failure, 1, TimeSpan.FromSeconds(61)));
    }

    [Fact]
    public void SpentAttemptsEndTheWindowWhateverTimeIsLeft()
    {
        var policy = new RetryPolicy(
            TimeSpan.FromMilliseconds(10), TimeSpan.FromMilliseconds(10), MaxAttempts: 3,
            MaxElapsed: TimeSpan.FromMinutes(2));
        var failure = new IOException("gone");

        Assert.NotNull(policy.NextDelay(failure, 2, TimeSpan.Zero));
        Assert.Null(policy.NextDelay(failure, 3, TimeSpan.Zero));
    }

    [Fact]
    public async Task ThePlayerIsToldTheServerIsLimitingRequestsAndForHowLong()
    {
        var failure = await RateLimitedAsync(TimeSpan.FromSeconds(42));

        Assert.Equal(
            "The server is limiting how often this client may call it; it asked to wait 42 s. (request r9)",
            MultiplayerFailureText.Describe(failure));
        Assert.True(MultiplayerFailureText.IsRateLimited(
            new RetryExhaustedException(2, TimeSpan.Zero, failure)));
        Assert.False(MultiplayerFailureText.IsRateLimited(new IOException("gone")));
    }

    [Fact]
    public async Task TheReconnectLogMarksARateLimitedAttempt()
    {
        var failure = await RateLimitedAsync(TimeSpan.FromSeconds(5));
        var state = new MultiplayerUiState();

        state.ReconnectLog.Add(ReconnectAttemptEntry.From(
            new MultiplayerNotice.ConnectionChanged(false, "limited", 1, "stream", failure),
            DateTimeOffset.UnixEpoch));
        Assert.True(state.IsRateLimited);

        state.ReconnectLog.Add(ReconnectAttemptEntry.From(
            new MultiplayerNotice.ConnectionChanged(false, "gone", 2, "stream", new IOException("gone")),
            DateTimeOffset.UnixEpoch));
        Assert.False(state.IsRateLimited);
    }

    private static async Task<MultiplayerApiException> RateLimitedAsync(TimeSpan? retryAfter)
    {
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests)
        {
            Content = new StringContent(RateLimitedEnvelope, Encoding.UTF8, "application/json"),
        };
        if (retryAfter is { } wait) response.Headers.RetryAfter = new RetryConditionHeaderValue(wait);
        return await MultiplayerApiException.FromResponseAsync(
            response, TestContext.Current.CancellationToken);
    }
}
