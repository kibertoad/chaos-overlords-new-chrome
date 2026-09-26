using Rechaos.Core.GameModel;
using Rechaos.Core.Assets;
using Rechaos.Core.Persistence;
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
    public void SelectedFightIdentifiesItsActualOpponent()
    {
        var ownFight = Attack(1, 12, 0, 10, 2, 30);
        var otherFight = Attack(2, 12, 1, 20, 3, 40);

        Assert.Equal(new PlayerId(2), ownFight.OpponentFor(new PlayerId(0)));
        Assert.Equal(new PlayerId(0), ownFight.OpponentFor(new PlayerId(2)));
        Assert.Equal(new PlayerId(1), otherFight.OpponentFor(new PlayerId(0)));
    }

    [Fact]
    public void ClickedOpponentSelectsAFightTheyAreIn()
    {
        var viewer = new PlayerId(0);
        var page = new CombatResultPage(12,
        [
            Attack(1, 12, 1, 20, 2, 30),
            Attack(2, 12, 1, 21, 3, 40),
            Attack(3, 12, 0, 10, 3, 41)
        ]);

        // Prefers the fight against the viewer, even when an earlier one also involves them.
        Assert.Equal(3L, page.ResultAgainst(viewer, new PlayerId(3))!.Event.Sequence);
        // Player 2 never fought the viewer, so their first fight is shown. Its first-listed player
        // is player 1, which is why a click passes the opponent it was on instead of deriving it.
        var selected = page.ResultAgainst(viewer, new PlayerId(2))!;
        Assert.Equal(1L, selected.Event.Sequence);
        Assert.Equal(new PlayerId(1), selected.OpponentFor(viewer));
        Assert.Null(page.ResultAgainst(viewer, new PlayerId(4)));
    }

    [Fact]
    public void RewindStopsAtTheLastEventBeforeTheTurn()
    {
        GameEvent At(long sequence, int turn) => Event(sequence, GameEventKind.CommandResolved,
            new PlayerId(0), new GangId(1), GangAction.Attack, CommandTarget.Gang(new GangId(2)))
            with { Turn = turn };
        // Found by turn, not by comparing sequences: the sealed turn reuses numbers that local
        // planning had already given its own events on the speculative copy.
        GameEvent[] events = [At(3, 1), At(4, 1), At(5, 2), At(6, 2), At(7, 3)];

        Assert.Equal(4L, CombatResultProjection.LastSequenceBefore(events, 2));
        Assert.Equal(6L, CombatResultProjection.LastSequenceBefore(events, 3));
        Assert.Equal(-1L, CombatResultProjection.LastSequenceBefore(events, 1));
        Assert.Equal(7L, CombatResultProjection.LastSequenceBefore(events, 4));
        Assert.Equal(-1L, CombatResultProjection.LastSequenceBefore([], 2));
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
        var (match, attack) = ResolveAttackThenRehireTheDefendersSlot();
        var defender = new PlayerId(2);

        Assert.Null(match.FindGang(new GangId(30)));
        var page = Assert.Single(CombatResultProjection.Pages(match, defender));
        Assert.Equal(0, page.SectorId);
        var force = Assert.Single(page.ForcesFor(defender));
        Assert.Equal(new GangId(30), force.Gang);
        Assert.NotEmpty(CombatAnimationRouting.ForEvent(
            match, attack, defender, CombatForceTimeline.For(match, attack)));
    }

    [Fact]
    public void ALoadedMatchStillPresentsCombatAgainstARehiredSlot()
    {
        var (match, _) = ResolveAttackThenRehireTheDefendersSlot();
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        stream.Position = 0;

        var loaded = NativeSaveSerializer.Load(stream, match.Definitions);

        var attack = Assert.Single(loaded.Events, gameEvent =>
            gameEvent is { Kind: GameEventKind.CommandResolved, Action: GangAction.Attack });
        Assert.Equal(new CombatantDetails(new PlayerId(2), 1, 0, null, null, null),
            attack.Resolution!.Defender);
        var page = Assert.Single(CombatResultProjection.Pages(loaded, new PlayerId(2)));
        Assert.Equal(new GangId(30), Assert.Single(page.ForcesFor(new PlayerId(2))).Gang);
        Assert.NotEmpty(CombatAnimationRouting.ForEvent(
            loaded, attack, new PlayerId(2), CombatForceTimeline.For(loaded, attack)));
    }

    [Fact]
    public void CombatEventsRecordBothCombatantsAsTheyFought()
    {
        var (_, attack) = ResolveAttackThenRehireTheDefendersSlot();

        Assert.Equal(new CombatantDetails(new PlayerId(1), 1, 0, null, null, null),
            attack.Resolution!.Attacker);
        Assert.Equal(new CombatantDetails(new PlayerId(2), 1, 0, null, null, null),
            attack.Resolution.Defender);
    }

    [Fact]
    public void CombatantLookupPrefersTheLiveGangAndFallsBackToTheEvent()
    {
        var (match, attack) = ResolveAttackThenRehireTheDefendersSlot();

        Assert.Same(match.FindGang(new GangId(20)), match.FindCombatant(attack, new GangId(20)));
        var retired = match.FindCombatant(attack, new GangId(30))!;
        Assert.Equal(new PlayerId(2), retired.Owner);
        Assert.Equal(0, retired.Force);
        Assert.Same(retired, match.FindCombatant(attack, new GangId(30)));
        Assert.Null(match.FindCombatant(null, new GangId(30)));
        Assert.Null(match.FindCombatant(attack, new GangId(99)));
    }

    [Fact]
    public void PoliceAttackOnAGangWhoseSlotWasRehiredIsStillPresented()
    {
        var match = CreateCrackdownMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        var police = Assert.Single(match.Events,
            gameEvent => gameEvent.PoliceAttack is { Detected: true });
        var target = police.Gang!.Value;
        Assert.Equal(CombatantDetails.Of(match.FindGang(target)!),
            police.PoliceAttack!.Target);
        RetireAsHireDoes(match, target);
        match.FinishHire(new PlayerId(0));
        match.FinishPlayerElimination();

        var page = Assert.Single(CombatResultProjection.Pages(match, new PlayerId(0)));
        Assert.Equal(police.Sequence, Assert.Single(page.Results).Event.Sequence);
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
    /// Gang 20 attacks gang 30, which is then retired the way a same-turn hire retires it.
    /// </summary>
    private static (MatchState Match, GameEvent Attack) ResolveAttackThenRehireTheDefendersSlot()
    {
        var match = CreateObservedCombatMatch();
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        Assert.True(match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Attack,
            CommandTarget.Gang(new GangId(30)))).Accepted);
        match.FinishCommand(new PlayerId(1));
        match.FinishCommand(new PlayerId(2));
        foreach (var _ in TurnStructure.ExecutionOrder) match.FinishExecutionPhase();
        RetireAsHireDoes(match, new GangId(30));
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        var attack = Assert.Single(match.Events, gameEvent =>
            gameEvent is { Kind: GameEventKind.CommandResolved, Action: GangAction.Attack });
        return (match, attack);
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
            match.NextGangId(), gang.Owner, definitionId: 2, gang.SectorId, force: 7,
            statistics: EffectiveStatistics.From(match.Definitions.Gang(2).Stats)));
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
