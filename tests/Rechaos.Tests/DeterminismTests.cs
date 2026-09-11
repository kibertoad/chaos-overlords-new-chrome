using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void RandomStreamCanBeRestoredExactly()
    {
        var random = new DeterministicRandom(1996);
        var prefix = Enumerable.Range(0, 4).Select(_ => random.NextRaw()).ToArray();
        var restored = new DeterministicRandom(random.State, random.ConsumptionCount);

        var suffix = Enumerable.Range(0, 8).Select(_ => random.NextRaw()).ToArray();
        var restoredSuffix = Enumerable.Range(0, 8).Select(_ => restored.NextRaw()).ToArray();

        Assert.Equal(4, prefix.Distinct().Count());
        Assert.Equal(suffix, restoredSuffix);
        Assert.Equal(random.ConsumptionCount, restored.ConsumptionCount);
    }

    [Fact]
    public void BoundedRandomValuesStayInRangeAndTrackRawConsumption()
    {
        var random = new DeterministicRandom(-1);

        var values = Enumerable.Range(0, 100).Select(_ => random.NextInt(7)).ToArray();

        Assert.All(values, value => Assert.InRange(value, 0, 6));
        Assert.Equal(values.Length * 3, random.ConsumptionCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
    }

    [Fact]
    public void RawStepMatchesRecoveredVisualCppSequence()
    {
        var random = new DeterministicRandom(1);

        Assert.Equal([41, 18467, 6334, 26500, 19169],
            Enumerable.Range(0, 5).Select(_ => random.NextRaw()));
    }

    [Fact]
    public void NotificationQueueDropsOldestAtItsExplicitBound()
    {
        var queue = new NotificationQueue(capacity: 2);
        queue.Enqueue(Notification(0));
        queue.Enqueue(Notification(1));
        queue.Enqueue(Notification(2));

        Assert.Equal([1L, 2L], queue.Items.Select(item => item.Sequence));
        Assert.True(queue.TryDequeue(out var first));
        Assert.Equal(1, first!.Sequence);

    }

    [Fact]
    public void CanonicalHashMatchesEquivalentStateAndChangesWithCommands()
    {
        var first = CreateMatch();
        var second = CreateMatch();
        Assert.Equal(MatchStateHasher.ComputeSha256(first), MatchStateHasher.ComputeSha256(second));

        first.FinishUpkeep();
        second.FinishUpkeep();
        Assert.True(first.Submit(new GameCommand(new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None)).Accepted);

        Assert.NotEqual(MatchStateHasher.ComputeSha256(first), MatchStateHasher.ComputeSha256(second));
    }

    [Fact]
    public void FamilySixCoverageContributesToCanonicalHash()
    {
        var first = CreateMatch();
        var second = CreateMatch();

        first.AiPlanning.SetCoverageSector(new PlayerId(1), 0, 23);

        Assert.NotEqual(
            MatchStateHasher.ComputeSha256(first),
            MatchStateHasher.ComputeSha256(second));
    }

    [Fact]
    public void MatchTransitionCapturesResultingPhaseHash()
    {
        var match = CreateMatch();

        var transition = match.FinishUpkeep();

        var boundary = Assert.Single(match.PhaseHashes);
        Assert.Equal(transition.Turn, boundary.Turn);
        Assert.Equal(transition.Phase, boundary.Phase);
        Assert.Equal(transition.ExecutionPhase, boundary.ExecutionPhase);
        Assert.Equal(MatchStateHasher.ComputeSha256(match), boundary.Sha256);
    }

    private static GameNotification Notification(long sequence) =>
        new(sequence, 1, TurnPhase.Command, null, GameNotificationKind.Information);

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, playerSetups);
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500, [new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5)]),
            new(setup.Players[1], 500, [new MatchGangState(new GangId(20), new PlayerId(1), 2, 0, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
