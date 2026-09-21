using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void OriginalTimerSeedZeroExtendsOnlyTheLowSixteenBits()
    {
        Assert.Equal(0, DeterministicRandom.SeedFromTimerMilliseconds(0x1234_0000));
        Assert.Equal(0x5678, DeterministicRandom.SeedFromTimerMilliseconds(0x1234_5678));
        Assert.Equal(ushort.MaxValue,
            DeterministicRandom.SeedFromTimerMilliseconds(uint.MaxValue));
    }

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
    public void OriginalInclusiveWrapperClampsNonpositiveBoundsAndStillConsumesThreeRawValues()
    {
        var random = new DeterministicRandom(1);

        Assert.Equal(1, random.NextInclusive(0));
        Assert.Equal(1, random.NextInclusive(-17));
        Assert.Equal(6, random.ConsumptionCount);
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
        Assert.Equal(MatchStateHasher.ComputeFingerprint(first), MatchStateHasher.ComputeFingerprint(second));

        first.FinishUpkeep();
        second.FinishUpkeep();
        Assert.True(first.Submit(new GameCommand(new PlayerId(0), new GangId(10), GangAction.Hide, CommandTarget.None)).Accepted);

        Assert.NotEqual(MatchStateHasher.ComputeFingerprint(first), MatchStateHasher.ComputeFingerprint(second));
    }

    [Fact]
    public void FamilySixCoverageContributesToCanonicalHash()
    {
        var first = CreateMatch();
        var second = CreateMatch();

        first.AiPlanning.SetCoverageSector(new PlayerId(1), 0, 23);

        Assert.NotEqual(
            MatchStateHasher.ComputeFingerprint(first),
            MatchStateHasher.ComputeFingerprint(second));
    }

    [Fact]
    public void EventHistoryIsReadOnlyAndCachedEncodingRemainsCanonical()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Attack,
            CommandTarget.Gang(new GangId(20)))).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        match.FinishExecutionPhase();
        var events = Assert.IsAssignableFrom<IList<GameEvent>>(match.Events);

        Assert.True(events.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => events[0] = events[0] with
        {
            Turn = events[0].Turn + 1
        });
        var rolls = Assert.IsAssignableFrom<IList<int>>(
            Assert.Single(match.Events, gameEvent =>
                gameEvent.Kind == GameEventKind.CommandResolved).Resolution!.Rolls);
        Assert.True(rolls.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => rolls[0]++);

        // The running digest is the chain of every event's canonical encoding, in order.
        UInt128 expected = 0;
        foreach (var gameEvent in match.Events)
        {
            using var entry = new MemoryStream();
            CanonicalEventWriter.Append(entry, gameEvent);
            expected = MatchStateHasher.Chain(expected, entry.ToArray());
        }

        Assert.NotEmpty(match.Events);
        Assert.Equal(expected, match.EventHistoryDigest);
    }

    [Fact]
    public void PhaseHashHistoryDigestChainsEveryBoundaryInOrder()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();

        UInt128 expected = 0;
        foreach (var boundary in match.PhaseHashes)
        {
            using var entry = new MemoryStream();
            PhaseBoundaryHash.WriteCanonical(entry, boundary);
            expected = MatchStateHasher.Chain(expected, entry.ToArray());
        }

        Assert.NotEmpty(match.PhaseHashes);
        Assert.Equal(expected, match.PhaseHashHistoryDigest);
    }

    [Fact]
    public void HistoryDigestsSurviveASaveRoundTrip()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase();
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        stream.Position = 0;

        var restored = NativeSaveSerializer.Load(stream, match.Definitions);

        Assert.Equal(match.EventHistoryDigest, restored.EventHistoryDigest);
        Assert.Equal(match.PhaseHashHistoryDigest, restored.PhaseHashHistoryDigest);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
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
        Assert.Equal(MatchStateHasher.ComputePhaseBoundaryFingerprint(match), boundary.Fingerprint);
        var boundaries = Assert.IsAssignableFrom<IList<PhaseBoundaryHash>>(match.PhaseHashes);
        Assert.True(boundaries.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => boundaries[0] = boundary with { Turn = 2 });
    }

    private static GameNotification Notification(long sequence) =>
        new(sequence, 1, TurnPhase.Command, null, GameNotificationKind.Information);

    private static MatchState CreateMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] playerSetups =
        [
            new(new PlayerId(0), "SMGHUBBLE", PlayerController.Human),
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
