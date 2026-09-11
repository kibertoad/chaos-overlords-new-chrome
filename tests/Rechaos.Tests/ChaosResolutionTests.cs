using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

public sealed class ChaosResolutionTests
{
    [Fact]
    public void UpkeepClearsPreviousTurnsChaosBeforeCommands()
    {
        var match = CreateMatch(initialChaos: 17);

        match.FinishUpkeep();

        Assert.Equal(0, match.Sectors[0].Chaos);
    }

    [Fact]
    public void CrackdownDurationIsThreeToFivePoliceCombatPhases()
    {
        var match = CreateMatch(tolerance: 0);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);

        match.FinishExecutionPhase();

        var duration = match.Sectors[0].CrackdownTurnsRemaining;
        Assert.InRange(duration, ManualRules.MinimumCrackdownTurns, ManualRules.MaximumCrackdownTurns);
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
    public void AnotherCrackdownExtendsExistingPolicePresence()
    {
        var match = CreateMatch(tolerance: 0, crackdownActive: true);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);
        var remainingBeforeChaos = match.Sectors[0].CrackdownTurnsRemaining;

        match.FinishExecutionPhase();

        Assert.InRange(
            match.Sectors[0].CrackdownTurnsRemaining - remainingBeforeChaos,
            ManualRules.MinimumCrackdownTurns,
            ManualRules.MaximumCrackdownTurns);
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
        Assert.Equal([3, 5], sector.CrackdownHistory);
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
        Assert.Equal([7], sector.CrackdownHistory);
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
        Assert.Equal(first.Successes, match.Sectors[0].Chaos);
        Assert.Equal(expectedDice * 3, match.Random.ConsumptionCount);
        Assert.All(match.LastPhaseResolutions,
            result => Assert.Equal(GameNotificationKind.Chaos,
                match.NotificationsFor(result.Command.Player)
                    .Single(notification => notification.RelatedEventSequence == result.Event!.Sequence).Kind));
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
    public void ChaosUsesGeneratedSectorIncomeInsteadOfSiteCashBenefits()
    {
        var match = CreateMatch(tolerance: 40, income: 7);
        var gang = match.FindGang(new GangId(10))!;
        var sector = match.Sectors[0];
        var siteCash = sector.Sites.Sum(site => match.Definitions.Sites.Single(
            definition => definition.Id == site.DefinitionId).Cash);
        Assert.NotEqual(siteCash, sector.Income);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);

        match.FinishExecutionPhase();

        var band = OriginalResolutionRules.Band(match, new PlayerId(0));
        var statistics = EffectiveStatisticsCalculator.ForGang(match, gang);
        var expectedDice = OriginalResolutionRules.ActionPool(
            band, GangAction.Chaos, sector.Income + gang.Force + statistics.Chaos);
        var siteDerivedDice = OriginalResolutionRules.ActionPool(
            band, GangAction.Chaos, siteCash + gang.Force + statistics.Chaos);
        var resolution = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!;
        Assert.NotEqual(siteDerivedDice, expectedDice);
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
        Assert.Equal(totalSuccesses, match.Sectors[0].Chaos);
        Assert.True(match.Sectors[0].CrackdownActive);
        Assert.Equal(cashBefore, match.Players.Select(player => player.Cash));
        Assert.All(match.Players, player => Assert.Contains(
            match.NotificationsFor(player.Id),
            notification => notification.Kind == GameNotificationKind.Crackdown && notification.SectorId == 0));
    }

    [Fact]
    public void ExistingCrackdownSuppressesIncomeWhileChaosStillAccumulates()
    {
        var match = CreateMatch(tolerance: 40, crackdownActive: true);
        QueueChaosAndEnterPhase(match, includeSecondPlayer: false);
        var cashBefore = match.Players[0].Cash;

        match.FinishExecutionPhase();

        var successes = Assert.Single(match.LastPhaseResolutions).Event!.Resolution!.Successes;
        Assert.Equal(cashBefore, match.Players[0].Cash);
        Assert.Equal(successes, match.Sectors[0].Chaos);
        Assert.DoesNotContain(match.NotificationsFor(new PlayerId(0)),
            notification => notification.Kind == GameNotificationKind.Crackdown);
    }

    [Fact]
    public void NegativeEffectiveToleranceTriggersCrackdownWithoutChaosCommands()
    {
        var match = CreateMatch(tolerance: -2);
        match.FinishUpkeep();
        Assert.Equal(-1, match.Sectors[0].Tolerance);
        match.FinishCommand(new PlayerId(0));
        match.FinishCommand(new PlayerId(1));
        for (var index = 0; index < 3; index++) match.FinishExecutionPhase();

        match.FinishExecutionPhase();

        Assert.True(match.Sectors[0].CrackdownActive);
        Assert.All(match.Players, player => Assert.Contains(
            match.NotificationsFor(player.Id),
            notification => notification.Kind == GameNotificationKind.Crackdown
                && notification.SectorId == 0
                && notification.RelatedEventSequence is null));
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
        int initialChaos = 0,
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
                chaos: id == 0 ? initialChaos : 0,
                crackdownActive: id == 0 && crackdownActive,
                income: id == 0 ? income : 2))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }
}
