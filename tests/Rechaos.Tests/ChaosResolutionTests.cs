using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

public sealed class ChaosResolutionTests
{
    [Fact]
    public void NewCrackdownRetainsTwoToFourFuturePoliceCombatPhasesAfterImmediateCombat()
    {
        var match = CreateMatch(tolerance: 0);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);

        match.FinishExecutionPhase();

        var duration = match.Sectors[0].CrackdownTurnsRemaining;
        Assert.InRange(duration,
            ManualRules.MinimumCrackdownTurns - 1,
            ManualRules.MaximumCrackdownTurns - 1);
        CrackdownResolver.ResolveUpkeep(match);
        Assert.Equal(duration, match.Sectors[0].CrackdownTurnsRemaining);
        CrackdownResolver.FinishCombat(match);
        Assert.Equal(duration - 1, match.Sectors[0].CrackdownTurnsRemaining);
        for (var phase = 1; phase < duration; phase++)
            CrackdownResolver.FinishCombat(match);
        Assert.False(match.Sectors[0].CrackdownActive);
        Assert.Equal(0, match.Sectors[0].CrackdownTurnsRemaining);
    }

    [Fact]
    public void AnotherCrackdownExtendsExistingPolicePresenceBeforeSameTurnDurationTick()
    {
        var match = CreateMatch(tolerance: 0, crackdownActive: true);
        var remainingBeforeChaos = match.Sectors[0].CrackdownTurnsRemaining;
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);

        Assert.InRange(
            match.Sectors[0].CrackdownTurnsRemaining - remainingBeforeChaos,
            ManualRules.MinimumCrackdownTurns - 1,
            ManualRules.MaximumCrackdownTurns - 1);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)),
            notification => notification.Kind == GameNotificationKind.Crackdown);
    }

    [Fact]
    public void ThirdCrackdownWithinFiveTurnsNeutralizesSectorAndResetsInfluence()
    {
        var match = CreateMatch(owner: new PlayerId(0));
        var sector = match.Sectors[0];
        var site = sector.Sites[1];
        var definition = match.Definitions.Sites.Single(value => value.Id == site.DefinitionId);
        site.InfluencedBy = new PlayerId(0);
        site.Resistance = 0;
        sector.Tolerance += definition.Tolerance;
        match.Players[0].Support = definition.Support;

        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        AdvanceCoordinatorTurn(match);

        var result = CrackdownResolver.Trigger(match, sector);

        Assert.True(result.ControlLost);
        Assert.Equal(new PlayerId(0), result.PreviousOwner);
        Assert.Null(sector.Owner);
        Assert.Null(site.InfluencedBy);
        Assert.Equal(definition.Resistance, site.Resistance);
        Assert.Equal(0, match.Players[0].Support);
        Assert.Equal(20, sector.Tolerance);
        Assert.Equal([5, 5], sector.CrackdownHistory);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)), notification =>
            notification.Kind == GameNotificationKind.ControlLost && notification.SectorId == sector.Id);
    }

    [Fact]
    public void CrackdownsOutsideFiveTurnWindowDoNotNeutralizeSector()
    {
        var match = CreateMatch(owner: new PlayerId(0));
        var sector = match.Sectors[0];
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        for (var index = 0; index < 5; index++) AdvanceCoordinatorTurn(match);

        var result = CrackdownResolver.Trigger(match, sector);

        Assert.False(result.ControlLost);
        Assert.Equal(new PlayerId(0), sector.Owner);
        Assert.Equal([2, 7], sector.CrackdownHistory);
    }

    [Fact]
    public void ThirdCrackdownCountsOldestAtInclusiveFiveTurnBoundary()
    {
        var match = CreateMatch(owner: new PlayerId(0));
        var sector = match.Sectors[0];
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        for (var index = 0; index < 4; index++) AdvanceCoordinatorTurn(match);

        var result = CrackdownResolver.Trigger(match, sector);

        Assert.True(result.ControlLost);
        Assert.Null(sector.Owner);
        Assert.Equal([6, 6], sector.CrackdownHistory);
    }

    [Fact]
    public void RecentCrackdownAfterThirdTriggerCanNeutralizeReacquiredControl()
    {
        var match = CreateMatch(owner: new PlayerId(0));
        var sector = match.Sectors[0];
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        sector.Owner = new PlayerId(0);
        AdvanceCoordinatorTurn(match);

        var result = CrackdownResolver.Trigger(match, sector);

        Assert.True(result.ControlLost);
        Assert.Null(sector.Owner);
        Assert.Equal([4, 4], sector.CrackdownHistory);
    }

    [Fact]
    public void FriendlyGangsEachIncludeSectorIncomeAndControlledSectorPaysEverySuccess()
    {
        var match = CreateMatch(twoPlayerZeroGangs: true, owner: new PlayerId(0), tolerance: 40);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        Assert.Equal(2, match.LastPhaseResolutions.Count);
        var first = match.LastPhaseResolutions[0].Event!.Resolution!;
        var second = match.LastPhaseResolutions[1].Event!.Resolution!;
        var gangs = new[] { match.FindGang(new GangId(10))!, match.FindGang(new GangId(11))! };
        var sectorIncome = SectorIncome(match, 0);
        var expectedDice = gangs.Sum(gang => ManualRules.ChaosDiceCount(
            [(gang.Force, EffectiveStatisticsCalculator.ForGang(match, gang).Chaos)], sectorIncome));
        Assert.Equal(expectedDice, first.Rolls.Count);
        Assert.Equal(first.Rolls, second.Rolls);
        Assert.Equal(first.Successes, first.CashDelta);
        Assert.Equal(0, second.CashDelta);
        Assert.Equal(first.Successes,
            match.LastPhaseResolutions.Sum(result => result.Event!.Resolution!.CashDelta));
        Assert.Equal(cashBefore + first.Successes, match.Players[0].Cash);
        Assert.Equal(first.Successes, match.Players[0].Statistics.CashEarned);
        Assert.Equal(first.Successes, first.ResultValue);
        Assert.Equal(expectedDice * 3, match.Random.ConsumptionCount);
        Assert.All(match.LastPhaseResolutions,
            result => Assert.Equal(GameNotificationKind.Chaos,
                match.NotificationsFor(result.Command.Player)
                    .Single(notification => notification.RelatedEventSequence == result.Event!.Sequence).Kind));
    }

    [Fact]
    public void ChaosRollsAndEventsFollowRosterSlotsRatherThanSubmissionOrder()
    {
        var match = CreateMatch(twoPlayerZeroGangs: true, owner: new PlayerId(0), tolerance: 40);
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 11)).Accepted);
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        var expectedRandom = new DeterministicRandom(
            match.Random.State, match.Random.ConsumptionCount);
        var band = OriginalResolutionRules.Band(match, new PlayerId(0));
        var expectedRolls = new List<int>();
        foreach (var gangId in new[] { new GangId(10), new GangId(11) })
        {
            var gang = match.FindGang(gangId)!;
            var pool = match.Sectors[gang.SectorId].Income + gang.Force
                + EffectiveStatisticsCalculator.ForGang(match, gang).Chaos;
            var dice = OriginalResolutionRules.ActionPool(band, GangAction.Chaos, pool);
            expectedRolls.AddRange(DiceRoller.RollD6(expectedRandom, dice));
        }

        for (var index = 0; index < 3; index++) match.FinishExecutionPhase();
        match.FinishExecutionPhase();

        Assert.Equal([new GangId(10), new GangId(11)],
            match.LastPhaseResolutions.Select(result => result.Command.Gang).ToArray());
        Assert.All(match.LastPhaseResolutions, result =>
            Assert.Equal(expectedRolls, result.Event!.Resolution!.Rolls));
        Assert.Equal(expectedRandom.State, match.Random.State);
        Assert.Equal(expectedRandom.ConsumptionCount, match.Random.ConsumptionCount);
    }

    [Fact]
    public void ChaosRollsBeforeCombatButIncomeWaitsUntilAfterTransactions()
    {
        var match = CreateMatch(owner: new PlayerId(0), tolerance: 40);
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);
        var chaosEvent = Assert.Single(match.Events, gameEvent =>
            gameEvent.Turn == 1 && gameEvent.ExecutionPhase == ExecutionPhase.Chaos);
        Assert.True(chaosEvent.Resolution!.Successes > 0);
        Assert.Equal(chaosEvent.Resolution.Successes, chaosEvent.Resolution.ResultValue);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.All(match.NotificationsFor(new PlayerId(0)).Where(notification =>
                notification.Kind == GameNotificationKind.Chaos),
            notification => Assert.Equal(ExecutionPhase.Chaos, notification.ExecutionPhase));

        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Transaction, match.Coordinator.ExecutionPhase);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Chaos, match.Coordinator.ExecutionPhase);
        Assert.Equal(cashBefore, match.Players[0].Cash);

        match.FinishExecutionPhase();

        Assert.Equal(cashBefore + chaosEvent.Resolution.CashDelta, match.Players[0].Cash);
        Assert.Equal(chaosEvent.Resolution.CashDelta, match.Players[0].Statistics.CashEarned);
    }

    [Fact]
    public void NewlyTriggeredCrackdownParticipatesInSameTurnCombatAndDurationTick()
    {
        var match = CreateMatch(tolerance: 0);
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));

        match.FinishExecutionPhase();

        Assert.Equal(ExecutionPhase.Combat, match.Coordinator.ExecutionPhase);
        Assert.True(match.Sectors[0].CrackdownActive);
        var durationBeforeCombat = match.Sectors[0].CrackdownTurnsRemaining;
        Assert.InRange(durationBeforeCombat,
            ManualRules.MinimumCrackdownTurns,
            ManualRules.MaximumCrackdownTurns);

        match.FinishExecutionPhase();

        Assert.NotEmpty(match.LastPoliceAttackResolutions);
        Assert.Equal(durationBeforeCombat - 1, match.Sectors[0].CrackdownTurnsRemaining);
    }

    [Fact]
    public void PreparedChaosPassSurvivesSaveBeforeDelayedIncome()
    {
        var original = CreateMatch(owner: new PlayerId(0), tolerance: 40);
        original.FinishUpkeep();
        Assert.True(original.Submit(Chaos(0, 10)).Accepted);
        original.FinishCommand(new PlayerId(0));
        original.FinishCommand(new PlayerId(1));
        var cashBefore = original.Players[0].Cash;
        original.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Combat, original.Coordinator.ExecutionPhase);

        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, original);
        save.Position = 0;
        var restored = NativeSaveSerializer.Load(save, original.Definitions);

        Assert.Equal(cashBefore, restored.Players[0].Cash);
        Assert.Contains(restored.Events, gameEvent =>
            gameEvent.ExecutionPhase == ExecutionPhase.Chaos);
        for (var index = 0; index < 3; index++)
        {
            original.FinishExecutionPhase();
            restored.FinishExecutionPhase();
        }

        Assert.True(original.Players[0].Cash > cashBefore);
        Assert.Equal(original.Players[0].Cash, restored.Players[0].Cash);
        Assert.Equal(original.Players[0].Statistics.CashEarned,
            restored.Players[0].Statistics.CashEarned);
        Assert.Equal(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(restored));
    }

    [Fact]
    public void UncontrolledSectorPaysHalfOfSuccessesRoundedDown()
    {
        var match = CreateMatch(tolerance: 40);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var successes = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.Successes;
        Assert.Equal(successes / 2, match.Players[0].Cash - cashBefore);
        Assert.Equal(successes / 2, match.Players[0].Statistics.CashEarned);
    }

    [Fact]
    public void ChaosUsesGeneratedSectorIncomeRatherThanOwnerCash()
    {
        var match = CreateMatch(tolerance: 40, income: 7);
        var gang = match.FindGang(new GangId(10))!;
        var sector = match.Sectors[0];
        var sectorCash = SectorIncomeResolver.SectorCash(match, sector);
        Assert.NotEqual(sector.Income, sectorCash);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);

        match.FinishExecutionPhase();

        var band = OriginalResolutionRules.Band(match, new PlayerId(0));
        var statistics = EffectiveStatisticsCalculator.ForGang(match, gang);
        var expectedDice = OriginalResolutionRules.ActionPool(
            band, GangAction.Chaos, sector.Income + gang.Force + statistics.Chaos);
        var cashDice = OriginalResolutionRules.ActionPool(
            band, GangAction.Chaos, sectorCash + gang.Force + statistics.Chaos);
        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.NotEqual(cashDice, expectedDice);
        Assert.Equal(expectedDice, resolution.AttackValue);
        Assert.Equal(expectedDice, resolution.Rolls.Count);
    }

    [Fact]
    public void AllPlayersContributeBeforeCrackdownSuppressesSectorPayouts()
    {
        var match = CreateMatch(secondPlayerSector: 0, tolerance: 0);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: true);
        var cashBefore = match.Players.Select(player => player.Cash).ToArray();

        match.FinishExecutionPhase();

        Assert.Equal(2, match.LastPhaseResolutions.Count);
        var totalSuccesses = match.LastPhaseResolutions.Sum(result => result.Event!.Resolution!.Successes);
        Assert.True(totalSuccesses > 0);
        Assert.All(match.LastPhaseResolutions,
            result => Assert.Equal(totalSuccesses, result.Event!.Resolution!.ResultValue));
        Assert.True(match.Sectors[0].CrackdownActive);
        Assert.Equal(cashBefore, match.Players.Select(player => player.Cash));
        Assert.All(match.Players, player => Assert.Contains(
            match.NotificationsFor(player.Id),
            notification => notification.Kind == GameNotificationKind.Crackdown && notification.SectorId == 0));
    }

    [Fact]
    public void ExistingCrackdownSuppressesIncomeWhileChaosStillRolls()
    {
        var match = CreateMatch(tolerance: 40, crackdownActive: true);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var successes = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.Successes;
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(successes,
            Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.ResultValue);
        Assert.DoesNotContain(match.NotificationsFor(new PlayerId(0)),
            notification => notification.Kind == GameNotificationKind.Crackdown);
    }

    [Fact]
    public void InstantPhaseClampsNegativeToleranceBeforeCommandlessChaosCheck()
    {
        var match = CreateMatch(tolerance: -2);
        match.FinishUpkeep();
        Assert.Equal(-1, match.Sectors[0].Tolerance);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        for (var index = 0; index < 3; index++) match.FinishExecutionPhase();

        match.FinishExecutionPhase();

        Assert.Equal(1, match.Sectors[0].Tolerance);
        Assert.False(match.Sectors[0].CrackdownActive);
        Assert.All(match.Players, player => Assert.DoesNotContain(
            match.NotificationsFor(player.Id),
            notification => notification.Kind == GameNotificationKind.Crackdown));
    }

    [Fact]
    public void EquivalentChaosRunsProduceIdenticalEventsAndHash()
    {
        var first = CreateMatch(twoPlayerZeroGangs: true, owner: new PlayerId(0), tolerance: 40);
        var second = CreateMatch(twoPlayerZeroGangs: true, owner: new PlayerId(0), tolerance: 40);
        QueueChaosAndEnterPhase(first, includeSecondPlayer: false);
        QueueChaosAndEnterPhase(second, includeSecondPlayer: false);

        first.FinishExecutionPhase();
        second.FinishExecutionPhase();

        Assert.Equal(
            first.LastPhaseResolutions.Select(result => result.Event!.Resolution!.Rolls),
            second.LastPhaseResolutions.Select(result => result.Event!.Resolution!.Rolls),
            new RollCollectionComparer());
        Assert.Equal(first.LastPhaseResolutions.Select(result => result.Event!.Resolution!.Successes),
            second.LastPhaseResolutions.Select(result => result.Event!.Resolution!.Successes));
        Assert.Equal(first.PhaseHashes[^1].Sha256, second.PhaseHashes[^1].Sha256);
    }

    /// <summary>
    /// A Chaos participant killed between the roll and the payout does not abort the turn.
    /// </summary>
    /// <remarks>
    /// Chaos is rolled at the end of Instant and paid at the later Chaos boundary; Combat runs in
    /// between, and eliminating a gang retires its queue entry. Rebuilding the pass from the live
    /// queue therefore found one fewer command than there were events and threw — offline that
    /// ended the process at the end of the turn, and online it threw on every client at the same
    /// sealed turn, so reconnecting replayed straight back into it and the match was lost.
    /// </remarks>
    [Fact]
    public void AChaosParticipantKilledInCombatStillPaysItsGroup()
    {
        var match = CreateMatch(twoPlayerZeroGangs: true, income: 6);
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        Assert.True(match.Submit(Chaos(0, 11)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase(); // Instant: the Chaos pass is rolled and recorded here.
        var chaosEvents = match.Events.Count(gameEvent =>
            gameEvent.ExecutionPhase == ExecutionPhase.Chaos
            && gameEvent.Action == GangAction.Chaos);
        Assert.Equal(2, chaosEvents);
        var cashBeforeCombat = match.Players[0].Cash;
        // Combat killing the first participant of the group, whose event carries its payout. That
        // is what `EliminateGang` does: Force to zero and the queue entry retired.
        match.Players[0].Gangs[0].Force = 0;
        match.Players[0].Gangs[0].Hidden = false;
        match.Commands.Cancel(new GangId(10));
        match.Players[0].Gangs[0].QueuedCommand = null;

        match.FinishExecutionPhase(); // Combat
        match.FinishExecutionPhase(); // Transaction
        match.FinishExecutionPhase(); // Chaos: the payout, from the recorded events.

        Assert.Equal(ExecutionPhase.Movement, match.Coordinator.ExecutionPhase);
        Assert.Equal(2, match.LastPhaseResolutions.Count);
        // The dead gang's event stays in the pass, so the group's income is still credited once.
        Assert.True(match.Players[0].Cash >= cashBeforeCombat);
    }

    /// <summary>The whole Chaos set dying leaves an empty queue and a pass that still pays.</summary>
    [Fact]
    public void AChaosPassWhoseWholeSetDiedStillResolves()
    {
        var match = CreateMatch(income: 6);
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        match.FinishExecutionPhase(); // Instant
        match.Players[0].Gangs[0].Force = 0;
        match.Commands.Cancel(new GangId(10));
        match.Players[0].Gangs[0].QueuedCommand = null;
        Assert.Empty(match.Commands.ForPhase(ExecutionPhase.Chaos));

        match.FinishExecutionPhase(); // Combat
        match.FinishExecutionPhase(); // Transaction
        match.FinishExecutionPhase(); // Chaos

        Assert.Equal(ExecutionPhase.Movement, match.Coordinator.ExecutionPhase);
        Assert.Single(match.LastPhaseResolutions);
    }

    private static void QueueChaosAndEnterPhase(MatchState match, bool includeSecondPlayer)
    {
        match.FinishUpkeep();
        Assert.True(match.Submit(Chaos(0, 10)).Accepted);
        if (match.FindGang(new GangId(11)) is not null) Assert.True(match.Submit(Chaos(0, 11)).Accepted);
        match.FinishCommand(new PlayerId(0));
        if (includeSecondPlayer) Assert.True(match.Submit(Chaos(1, 20)).Accepted);
        match.FinishCommand(new PlayerId(1));
        for (var index = 0; index < 3; index++) match.FinishExecutionPhase();
        Assert.Equal(ExecutionPhase.Chaos, match.Coordinator.ExecutionPhase);
    }

    private static GameCommand Chaos(int player, int gang) =>
        new(new PlayerId(player), new GangId(gang), GangAction.Chaos, CommandTarget.None);

    private static int SectorIncome(MatchState match, int sectorId) =>
        match.Sectors[sectorId].Income;

    private static void AdvanceCoordinatorTurn(MatchState match)
    {
        var coordinator = match.Coordinator;
        coordinator.FinishUpkeep();
        foreach (var player in match.Players) coordinator.FinishCommand(player.Id);
        while (coordinator.Phase == TurnPhase.Execution) coordinator.FinishExecutionPhase();
        foreach (var player in match.Players) coordinator.FinishHire(player.Id);
        coordinator.FinishPlayerElimination();
    }

    private sealed class RollCollectionComparer : IEqualityComparer<IReadOnlyList<int>>
    {
        public bool Equals(IReadOnlyList<int>? x, IReadOnlyList<int>? y) =>
            x is not null && y is not null && x.SequenceEqual(y);

        public int GetHashCode(IReadOnlyList<int> obj) => 0;
    }

    private static MatchState CreateMatch(
        bool twoPlayerZeroGangs = false,
        int secondPlayerSector = 3,
        PlayerId? owner = null,
        int tolerance = 20,
        bool crackdownActive = false,
        int income = 2)
    {
        var data = BundledOriginalData.Load();
        var chaosGang = data.Gangs.OrderByDescending(gang => gang.Stats.Chaos).First().Id;
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "ONE", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        var playerZeroGangs = new List<MatchGangState>
        {
            new(new GangId(10), new PlayerId(0), chaosGang, 0, 10)
        };
        if (twoPlayerZeroGangs)
            playerZeroGangs.Add(new MatchGangState(new GangId(11), new PlayerId(0), chaosGang, 0, 8));
        MatchPlayerState[] players =
        [
            new(setups[0], 500, playerZeroGangs),
            new(setups[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), chaosGang, secondPlayerSector, 9)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], id == 0 ? owner : null, id == 0 ? tolerance : 20,
                crackdownActive: id == 0 && crackdownActive,
                income: id == 0 ? income : 2))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
