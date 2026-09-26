using System.Net;
using System.Text;
using System.Text.Json;
using Rechaos.Core.GameModel;
using Rechaos.Multiplayer.Http;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Where this client and the TypeScript coordination server have to spell something the same way:
/// the keys of the settings blob the server reads, and the refusal reasons it sends back.
/// </summary>
public sealed class MultiplayerServerAgreementTests
{
    private static MultiplayerGameSettings LateJoinSettings() => new(
        ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal,
        [0, 1, 2, 3, 4, 5], AiPolicyMode.Advanced, AllowLateJoin: true);

    /// <summary>
    /// The server opens the late-join door on <c>gameSettings.allowLateJoin === true</c>
    /// (<c>LobbyService.joinRunning</c>, <c>MatchQueryService</c>), so the blob has to carry exactly
    /// that key. It used to carry <c>AllowLateJoin</c>, and no match could ever be joined late.
    /// </summary>
    [Fact]
    public void TheSettingsBlobIsWrittenInTheCamelCaseTheServerReads()
    {
        var blob = LateJoinSettings().ToWire();

        Assert.Equal(
            ["aiMentality", "aiPolicy", "allowLateJoin", "duration", "portraits", "scenario"],
            blob.Keys.Order(StringComparer.Ordinal));
        Assert.Equal(JsonValueKind.True, blob["allowLateJoin"].ValueKind);
    }

    /// <summary>
    /// A match created by a build that wrote PascalCase keys is stored verbatim for its whole life,
    /// so it still has to read back — otherwise every resumable match would become unreadable.
    /// </summary>
    [Fact]
    public void ABlobStoredWithPascalCaseKeysStillReads()
    {
        var legacy = new Dictionary<string, JsonElement>
        {
            ["Scenario"] = JsonSerializer.SerializeToElement((int)ScenarioId.Greed),
            ["Duration"] = JsonSerializer.SerializeToElement((int)GameDuration.SixMonths),
            ["AiMentality"] = JsonSerializer.SerializeToElement((int)AiDifficulty.Criminal),
            ["Portraits"] = JsonSerializer.SerializeToElement(new short[] { 5, 4, 3, 2, 1, 0 }),
            ["AiPolicy"] = JsonSerializer.SerializeToElement((int)AiPolicyMode.Advanced),
            ["AllowLateJoin"] = JsonSerializer.SerializeToElement(true),
            // What the coordinator adds to the blob at runtime, which is camelCase either way.
            ["seatSummaries"] = JsonSerializer.SerializeToElement(Array.Empty<object>()),
        };

        var settings = MultiplayerGameSettings.FromWire(legacy);

        Assert.Equal(ScenarioId.Greed, settings.Scenario);
        Assert.Equal(GameDuration.SixMonths, settings.Duration);
        Assert.Equal(AiDifficulty.Criminal, settings.AiMentality);
        Assert.Equal([5, 4, 3, 2, 1, 0], settings.Portraits);
        Assert.Equal(AiPolicyMode.Advanced, settings.AiPolicy);
        Assert.True(settings.AllowLateJoin);
    }

    [Fact]
    public void TheCamelCaseBlobRoundTrips()
    {
        var settings = LateJoinSettings();

        var restored = MultiplayerGameSettings.FromWire(settings.ToWire());

        Assert.Equal(settings.Scenario, restored.Scenario);
        Assert.Equal(settings.Duration, restored.Duration);
        Assert.Equal(settings.AiMentality, restored.AiMentality);
        Assert.Equal(settings.Portraits, restored.Portraits);
        Assert.Equal(settings.AiPolicy, restored.AiPolicy);
        Assert.True(restored.AllowLateJoin);
    }

    /// <summary>
    /// Each code is the one the server writes (the kernel's <c>LobbyService</c>, and the events route
    /// for <c>unreadable_event</c>); a map written against codes it never sends fell through to the
    /// raw HTTP line.
    /// </summary>
    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "wrong_password", "Wrong password.")]
    [InlineData(HttpStatusCode.Unauthorized, "password_required", "That match needs a password.")]
    [InlineData(HttpStatusCode.NotFound, "unknown_join_code", "No lobby is open with that code.")]
    [InlineData(HttpStatusCode.Conflict, "match_not_in_lobby", "That match has already started.")]
    [InlineData(HttpStatusCode.Conflict, "match_not_joinable", "That match has already started.")]
    [InlineData(HttpStatusCode.NotFound, "unknown_player", "That player is no longer in this match.")]
    [InlineData(HttpStatusCode.Conflict, "unreadable_event",
        "The server holds a match event it cannot read, so this match cannot be resumed.")]
    [InlineData(HttpStatusCode.Unauthorized, "invalid_token", "You are no longer in this match.")]
    [InlineData(HttpStatusCode.Forbidden, "kicked", "You are no longer in this match.")]
    public async Task RefusalsReadAsTheServerMeansThem(
        HttpStatusCode status, string reason, string expected)
    {
        var refusal = await RefusalAsync(status, reason);

        Assert.Equal(expected, MultiplayerFailureText.Describe(refusal));
    }

    /// <summary>
    /// <c>unknown_player</c> names the target of a kick or a takeover vote, never the caller, so a
    /// vote on a seat that has just gone must not end the voter's own session.
    /// </summary>
    [Fact]
    public async Task AMissingKickOrVoteTargetIsNotTheCallersMembershipGoing()
    {
        Assert.False(MultiplayerFailureText.IsMembershipRevoked(
            await RefusalAsync(HttpStatusCode.NotFound, "unknown_player")));
        Assert.True(MultiplayerFailureText.IsMembershipRevoked(
            await RefusalAsync(HttpStatusCode.Unauthorized, "invalid_token")));
    }

    private static async Task<MultiplayerApiException> RefusalAsync(HttpStatusCode status, string reason)
    {
        var envelope = JsonSerializer.Serialize(new
        {
            error = new { code = "conflict", message = "refused", details = new { reason } },
        });
        using var response = new HttpResponseMessage(status)
        {
            Content = new StringContent(envelope, Encoding.UTF8, "application/json"),
        };
        return await MultiplayerApiException.FromResponseAsync(
            response, TestContext.Current.CancellationToken);
    }
}
