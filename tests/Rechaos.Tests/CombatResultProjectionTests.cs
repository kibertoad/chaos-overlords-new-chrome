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

    private static MatchGangState Gang(int id, int owner, int sector) =>
        new(new GangId(id), new PlayerId(owner), definitionId: 1, sector, force: 10);
}
