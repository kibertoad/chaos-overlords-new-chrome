using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class HireAndEliminationTests
{
    [Fact]
    public void DebtStillAllowsAZeroInitialCostGang()
    {
        var free = new GangDefinition("FREE", 90, "", 0, 0, 1,
            new Statistics(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));
        var paid = free with { Name = "PAID", Id = 91, Force = 1 };

        Assert.True(HireRules.CanAffordInitialCost(-5, free));
        Assert.False(HireRules.CanAffordInitialCost(-5, paid));
        Assert.True(HireRules.CanAffordInitialCost(1, paid));
    }

    [Fact]
    public void HireSelectionQueuesWithoutPayingUntilDeferredPlacement()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;

        var result = match.QueueHire(new PlayerId(0), 2, 0);

        Assert.True(result.Accepted);
        Assert.Equal(string.Empty, result.Validation.Message);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(0, match.Players[0].Statistics.CashSpent);
        Assert.Contains((short)2, match.Players[0].HirePool);
        Assert.Equal(new PendingHireState(2, 0, 1, InitialCostPaid: false),
            Assert.Single(match.Players[0].PendingHires));
        Assert.Equal(HireOfferSlotState.Available(2), match.Players[0].HireOfferSlots[1]);
        Assert.Single(match.Players[0].Gangs);
        Assert.Equal(GameEventKind.HireQueued, result.Event!.Kind);
        Assert.Equal(2, result.Event.Hire!.GangDefinitionId);
    }

    [Fact]
    public void FinishingHireLeavesSameSlotVacantUntilNextPlanningEntry()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;
        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);

        match.FinishHire(new PlayerId(0));

        var resolution = Assert.Single(match.LastHireResolutions);
        var recruit = match.FindGang(resolution.Gang)!;
        Assert.Equal((short)2, recruit.DefinitionId);
        Assert.Equal(0, recruit.SectorId);
        Assert.InRange(recruit.Force, ManualRules.MinimumHiredGangForce, ManualRules.MaximumHiredGangForce);
        Assert.Equal(recruit.Force, resolution.InitialForce);
        Assert.Equal(recruit.Force, resolution.Event.Hire!.InitialForce);
        Assert.True(recruit.HiredThisTurn);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal(2, match.Players[0].HirePool.Count);
        Assert.Equal(HireOfferSlotState.Vacant(2), match.Players[0].HireOfferSlots[1]);
        Assert.Equal(3, match.Random.ConsumptionCount);
        Assert.Null(resolution.ReplacementOffer);
        Assert.Equal(GameEventKind.HireResolved, resolution.Event.Kind);
        Assert.Equal(cashBefore - 1, match.Players[0].Cash);
        Assert.Equal(1, match.Players[0].Statistics.CashSpent);
        Assert.Single(match.NotificationsFor(new PlayerId(0)), item => item.Kind == GameNotificationKind.Hire);

        FinishTurn(match);
        match.FinishUpkeep();
        Assert.False(recruit.HiredThisTurn);
        match.PrepareHireOffers(new PlayerId(0));
        var replacement = match.Players[0].HireOfferSlots[1].GangDefinitionId;
        Assert.Equal(MatchLimits.HireOffersPerPlayer, match.Players[0].HirePool.Count);
        Assert.NotNull(replacement);
        Assert.NotEqual((short)2, replacement);
    }

    [Fact]
    public void HireRulesReturnTheirOwnErrorMessagesWithoutMutation()
    {
        var match = CreateMatch();

        var wrongPhase = match.QueueHire(new PlayerId(0), 2, 0);

        Assert.Equal(HireValidationCode.InvalidPhase, wrongPhase.Validation.Code);
        Assert.Equal("Gangs may only be hired during the player's planning turn.", wrongPhase.Validation.Message);
        Assert.Equal(10, match.Players[0].Cash);
        Assert.Empty(match.Events);

        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;
        var eventCountBefore = match.Events.Count;
        var uncontrolled = match.QueueHire(new PlayerId(0), 2, 1);
        Assert.Equal(HireValidationCode.SectorNotControlled, uncontrolled.Validation.Code);
        Assert.Equal("A recruit must be placed in a controlled sector or with one of the player's gangs.",
            uncontrolled.Validation.Message);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal(eventCountBefore, match.Events.Count);
    }

    [Fact]
    public void HireSelectionAllowsAVisibleOwnGangWithoutSectorControl()
    {
        var match = CreateMatch(gangs:
        [
            new MatchGangState(new GangId(10), new PlayerId(0), 1, 1, 5)
        ]);
        AdvanceToHire(match);

        var result = match.QueueHire(new PlayerId(0), 2, 1);

        Assert.True(result.Accepted);
        Assert.Equal(1, Assert.Single(match.Players[0].PendingHires).TargetSectorId);

        var legacy = CreateMatch(gangs:
        [
            new MatchGangState(new GangId(10), new PlayerId(0), 1, 1, 5)
        ]);
        AdvanceToHire(legacy);
        Assert.Equal(HireValidationCode.SectorNotControlled,
            legacy.QueueHireLegacyImmediatePayment(new PlayerId(0), 2, 1).Validation.Code);
    }

    [Fact]
    public void HireCanBeChosenDuringCommandPlanningAndPlacedLater()
    {
        var match = CreateMatch();
        match.FinishUpkeep();
        var cashBefore = match.Players[0].Cash;

        var result = match.QueueHire(new PlayerId(0), 2, 0);

        Assert.True(result.Accepted);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(0, match.Players[0].Statistics.CashSpent);
        Assert.Single(match.Players[0].PendingHires);
        Assert.Single(match.Players[0].Gangs);

        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));

        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal(2, match.Players[0].Gangs.Count);
        Assert.Equal(cashBefore - 1, match.Players[0].Cash);
        Assert.Equal(1, match.Players[0].Statistics.CashSpent);
    }

    [Fact]
    public void SnubLeavesSameSlotVacantUntilNextPlanningEntry()
    {
        var match = CreateMatch();
        Assert.Equal(HireValidationCode.InvalidPhase,
            match.SnubHireOffer(new PlayerId(0), 2).Validation.Code);
        AdvanceToHire(match);

        var snub = match.SnubHireOffer(new PlayerId(0), 2);

        Assert.True(snub.Accepted);
        Assert.Equal((short)2, match.Players[0].SnubbedHireOffer);
        Assert.Contains((short)2, match.Players[0].HirePool);
        Assert.Equal(1, match.Players[0].SnubbedHireOfferSlot);
        Assert.Equal(GameEventKind.HireOfferSnubbed, snub.Event!.Kind);
        var replacementSnub = match.SnubHireOffer(new PlayerId(0), 1);
        Assert.True(replacementSnub.Accepted);
        Assert.Equal((short)1, match.Players[0].SnubbedHireOffer);
        Assert.Equal(0, match.Players[0].SnubbedHireOfferSlot);

        match.FinishHire(new PlayerId(0));

        Assert.Null(match.Players[0].SnubbedHireOffer);
        Assert.Equal(HireOfferSlotState.Vacant(1), match.Players[0].HireOfferSlots[0]);
        Assert.Equal(0, match.Random.ConsumptionCount);
        FinishTurn(match);
        match.FinishUpkeep();
        match.PrepareHireOffers(new PlayerId(0));
        Assert.Equal(MatchLimits.HireOffersPerPlayer, match.Players[0].HirePool.Count);
        Assert.NotEqual((short)1, match.Players[0].HireOfferSlots[0].GangDefinitionId);
        var refill = Assert.Single(match.Events, item => item.Kind == GameEventKind.HireOfferRefilled);
        Assert.Equal((short)1, refill.HireOffer!.RemovedOffer);
        Assert.Equal(match.Players[0].HireOfferSlots[0].GangDefinitionId,
            refill.HireOffer.AddedOffer);
        Assert.Equal(3, match.Random.ConsumptionCount);
    }

    [Fact]
    public void HireAndSnubSelectionsReplaceOrToggleTheSingleAction()
    {
        var match = CreateMatch();
        AdvanceToHire(match);

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        Assert.True(match.SnubHireOffer(new PlayerId(0), 1).Accepted);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Equal((short)1, match.Players[0].SnubbedHireOffer);

        Assert.True(match.SnubHireOffer(new PlayerId(0), 1).Accepted);
        Assert.Null(match.Players[0].SnubbedHireOffer);

        Assert.True(match.SnubHireOffer(new PlayerId(0), 2).Accepted);
        Assert.True(match.QueueHire(new PlayerId(0), 3, 0).Accepted);
        Assert.Null(match.Players[0].SnubbedHireOffer);
        var pending = Assert.Single(match.Players[0].PendingHires);
        Assert.Equal((short)3, pending.GangDefinitionId);
        Assert.Equal(2, pending.OfferSlot);
    }

    [Fact]
    public void RejectingSelectedHireCancelsInsteadOfSnubbingAndQueueRetargets()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var cashBefore = match.Players[0].Cash;

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        var retargeted = Assert.Single(match.Players[0].PendingHires);
        Assert.Equal(new PendingHireState(2, 0, 1, InitialCostPaid: false), retargeted);

        var canceled = match.SnubHireOffer(new PlayerId(0), 2);

        Assert.True(canceled.Accepted);
        Assert.Null(canceled.GangDefinitionId);
        Assert.Null(canceled.Event);
        Assert.Empty(match.Players[0].PendingHires);
        Assert.Null(match.Players[0].SnubbedHireOffer);
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(0, match.Players[0].Statistics.CashSpent);
    }

    [Fact]
    public void InvalidReplacementPreservesTheExistingActionAndAccounting()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        var pendingBefore = Assert.Single(match.Players[0].PendingHires);
        var cashBefore = match.Players[0].Cash;
        var spentBefore = match.Players[0].Statistics.CashSpent;
        var eventCountBefore = match.Events.Count;

        var invalid = match.QueueHire(new PlayerId(0), 3, 1);

        Assert.Equal(HireValidationCode.SectorNotControlled, invalid.Validation.Code);
        Assert.Equal(pendingBefore, Assert.Single(match.Players[0].PendingHires));
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(spentBefore, match.Players[0].Statistics.CashSpent);
        Assert.Equal(eventCountBefore, match.Events.Count);
    }

    [Fact]
    public void UnaffordableHireFailsAtResolutionWithoutRngPaymentOrTombstone()
    {
        var match = CreateMatch(initialCash: -1);
        AdvanceToHire(match);
        var player = match.Players[0];
        var cashBefore = player.Cash;
        var randomBefore = match.Random.ConsumptionCount;

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        match.FinishHire(new PlayerId(0));

        Assert.Empty(match.LastHireResolutions);
        Assert.Empty(player.PendingHires);
        Assert.Equal(cashBefore, player.Cash);
        Assert.Equal(0, player.Statistics.CashSpent);
        Assert.Equal(randomBefore, match.Random.ConsumptionCount);
        Assert.Equal(HireOfferSlotState.Available(2), player.HireOfferSlots[1]);
        Assert.Single(player.Gangs);
    }

    [Fact]
    public void SectorCapacityFailureClearsActionWithoutRngPaymentOrTombstone()
    {
        var match = CreateMatch();
        foreach (var index in Enumerable.Range(0, MatchLimits.FriendlyGangsPerSector - 1))
            match.Players[1].AddGang(new MatchGangState(
                new GangId(20 + index), new PlayerId(1), 4, 0, 5));
        AdvanceToHire(match);
        var player = match.Players[0];
        var cashBefore = player.Cash;
        var randomBefore = match.Random.ConsumptionCount;

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        match.FinishHire(new PlayerId(0));

        Assert.Empty(match.LastHireResolutions);
        Assert.Empty(player.PendingHires);
        Assert.Equal(cashBefore, player.Cash);
        Assert.Equal(0, player.Statistics.CashSpent);
        Assert.Equal(randomBefore, match.Random.ConsumptionCount);
        Assert.Equal(HireOfferSlotState.Available(2), player.HireOfferSlots[1]);
        Assert.Single(player.Gangs);
        Assert.Equal(MatchLimits.FriendlyGangsPerSector,
            match.Players.SelectMany(candidate => candidate.Gangs)
                .Count(gang => gang.IsActive && gang.SectorId == 0));
    }

    [Fact]
    public void GlobalGangCapacityFailureConsumesForceRollButDoesNotPayOrTombstone()
    {
        var gangs = Enumerable.Range(0, MatchLimits.GangsPerPlayer)
            .Select(index => new MatchGangState(
                new GangId(10 + index), new PlayerId(0), 1,
                1 + index / MatchLimits.FriendlyGangsPerSector, 5))
            .ToArray();
        var match = CreateMatch(gangs: gangs);
        AdvanceToHire(match);
        var player = match.Players[0];
        var cashBefore = player.Cash;
        var randomBefore = match.Random.ConsumptionCount;

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        match.FinishHire(new PlayerId(0));

        Assert.Empty(match.LastHireResolutions);
        Assert.Empty(player.PendingHires);
        Assert.Equal(cashBefore, player.Cash);
        Assert.Equal(0, player.Statistics.CashSpent);
        Assert.Equal(randomBefore + 3, match.Random.ConsumptionCount);
        Assert.Equal(HireOfferSlotState.Available(2), player.HireOfferSlots[1]);
        Assert.Equal(MatchLimits.GangsPerPlayer, player.Gangs.Count);
    }

    [Fact]
    public void SmgMilkNameGivesEveryHireMaximumForceWithoutForceRng()
    {
        var match = CreateMatch(playerName: "SMGMILK");
        AdvanceToHire(match);
        var randomBefore = match.Random.ConsumptionCount;

        Assert.True(match.QueueHire(new PlayerId(0), 2, 0).Accepted);
        match.FinishHire(new PlayerId(0));

        var result = Assert.Single(match.LastHireResolutions);
        Assert.Equal(ManualRules.MaximumForce, result.InitialForce);
        Assert.Equal(randomBefore, match.Random.ConsumptionCount);
    }

    [Fact]
    public void SmgMilkNameMatchIsExactAndSuppressesFailedCapacityForceRng()
    {
        var ordinary = CreateMatch(playerName: "smgmilk");
        AdvanceToHire(ordinary);
        var ordinaryRandom = ordinary.Random.ConsumptionCount;
        Assert.True(ordinary.QueueHire(new PlayerId(0), 2, 0).Accepted);
        ordinary.FinishHire(new PlayerId(0));
        Assert.Equal(ordinaryRandom + 3, ordinary.Random.ConsumptionCount);

        var gangs = Enumerable.Range(0, MatchLimits.GangsPerPlayer)
            .Select(index => new MatchGangState(
                new GangId(10 + index), new PlayerId(0), 1,
                1 + index / MatchLimits.FriendlyGangsPerSector, 5))
            .ToArray();
        var full = CreateMatch(gangs: gangs, playerName: "SMGMILK");
        AdvanceToHire(full);
        var fullRandom = full.Random.ConsumptionCount;
        Assert.True(full.QueueHire(new PlayerId(0), 2, 0).Accepted);
        full.FinishHire(new PlayerId(0));
        Assert.Empty(full.LastHireResolutions);
        Assert.Equal(fullRandom, full.Random.ConsumptionCount);
    }

    [Fact]
    public void ReplacingLegacyPrepaidHireRefundsItAndQueuesDeferredAction()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var player = match.Players[0];
        var cashBefore = player.Cash;
        player.Cash -= 1;
        player.Statistics.CashSpent += 1;
        player.AddPendingHire(new PendingHireState(2, 0, 1, InitialCostPaid: true));

        var replacement = match.QueueHire(new PlayerId(0), 3, 0);

        Assert.True(replacement.Accepted);
        Assert.Equal(cashBefore, player.Cash);
        Assert.Equal(0, player.Statistics.CashSpent);
        Assert.Equal(new PendingHireState(3, 0, 2, InitialCostPaid: false),
            Assert.Single(player.PendingHires));
    }

    [Fact]
    public void LegacyPrepaidHireIsNotChargedTwiceAtResolution()
    {
        var match = CreateMatch();
        AdvanceToHire(match);
        var player = match.Players[0];
        var cashBefore = player.Cash;
        player.Cash -= 1;
        player.Statistics.CashSpent += 1;
        player.AddPendingHire(new PendingHireState(2, 0, 1, InitialCostPaid: true));

        match.FinishHire(new PlayerId(0));

        Assert.Single(match.LastHireResolutions);
        Assert.Equal(cashBefore - 1, player.Cash);
        Assert.Equal(1, player.Statistics.CashSpent);
        Assert.Equal(HireOfferSlotState.Vacant(2), player.HireOfferSlots[1]);
    }

    [Fact]
    public void MultipleInjectedVacanciesRefillInOriginalSlotAndRngOrder()
    {
        var match = CreateMatch();
        var player = match.Players[0];
        player.SetHireOfferSlot(0, HireOfferSlotState.Vacant(1));
        player.SetHireOfferSlot(1, HireOfferSlotState.Vacant(2));
        player.SetHireOfferSlot(2, HireOfferSlotState.Available(3));
        match.FinishUpkeep();
        var expectedRandom = new DeterministicRandom(
            match.Random.State, match.Random.ConsumptionCount);
        var first = DrawExpected(expectedRandom, [3], excluded: 1);
        var second = DrawExpected(expectedRandom, [first, 3], excluded: 2);

        match.PrepareHireOffers(new PlayerId(0));

        Assert.Equal(first, player.HireOfferSlots[0].GangDefinitionId);
        Assert.Equal(second, player.HireOfferSlots[1].GangDefinitionId);
        Assert.Equal((short)3, player.HireOfferSlots[2].GangDefinitionId);
        Assert.Equal(expectedRandom.State, match.Random.State);
        Assert.Equal(expectedRandom.ConsumptionCount, match.Random.ConsumptionCount);
    }

    [Fact]
    public void EndOfTurnEliminatesPlayerWithNoSectorOrActiveGangAndClearsInfluence()
    {
        var match = CreateMatch(influencedBySecondPlayer: true);
        AdvanceToHire(match);
        match.FinishHire(new PlayerId(0));
        match.FinishHire(new PlayerId(1));

        match.FinishPlayerElimination();

        Assert.Equal(PlayerStatus.Active, match.Players[0].Status);
        Assert.Equal(PlayerStatus.Eliminated, match.Players[1].Status);
        Assert.Null(match.Sectors[1].Sites[0].InfluencedBy);
        Assert.Equal(match.Definitions.Sites[0].Resistance, match.Sectors[1].Sites[0].Resistance);
        var gameEvent = Assert.Single(match.Events, item => item.Kind == GameEventKind.PlayerEliminated);
        Assert.Equal(new PlayerId(1), gameEvent.Elimination!.EliminatedPlayer);
        Assert.Equal(1, gameEvent.Elimination.RemainingPlayers);
        Assert.Contains(match.NotificationsFor(new PlayerId(0)), item => item.Kind == GameNotificationKind.Elimination);
        Assert.Contains(match.NotificationsFor(new PlayerId(1)), item => item.Kind == GameNotificationKind.Elimination);
        Assert.Equal(2, match.Coordinator.Turn);
        Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
    }

    private static MatchState CreateMatch(
        bool influencedBySecondPlayer = false,
        int initialCash = 10,
        IReadOnlyList<MatchGangState>? gangs = null,
        string playerName = "ONE")
    {
        var definitions = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), playerName, PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var setup = new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups);
        MatchPlayerState[] players =
        [
            new(setups[0], initialCash,
                gangs ?? [new MatchGangState(new GangId(10), new PlayerId(0), 1, 0, 5)],
                hirePool: [1, 2, 3],
                usesMaximumHireForce: OriginalHireCheatRules.DetectMaximumHireForce(playerName)),
            new(setups[1], 10, hirePool: [4, 5, 6])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, id == 1 && influencedBySecondPlayer ? 0 : 7,
                    id == 1 && influencedBySecondPlayer ? new PlayerId(1) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : null))
            .ToArray();
        return new MatchState(definitions, setup, players, sectors);
    }

    private static void AdvanceToHire(MatchState match)
    {
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        Assert.Equal(TurnPhase.Hire, match.Coordinator.Phase);
    }

    private static void FinishTurn(MatchState match)
    {
        match.FinishHire(new PlayerId(1));
        match.FinishPlayerElimination();
    }

    private static short DrawExpected(
        DeterministicRandom random,
        IReadOnlyCollection<short> visible,
        short excluded)
    {
        short selected;
        do selected = checked((short)random.NextInclusive(89));
        while (visible.Contains(selected) || selected == excluded);
        return selected;
    }
}
