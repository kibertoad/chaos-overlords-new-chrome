using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The Revised rules setting (DEV-EQUIP-001, DEV-AI-001) is part of the match: it is hashed, saved,
/// replayed and carried to every online seat, and a match without it plays the original.
/// </summary>
public sealed class RuleRevisionsTests
{
    [Fact]
    public void ANewMatchPlaysTheOriginalUnlessTheSetupRevisesIt()
    {
        var setup = Setup(RuleRevisions.None);

        Assert.Equal(RuleRevisions.None, setup.RuleRevisions);
        Assert.False(setup.Revises(RuleRevisions.TransactionsInOrderGiven));
        Assert.False(setup.Revises(RuleRevisions.CorrectedHunterGuard));
        Assert.True(Setup(RuleRevisions.All).Revises(RuleRevisions.CorrectedHunterGuard));
    }

    [Fact]
    public void ASetupRefusesARevisionThisBuildDoesNotHave()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Setup((RuleRevisions)0x80));
    }

    [Fact]
    public void RevisionsContributeToTheStateFingerprint()
    {
        Assert.NotEqual(
            MatchStateHasher.ComputeFingerprint(Match(RuleRevisions.None)),
            MatchStateHasher.ComputeFingerprint(Match(RuleRevisions.All)));
    }

    [Fact]
    public void RevisionsRoundTripThroughSaveAndReplay()
    {
        var match = Match(RuleRevisions.All);
        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, match);
        save.Position = 0;

        Assert.Equal(RuleRevisions.All,
            NativeSaveSerializer.Load(save, match.Definitions).Setup.RuleRevisions);

        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, new MatchReplayRecorder(match));
        replay.Position = 0;
        Assert.Equal(RuleRevisions.All,
            MatchReplaySerializer.LoadAndReplay(replay, match.Definitions).Setup.RuleRevisions);
    }

    [Fact]
    public void OnlineSettingsCarryRevisionsAndABlobWithoutThemPlaysTheOriginal()
    {
        var revised = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal,
            [0, 1, 2, 3, 4, 5], RuleRevisions: RuleRevisions.All);

        Assert.Equal(RuleRevisions.All,
            MultiplayerGameSettings.FromWire(revised.ToWire()).RuleRevisions);
        var older = revised.ToWire().Where(pair => !string.Equals(
                pair.Key, "ruleRevisions", StringComparison.OrdinalIgnoreCase))
            .ToDictionary();
        Assert.Equal(RuleRevisions.None, MultiplayerGameSettings.FromWire(older).RuleRevisions);
    }

    [Fact]
    public void OnlineSettingsRefuseARevisionThisBuildDoesNotHave()
    {
        var blob = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal,
            [0, 1, 2, 3, 4, 5]).ToWire().ToDictionary();
        blob["RuleRevisions"] = JsonSerializer.SerializeToElement(0x80);

        Assert.Throws<MultiplayerProtocolException>(() => MultiplayerGameSettings.FromWire(blob));
    }

    private static MatchSetup Setup(RuleRevisions revisions) => new(
        ScenarioId.Greed, GameDuration.SixMonths, 1996,
        [new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human)],
        ruleRevisions: revisions);

    private static MatchState Match(RuleRevisions revisions) =>
        OriginalMatchFactory.Create(BundledOriginalData.Load(), Setup(revisions));
}
