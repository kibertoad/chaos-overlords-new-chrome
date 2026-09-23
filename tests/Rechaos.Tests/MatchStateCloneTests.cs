using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Generated;
using Rechaos.Multiplayer.Session;
using Xunit;
using WirePlayerStatus = Rechaos.Multiplayer.Generated.PlayerStatus;

namespace Rechaos.Tests;

public sealed class MatchStateCloneTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void OnlineSnapshotPreservesDecayedActiveCrackdownDuration(int remaining)
    {
        var definitions = BundledOriginalData.Load();
        var state = State(definitions);
        state.Sectors[29].CrackdownTurnsRemaining = remaining;

        var restored = MatchStateClone.Of(state, definitions);

        Assert.Equal(remaining, restored.Sectors[29].CrackdownTurnsRemaining);
        Assert.True(restored.Sectors[29].CrackdownActive);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(state), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void OnlineSnapshotIsCompressedAndRoundTrips()
    {
        var definitions = BundledOriginalData.Load();
        var state = State(definitions);
        using var raw = new MemoryStream();
        NativeSaveSerializer.Save(raw, state);

        var body = MatchStateClone.ToBase64(state);
        var encoded = Convert.FromBase64String(body);
        var restored = MatchStateClone.FromBase64(body, definitions);

        Assert.Equal("RCHS", System.Text.Encoding.ASCII.GetString(encoded, 0, 4));
        Assert.True(encoded.Length < raw.Length / 2);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(state), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    public void EvictedReadComlinkSequenceSurvivesSaveAndSpeculativeClone()
    {
        var definitions = BundledOriginalData.Load();
        var state = State(definitions);
        var sender = new PlayerId(0);
        var recipient = new PlayerId(1);
        state.FinishUpkeep();
        Assert.True(state.SendComlinkMessage(sender, [recipient], "FIRST").Accepted);
        Assert.True(state.MarkComlinkRead(recipient, 0));
        for (var index = 0; index < MatchLimits.ComlinkMessagesPerPlayer; index++)
            Assert.True(state.SendComlinkMessage(sender, [recipient], $"NEXT {index}").Accepted);
        Assert.Empty(state.ComlinkFor(recipient).ReadSequences);
        Assert.Equal(0, state.ComlinkFor(recipient).LegacyReadThroughSequence);

        var cloned = MatchStateClone.Of(state, definitions);
        using var originalSave = new MemoryStream();
        using var clonedSave = new MemoryStream();
        NativeSaveSerializer.Save(originalSave, state);
        NativeSaveSerializer.Save(clonedSave, cloned);

        Assert.Equal(0, cloned.ComlinkFor(recipient).LegacyReadThroughSequence);
        Assert.Equal(originalSave.ToArray(), clonedSave.ToArray());
    }

    [Fact]
    public void OnlineSnapshotStillReadsLegacyUncompressedBody()
    {
        var definitions = BundledOriginalData.Load();
        var state = State(definitions);
        using var raw = new MemoryStream();
        NativeSaveSerializer.Save(raw, state);

        var restored = MatchStateClone.FromBase64(
            Convert.ToBase64String(raw.ToArray()), definitions);

        Assert.Equal(MatchStateHasher.ComputeFingerprint(state), MatchStateHasher.ComputeFingerprint(restored));
    }

    [Fact]
    [Trait("Category", "LongRunning")]
    public void HundredTurnExceptionalSnapshotFitsTheWireLimitWhenCompressed()
    {
        var definitions = BundledOriginalData.Load();
        var result = HeadlessMatchRunner.Run(
            definitions,
            new HeadlessMatchOptions(
                ScenarioId.Power,
                GameDuration.FourYears,
                4093,
                ThroughTurn: 100),
            cancellationToken: TestContext.Current.CancellationToken);

        var body = MatchStateClone.ToBase64(result.State);

        Assert.True(result.State.Coordinator.Turn > 100);
        Assert.NotEmpty(result.State.Events);
        Assert.True(System.Text.Encoding.UTF8.GetByteCount(body) < 1024 * 1024);
        Assert.Equal(
            result.StateHash,
            MatchStateHasher.ComputeFingerprint(MatchStateClone.FromBase64(body, definitions)));
    }

    private static MatchState State(OriginalData definitions)
    {
        var settings = new MultiplayerGameSettings(
            ScenarioId.Greed,
            GameDuration.SixMonths,
            AiDifficulty.Criminal,
            [0, 1, 2, 3, 4, 5]);
        PlayerView[] roster =
        [
            new("p1", 0, "ADA", PortraitId: 0, Status: WirePlayerStatus.Active, IsHost: true),
            new("p2", 1, "GRACE", PortraitId: 1, Status: WirePlayerStatus.Active, IsHost: false),
        ];
        return MatchBootstrapFactory.Create(definitions, 1996, settings, roster);
    }
}
