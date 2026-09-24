using Rechaos.Core.GameModel;
using Rechaos.Core.Assets;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed class CombatResultProjectionTests
{
    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(0, 2, false)]
    [InlineData(2, 2, false)]
    public void OnlyTheImmediatelyCompletedTurnIsPresented(
        int eventTurn,
        int currentTurn,
        bool expected)
    {
        Assert.Equal(expected,
            CombatResultProjection.IsFromLastCompletedTurn(eventTurn, currentTurn));
    }

    [Fact]
    public void InvalidTurnValuesAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(-1, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            CombatResultProjection.IsFromLastCompletedTurn(0, 0));
    }

    [Theory]
    [InlineData(false, false, HandoffPresentationStep.City)]
    [InlineData(false, true, HandoffPresentationStep.Events)]
    [InlineData(true, false, HandoffPresentationStep.Combat)]
    [InlineData(true, true, HandoffPresentationStep.Combat)]
    public void HandoffPresentsCombatBeforePrivateTurnReports(
        bool hasCombat,
        bool hasReports,
        HandoffPresentationStep expected)
    {
        Assert.Equal(expected, HandoffPresentationOrder.First(hasCombat, hasReports));
    }

    [Fact]
    public void AnimationProgressIsIndependentForEachHotSeatPlayer()
    {
        var progress = new CombatPresentationProgress();
        var first = new PlayerId(0);
        var second = new PlayerId(1);

        progress.MarkSeen(first, 17);

        Assert.Equal(17, progress.LastSeen(first));
        Assert.Equal(-1, progress.LastSeen(second));
        progress.MarkSeen(second, 9);
        Assert.Equal(9, progress.LastSeen(second));
        Assert.Throws<ArgumentOutOfRangeException>(() => progress.MarkSeen(first, 16));
    }

    [Fact]
    public void LoadResetSuppressesHistoricalEventsForEveryPlayer()
    {
        var progress = new CombatPresentationProgress();
        PlayerId[] players = [new(0), new(1), new(2)];

        progress.ResetTo(players, 42);

        Assert.All(players, player => Assert.Equal(42, progress.LastSeen(player)));
        progress.Clear();
        Assert.All(players, player => Assert.Equal(-1, progress.LastSeen(player)));
    }

    [Fact]
    public void PagesGroupResultsBySectorInBoardOrder()
    {
        var entries = new[]
        {
            Attack(3, 12, 0, 10, 1, 20),
            Attack(1, 4, 0, 11, 2, 30),
            Attack(2, 12, 0, 12, 2, 31)
        };

        var pages = CombatResultProjection.Pages(entries, new PlayerId(0), new HashSet<int>());

        Assert.Equal([4, 12], pages.Select(page => page.SectorId));
        Assert.Equal([1L], pages[0].Results.Select(result => result.Event.Sequence));
        Assert.Equal([2L, 3L], pages[1].Results.Select(result => result.Event.Sequence));
    }

    [Fact]
    public void OccupyingACombatSectorRevealsOtherPlayersResults()
    {
        var entries = new[]
        {
            Attack(1, 7, 1, 20, 2, 30),
            Attack(2, 8, 1, 21, 2, 31)
        };

        var pages = CombatResultProjection.Pages(
            entries, new PlayerId(0), new HashSet<int> { 7 });

        var page = Assert.Single(pages);
        Assert.Equal(7, page.SectorId);
        Assert.Single(page.Results);
    }

    [Fact]
    public void ForceGridPacksOneEntryPerParticipatingGang()
    {
        var page = new CombatResultPage(12,
        [
            Attack(1, 12, 0, 10, 1, 20),
            Attack(2, 12, 0, 10, 2, 30),
            Attack(3, 12, 0, 11, 1, 21)
        ]);

        var forces = page.ForcesFor(new PlayerId(0));

        Assert.Equal([new GangId(10), new GangId(11)], forces.Select(force => force.Gang));
        Assert.Equal([1L, 3L], forces.Select(force => force.Event.Sequence));
    }

    [Fact]
    public void PoliceResultBelongsToItsTargetPlayer()
    {
        var gameEvent = Event(5, GameEventKind.PoliceAttackResolved, new PlayerId(0),
            new GangId(10), GangAction.None, CommandTarget.Sector(9));
        var entry = new CombatResultEntry(
            gameEvent, 9, new PlayerId(0), new GangId(10), null, null, true);

        var page = Assert.Single(CombatResultProjection.Pages(
            [entry], new PlayerId(0), new HashSet<int>()));

        Assert.Equal(new GangId(10), Assert.Single(page.ForcesFor(new PlayerId(0))).Gang);
        Assert.Empty(page.ForcesFor(new PlayerId(1)));
    }

    [Fact]
    public void MatchProjectionIncludesObservedSectorAndExcludesUnobservedCombat()
    {
        var match = CreateObservedCombatMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Attack,
            CommandTarget.Gang(new GangId(30)))).Accepted);
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(21), GangAction.Attack,
            CommandTarget.Gang(new GangId(31)))).Accepted);
        match.FinishCommand(new PlayerId(1));
        match.FinishCommand(new PlayerId(2));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();

        var observerPages = CombatResultProjection.Pages(match, new PlayerId(0));
        var attackerPages = CombatResultProjection.Pages(match, new PlayerId(1));

        Assert.Equal([0], observerPages.Select(page => page.SectorId));
        Assert.Equal([0, 5], attackerPages.Select(page => page.SectorId));
        Assert.Equal(2, match.Coordinator.Turn);
    }

    [Fact]
    public void CombatAgainstAGangWhoseSlotWasRehiredIsStillPresented()
    {
        var match = CreateObservedCombatMatch();
        var combatants = new CombatantHistory();
        var defender = new PlayerId(2);
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Attack,
            CommandTarget.Gang(new GangId(30)))).Accepted);
        // What the interface sees on a frame while the turn is still being planned.
        combatants.Observe(match);
        match.FinishCommand(new PlayerId(1));
        match.FinishCommand(defender);
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        RetireAsHireDoes(match, new GangId(30));
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        var attack = Assert.Single(match.Events, gameEvent =>
            gameEvent is { Kind: GameEventKind.CommandResolved, Action: GangAction.Attack });

        Assert.Null(match.FindGang(new GangId(30)));
        Assert.Empty(CombatResultProjection.Pages(match, defender));
        Assert.Empty(CombatAnimationRouting.ForEvent(match, attack));

        var page = Assert.Single(CombatResultProjection.Pages(match, defender, combatants));
        Assert.Equal(0, page.SectorId);
        var force = Assert.Single(page.ForcesFor(defender));
        Assert.Equal(new GangId(30), force.Gang);
        Assert.NotEmpty(CombatAnimationRouting.ForEvent(match, attack, combatants));
        var retired = combatants.Find(match, new GangId(30))!;
        Assert.Equal(defender, retired.Owner);
        Assert.Equal(0, retired.Force);
        Assert.Same(retired, combatants.Find(match, new GangId(30)));
    }

    [Fact]
    public void CombatantHistoryPrefersTheLiveGangAndForgetsAClearedMatch()
    {
        var match = CreateObservedCombatMatch();
        var combatants = new CombatantHistory();
        combatants.Observe(match);
        var live = match.FindGang(new GangId(30))!;
        RetireAsHireDoes(match, new GangId(31));

        Assert.Same(live, combatants.Find(match, new GangId(30)));
        Assert.NotNull(combatants.Find(match, new GangId(31)));
        Assert.Null(combatants.Find(match, new GangId(99)));
        combatants.Clear();
        Assert.Null(combatants.Find(match, new GangId(31)));
    }

    [Fact]
    public void CombatantHistoryOutlivesOnlyAResumeOfTheSameOnlineMatch()
    {
        var match = CreateObservedCombatMatch();
        var combatants = new CombatantHistory();
        combatants.ResetTo("match-a");
        combatants.Observe(match);
        RetireAsHireDoes(match, new GangId(31));

        combatants.ResetTo("match-a");
        Assert.NotNull(combatants.Find(match, new GangId(31)));
        combatants.ResetTo("match-b");
        Assert.Null(combatants.Find(match, new GangId(31)));

        combatants.Observe(CreateObservedCombatMatch());
        combatants.ResetTo(null);
        Assert.Null(combatants.Find(match, new GangId(31)));
    }

    [Fact]
    public void UndetectedPoliceAreNotCombatResults()
    {
        var match = CreateCrackdownMatch();
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(11), GangAction.Move, CommandTarget.Sector(1))).Accepted);
        match.FinishCommand(new PlayerId(0));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        var police = match.Events
            .Where(gameEvent => gameEvent.Kind == GameEventKind.PoliceAttackResolved)
            .ToArray();
        match.FinishHire(new PlayerId(0));
        match.FinishPlayerElimination();

        Assert.Contains(police, gameEvent => !gameEvent.PoliceAttack!.Detected);
        Assert.Contains(police, gameEvent => gameEvent.PoliceAttack!.Detected);
        var pages = CombatResultProjection.Pages(match, new PlayerId(0));
        var page = Assert.Single(pages);
        Assert.Equal(0, page.SectorId);
        Assert.Equal(
            police.Where(gameEvent => gameEvent.PoliceAttack!.Detected)
                .Select(gameEvent => gameEvent.Sequence),
            page.Results.Select(result => result.Event.Sequence));
    }

    [Fact]
    public void CrackdownThatDetectsNobodyHasNoCombatResults()
    {
        var match = CreateCrackdownMatch(undetectableOnly: true);
        match.FinishUpkeep();
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Move, CommandTarget.Sector(1))).Accepted);
        match.FinishCommand(new PlayerId(0));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        match.FinishHire(new PlayerId(0));
        match.FinishPlayerElimination();

        Assert.Contains(match.Events, gameEvent => gameEvent.PoliceAttack is { Detected: false });
        Assert.Empty(CombatResultProjection.Pages(match, new PlayerId(0)));
    }

    /// <summary>
    /// What resolving a hire does to the slot of a gang with no force left: the dead gang is
    /// replaced and its id stops resolving.
    /// </summary>
    private static void RetireAsHireDoes(MatchState match, GangId gangId)
    {
        var gang = match.FindGang(gangId)!;
        gang.Force = 0;
        var owner = match.FindPlayer(gang.Owner)!;
        var slot = owner.Gangs.ToList().IndexOf(gang);
        owner.ReplaceGang(slot, new MatchGangState(
            match.NextGangId(), gang.Owner, definitionId: 2, gang.SectorId, force: 7));
    }

    private static CombatResultEntry Attack(
        long sequence, int sector, int attacker, int attackerGang, int defender, int defenderGang)
    {
        var gameEvent = Event(sequence, GameEventKind.CommandResolved, new PlayerId(attacker),
            new GangId(attackerGang), GangAction.Attack, CommandTarget.Gang(new GangId(defenderGang)));
        return new CombatResultEntry(
            gameEvent, sector, new PlayerId(attacker), new GangId(attackerGang),
            new PlayerId(defender), new GangId(defenderGang), false);
    }

    private static GameEvent Event(
        long sequence,
        GameEventKind kind,
        PlayerId player,
        GangId gang,
        GangAction action,
        CommandTarget target) => new(
            sequence, 1, TurnPhase.Execution, ExecutionPhase.Combat,
            kind, player, gang, action, target,
            Resolution: action == GangAction.Attack
                ? new CommandResolutionDetails(CommandResolutionCode.Resolved, [], 0)
                : null,
            PoliceAttack: kind == GameEventKind.PoliceAttackResolved
                ? new PoliceAttackResolutionDetails(9, 100, 1, true, 20, 0, [4], 1, 1, 10, 9)
                : null);

    private static MatchState CreateObservedCombatMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "OBSERVER", PlayerController.Human),
            new(new PlayerId(1), "ATTACKER", PlayerController.Computer),
            new(new PlayerId(2), "DEFENDER", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 500, [Gang(10, 0, 0)]),
            new(setups[1], 500, [Gang(20, 1, 0), Gang(21, 1, 5)]),
            new(setups[2], 500, [Gang(30, 2, 0), Gang(31, 2, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ]))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups),
            players, sectors);
    }

    /// <summary>
    /// One player in crackdown sector 0. Ebon Order with an Inviso-Cloak beside a Stealth site the
    /// player influences reaches Stealth 25 and is never detected; the
    /// definition with the lowest Stealth always is.
    /// </summary>
    private static MatchState CreateCrackdownMatch(bool undetectableOnly = false)
    {
        const short ebonOrder = 84;
        const short invisoCloak = 36;
        var data = BundledOriginalData.Load();
        var setup = new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human);
        List<MatchGangState> gangs =
        [
            new(new GangId(10), new PlayerId(0), ebonOrder, 0, 7, miscellaneousItemId: invisoCloak)
        ];
        if (!undetectableOnly)
            gangs.Add(new MatchGangState(new GangId(11), new PlayerId(0),
                data.Gangs.MinBy(gang => gang.Stats.Stealth)!.Id, 0, 10));
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, id == 0 ? (short)17 : (short)0, id == 0 ? 0 : 7,
                    id == 0 ? new PlayerId(0) : null),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? new PlayerId(0) : null, crackdownActive: id == 0))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, [setup]),
            [new MatchPlayerState(setup, 500, gangs)], sectors);
    }

    private static MatchGangState Gang(int id, int owner, int sector) =>
        new(new GangId(id), new PlayerId(owner), definitionId: 1, sector, force: 10);
}
