using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class AiTurnPlannerTests
{
    // RULE-AI-003, FND-AI-045: a new match runs the refresh for every player, humans included, so
    // no row is left at zero for its player's first pass or the aliased read of RULE-AI-005. The
    // human sees every gang from the start, so its row weighs each computer headquarters 1.
    [Fact]
    public void NewMatchCachesEveryPlayersSectorWeights()
    {
        var match = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
            ScenarioId.Power, GameDuration.SixMonths, 1996,
            [
                new MatchPlayerSetup(
                    new PlayerId(0), OriginalSetupNameRules.OmniscienceName, PlayerController.Human),
                new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Computer),
                new MatchPlayerSetup(new PlayerId(2), "THREE", PlayerController.Computer)
            ]));

        foreach (var player in match.Players)
            Assert.Equal(VisibleWeights(match, player.Id), CachedWeights(match, player.Id));
        var human = new PlayerId(0);
        Assert.All(match.Players.Skip(1), computer =>
            Assert.Equal(1, match.AiPlanning.SectorWeight(human, computer.Gangs[0].SectorId)));
    }

    // RULE-AI-003, RULE-AI-005: a human's row is only ever filled by the start or load pass, and a
    // computer player 0 reads player 1's sector 0 through the aliased read. A load refreshes that
    // row, here to the 10 of a hostile human's gang player 1 sees, and the journal replays it.
    [Fact]
    public void LoadRefreshesAHumanRowForTheAliasedRead()
    {
        var data = BundledOriginalData.Load();
        var match = CreateMatch(controller: PlayerController.Human, data: data,
            definitionId: data.Gangs.MinBy(gang => gang.Stats.Stealth)!.Id,
            rivalDefinitionId: data.Gangs.MaxBy(gang => gang.Stats.Detect)!.Id);
        var human = new PlayerId(1);
        match.Players[1].Gangs[0].SectorId = 0;
        match.AiStrategy.RecordCombat(new PlayerId(0), human, openingDamage: 10);
        var recorder = new MatchReplayRecorder(match);
        Assert.True(match.AiStrategy.IsHostile(human, new PlayerId(0)));
        Assert.Equal(0, match.AiPlanning.SectorWeight(human, 0));

        recorder.RefreshAiSectorRecords();

        Assert.Equal(10, match.AiPlanning.SectorWeight(human, 0));
        Assert.Equal(VisibleWeights(match, human), CachedWeights(match, human));
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var restored = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(10, restored.AiPlanning.SectorWeight(human, 0));
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match),
            MatchStateHasher.ComputeFingerprint(restored));
    }

    private static byte[] VisibleWeights(MatchState match, PlayerId observer)
    {
        var weights = new byte[MatchLimits.SectorCount];
        AiTurnPlanner.ComputeVisibleWeights(match, observer, weights);
        return weights;
    }

    private static byte[] CachedWeights(MatchState match, PlayerId player) =>
        Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(sectorId => (byte)match.AiPlanning.SectorWeight(player, sectorId))
            .ToArray();
}
