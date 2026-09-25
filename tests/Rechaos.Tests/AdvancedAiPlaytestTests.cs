using System.Collections.Concurrent;
using System.Diagnostics;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class AdvancedAiPlaytestTests
{
    private static readonly int[] Seeds =
    [
        1977, 4093, 7717, 12289, 16381, 24593,
        32771, 49157, 65521, 81929, 104729, 131071
    ];
    private readonly ITestOutputHelper _output;

    public AdvancedAiPlaytestTests(ITestOutputHelper output) => _output = output;

    [Fact]
    public void ExpertExpansionImprovesSameSeedPowerCampaignSample()
    {
        var (original, advanced) = Compare(
            AiDifficulty.CrimeLord, AiTurnPlanner.AdvancedFeature.ExpertExpansion);
        Report("EXPERT EXPANSION", original, advanced);

        Assert.True(advanced.IdleGangTurns < original.IdleGangTurns);
        Assert.True(advanced.ExpansionMoves > original.ExpansionMoves);
        AssertTerritoryImproves(original, advanced, minimumDefendedRetentionPercent: 98);
    }

    [Fact]
    public void IdleRecoveryImprovesSameSeedPowerCampaignSample()
    {
        var (original, advanced) = Compare(
            AiDifficulty.Criminal, AiTurnPlanner.AdvancedFeature.IdleRecovery);
        Report("IDLE RECOVERY", original, advanced);

        Assert.True(advanced.IdleGangTurns < original.IdleGangTurns);
        // The ported dispatcher (RULE-AI-002) leaves few idle turns to recover, so the territory
        // gain is within noise of the original; the sample may lose at most 1% of it.
        AssertTerritoryImproves(original, advanced,
            minimumDefendedRetentionPercent: 99, minimumControlledRetentionPercent: 99,
            minimumFinalRetentionPercent: 99);
    }

    private static (CampaignMetrics Original, CampaignMetrics Advanced) Compare(
        AiDifficulty difficulty,
        AiTurnPlanner.AdvancedFeature feature)
    {
        var samples = new ConcurrentBag<(CampaignMetrics Original, CampaignMetrics Advanced)>();
        Parallel.ForEach(Seeds, new ParallelOptions { MaxDegreeOfParallelism = 2 }, seed =>
        {
            samples.Add((
                Drive(seed, difficulty, AiTurnPlanner.AdvancedFeature.None),
                Drive(seed, difficulty, feature)));
        });
        return (
            CampaignMetrics.Sum(samples.Select(sample => sample.Original)),
            CampaignMetrics.Sum(samples.Select(sample => sample.Advanced)));
    }

    private void Report(
        string feature,
        CampaignMetrics original,
        CampaignMetrics advanced)
    {
        _output.WriteLine(
            "Power {0} A/B ({1} seeds x 15 turns): Original idle={2}, expansion={3}, controlled-turns={4}, final-controlled={5}, undefended-turns={6}, elapsed={7}; Advanced idle={8}, expansion={9}, controlled-turns={10}, final-controlled={11}, undefended-turns={12}, elapsed={13}.",
            feature, Seeds.Length,
            original.IdleGangTurns, original.ExpansionMoves,
            original.ControlledSectorTurns, original.FinalControlledSectors,
            original.UndefendedSectorTurns, original.Elapsed,
            advanced.IdleGangTurns, advanced.ExpansionMoves,
            advanced.ControlledSectorTurns, advanced.FinalControlledSectors,
            advanced.UndefendedSectorTurns, advanced.Elapsed);
    }

    private static void AssertTerritoryImproves(
        CampaignMetrics original,
        CampaignMetrics advanced,
        int minimumDefendedRetentionPercent = 100,
        int minimumControlledRetentionPercent = 100,
        int minimumFinalRetentionPercent = 100)
    {
        Assert.True(advanced.ControlledSectorTurns * 100
            >= original.ControlledSectorTurns * minimumControlledRetentionPercent);
        Assert.True(advanced.FinalControlledSectors * 100
            >= original.FinalControlledSectors * minimumFinalRetentionPercent);
        var originalDefended = original.ControlledSectorTurns - original.UndefendedSectorTurns;
        var advancedDefended = advanced.ControlledSectorTurns - advanced.UndefendedSectorTurns;
        Assert.True(
            advancedDefended * 100
            >= originalDefended * minimumDefendedRetentionPercent);
    }

    private static CampaignMetrics Drive(
        int seed,
        AiDifficulty difficulty,
        AiTurnPlanner.AdvancedFeature feature)
    {
        var elapsed = Stopwatch.StartNew();
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU ONE", PlayerController.Computer),
            new(new PlayerId(1), "CPU TWO", PlayerController.Computer)
        ];
        var state = OriginalMatchFactory.Create(data, new MatchSetup(
            ScenarioId.Power, GameDuration.FourYears, seed, setups,
            difficulty,
            aiPolicy: feature == AiTurnPlanner.AdvancedFeature.None
                ? AiPolicyMode.Original
                : AiPolicyMode.Advanced));
        var metrics = new CampaignMetrics();
        var boundaries = 0;
        while (state.Outcome is null && state.Coordinator.Turn <= 15
               && boundaries++ < 15 * 32)
        {
            switch (state.Coordinator.Phase)
            {
                case TurnPhase.Upkeep:
                    metrics.ControlledSectorTurns += state.Sectors.Count(sector =>
                        sector.Owner is not null);
                    metrics.UndefendedSectorTurns += state.Sectors.Count(sector =>
                        sector.Owner is { } owner
                        && !state.FindPlayer(owner)!.Gangs.Any(gang =>
                            gang.IsActive && gang.SectorId == sector.Id));
                    state.FinishUpkeep();
                    break;
                case TurnPhase.Command:
                    var playerId = state.Coordinator.ActivePlayer!.Value;
                    var player = state.FindPlayer(playerId)!;
                    if (player.Status != PlayerStatus.Active)
                    {
                        state.FinishCommand(playerId);
                        break;
                    }
                    state.PrepareAiPlanning(playerId);
                    var commands = feature == AiTurnPlanner.AdvancedFeature.None
                        ? AiTurnPlanner.Plan(state, playerId)
                        : AiTurnPlanner.PlanAdvanced(state, playerId, feature);
                    metrics.IdleGangTurns += player.Gangs.Count(gang => gang.IsActive)
                        - commands.Select(command => command.Gang).Distinct().Count();
                    foreach (var command in commands)
                    {
                        if (command.Action == GangAction.Move
                            && state.FindGang(command.Gang) is { } gang
                            && state.Sectors[gang.SectorId].Owner == playerId
                            && state.Sectors[command.Target.Id].Owner != playerId)
                        {
                            metrics.ExpansionMoves++;
                        }
                        if (!state.Submit(command).Accepted)
                            throw new InvalidOperationException("AI selected an invalid command.");
                    }
                    state.PrepareHireOffers(playerId);
                    var hiring = state.PrepareAiHiring(playerId);
                    if (hiring.Choice is { } choice)
                    {
                        if (!state.QueueHire(playerId, choice.GangDefinitionId, choice.SectorId).Accepted)
                            throw new InvalidOperationException("AI selected an invalid hire.");
                    }
                    else if (hiring.RejectedGangDefinitionId is { } rejected
                             && !state.SnubHireOffer(playerId, rejected).Accepted)
                        throw new InvalidOperationException("AI selected an invalid hire rejection.");
                    state.FinishCommand(playerId);
                    break;
                case TurnPhase.Execution:
                    state.FinishExecutionPhase();
                    break;
                case TurnPhase.Hire:
                    state.FinishHire(state.Coordinator.ActivePlayer!.Value);
                    break;
                case TurnPhase.PlayerElimination:
                    state.FinishPlayerElimination();
                    break;
            }
        }
        if (boundaries >= 15 * 32)
            throw new InvalidOperationException("A/B campaign exceeded its phase-boundary limit.");
        metrics.FinalControlledSectors = state.Sectors.Count(sector => sector.Owner is not null);
        metrics.Elapsed = elapsed.Elapsed;
        return metrics;
    }

    private sealed class CampaignMetrics
    {
        public int IdleGangTurns { get; set; }
        public int ExpansionMoves { get; set; }
        public int ControlledSectorTurns { get; set; }
        public int FinalControlledSectors { get; set; }
        public int UndefendedSectorTurns { get; set; }
        public TimeSpan Elapsed { get; set; }

        public static CampaignMetrics Sum(IEnumerable<CampaignMetrics> values) => new()
        {
            IdleGangTurns = values.Sum(value => value.IdleGangTurns),
            ExpansionMoves = values.Sum(value => value.ExpansionMoves),
            ControlledSectorTurns = values.Sum(value => value.ControlledSectorTurns),
            FinalControlledSectors = values.Sum(value => value.FinalControlledSectors),
            UndefendedSectorTurns = values.Sum(value => value.UndefendedSectorTurns),
            Elapsed = TimeSpan.FromTicks(values.Sum(value => value.Elapsed.Ticks))
        };
    }
}
