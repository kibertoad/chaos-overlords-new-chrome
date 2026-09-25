using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// Fields of the sector record (FMT-STATE-002) whose values the rebuild once held differently
/// from the original: `cash_yield`, `tolerance` and `crackdown_turns`. Each expectation is worked
/// out from the procedure of the rule named on the test.
/// </summary>
public sealed class SectorRecordParityTests
{
    private const short CorporateTowers = 3;

    // RULE-UPKEEP-001, RULE-SITE-001, RULE-CONTROL-001: Upkeep runs before the rebuild, so the
    // new owner of a sector taken by Control collects the `cash_yield` the previous rebuild wrote,
    // with the Cash of the site whose progress the takeover set back to 0. The rebuild after that
    // Upkeep drops the site.
    [Fact]
    public void AControlTakeoverPaysTheNewOwnerTheOldCashYieldOnce()
    {
        var match = CreateTakeoverMatch();
        var data = match.Definitions;
        var towers = data.Site(CorporateTowers);
        match.FinishUpkeep();
        // The rebuild before the first planning: 1 plus the Cash of the one completed site.
        var previousYield = 1 + towers.Cash;
        Assert.Equal(previousYield, match.Sectors[0].CashYield);

        Assert.True(match.Submit(Control(0, 10)).Accepted);
        Assert.True(match.Submit(Control(0, 11)).Accepted);
        FinishTurn(match);
        Assert.Equal(new PlayerId(0), match.Sectors[0].Owner);
        Assert.Equal(towers.Resistance, match.Sectors[0].Sites[0].Resistance);
        var winnerCash = match.Players[0].Cash;
        var loserCash = match.Players[1].Cash;
        var winnerUpkeep = 2 * data.Gang(StrongControl(data)).Upkeep;
        var loserUpkeep = data.Gang(WeakControl(data)).Upkeep;

        match.FinishUpkeep();

        var winner = match.LastUpkeepResolutions.Single(result => result.Player == new PlayerId(0));
        Assert.Equal(1, winner.Details.SectorIncome);
        Assert.Equal(towers.Cash, winner.Details.SiteIncome);
        Assert.Equal(winnerCash + previousYield - winnerUpkeep, match.Players[0].Cash);
        Assert.Equal(loserCash - loserUpkeep, match.Players[1].Cash);
        // The next rebuild no longer counts the reset site.
        Assert.Equal(1, match.Sectors[0].CashYield);
        Assert.Equal(new EconomyForecast(match.Players[0].Cash, 1, 0, winnerUpkeep,
                match.Players[0].Cash + 1 - winnerUpkeep),
            EconomyResolver.Project(match, match.Players[0]));
    }

    // RULE-SITE-001, RULE-CHAOS-001: the headquarters site has Resistance 0, so it is complete at
    // progress 0 and the rebuild adds its Tolerance whoever owns the sector, neutral included.
    [Theory]
    [InlineData(12, false)]
    [InlineData(12, true)]
    [InlineData(1, false)]
    public void TheHeadquartersAddsItsToleranceWhoeverOwnsTheSector(int baseTolerance, bool owned)
    {
        var match = CreateHeadquartersMatch(baseTolerance, owned);
        var headquarters = match.Definitions.Site(MatchBootstrap.HeadquartersDefinitionId);
        Assert.Equal(0, headquarters.Resistance);

        match.FinishUpkeep();

        var sector = match.Sectors[0];
        Assert.Equal(baseTolerance + headquarters.Tolerance, sector.Tolerance);
        Assert.Equal(headquarters.Tolerance, ToleranceResolver.SiteAdjustment(match, sector));
        Assert.Equal(headquarters.Support, sector.Support);
        Assert.Equal(1 + headquarters.Cash, sector.CashYield);
    }

    // RULE-POLICE-002, RULE-SITE-001: a third Crackdown makes a headquarters sector neutral; the
    // next rebuild still adds the headquarters' Tolerance.
    [Fact]
    public void ANeutralizedHeadquartersSectorKeepsTheHeadquartersTolerance()
    {
        var match = CreateHeadquartersMatch(baseTolerance: 12, owned: true);
        var headquarters = match.Definitions.Site(MatchBootstrap.HeadquartersDefinitionId);
        SectorControlResolver.Neutralize(match, match.Sectors[0]);
        Assert.Null(match.Sectors[0].Owner);

        match.FinishUpkeep();

        Assert.Equal(12 + headquarters.Tolerance, match.Sectors[0].Tolerance);
    }

    // RULE-POLICE-003: after combat, a presence from 1 to 99 loses one turn; 0 and anything from
    // CRACKDOWN_PERMANENT (100) up stay.
    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 0)]
    [InlineData(5, 4)]
    [InlineData(99, 98)]
    [InlineData(100, 100)]
    [InlineData(104, 104)]
    public void PolicePresenceCountsDownOnlyBelowThePermanentValue(int before, int after)
    {
        var match = CreateHeadquartersMatch(baseTolerance: 12, owned: false);
        match.Sectors[5].CrackdownTurnsRemaining = before;

        CrackdownResolver.FinishCombat(match);

        Assert.Equal(after, match.Sectors[5].CrackdownTurnsRemaining);
    }

    // FMT-STATE-002, RULE-POLICE-002: `crackdown_turns` is a signed byte. A neutralizing
    // Crackdown on a presence of 125 adds 3 to 5 and wraps it to -128 to -126. RULE-POLICE-003
    // then leaves it, the police phase and the turn-start test read "above 0" and see no police,
    // and the Control pass and the computer players read "not 0" and see a Crackdown
    // (RULE-POLICE-001, RULE-TURN-004, RULE-CONTROL-001, RULE-AI-004).
    [Fact]
    public void RepeatedNeutralizationsWrapThePolicePresencePast127()
    {
        var match = CreateHeadquartersMatch(baseTolerance: 12, owned: false);
        var sector = match.Sectors[6];
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        CrackdownResolver.Trigger(match, sector);
        AdvanceCoordinatorTurn(match);
        sector.CrackdownTurnsRemaining = 125;

        var result = CrackdownResolver.Trigger(match, sector);

        Assert.InRange(result.Duration, 3, 5);
        Assert.Equal(125 + result.Duration - 256, sector.CrackdownTurnsRemaining);
        Assert.False(sector.CrackdownActive);
        Assert.True(sector.HasCrackdownTurns);
        Assert.Equal(-2, AiTurnPlanner.OwnerQuery(match, sector.Id));
        var wrapped = sector.CrackdownTurnsRemaining;
        CrackdownResolver.FinishCombat(match);
        Assert.Equal(wrapped, sector.CrackdownTurnsRemaining);
    }

    // FMT-STATE-002: a wrapped presence survives a save and its fingerprint.
    [Fact]
    public void AWrappedPolicePresenceRoundTripsThroughASave()
    {
        var match = CreateHeadquartersMatch(baseTolerance: 12, owned: false);
        match.Sectors[6].CrackdownTurnsRemaining = -126;
        using var stream = new MemoryStream();
        Rechaos.Core.Persistence.NativeSaveSerializer.Save(stream, match);
        stream.Position = 0;

        var restored = Rechaos.Core.Persistence.NativeSaveSerializer.Load(stream, match.Definitions);

        Assert.Equal(-126, restored.Sectors[6].CrackdownTurnsRemaining);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(match), MatchStateHasher.ComputeFingerprint(restored));
    }

    // RULE-SETUP-005, RULE-POLICE-003: the island name puts every neutral sector under police of
    // 100 turns, and no number of countdowns removes them.
    [Fact]
    public void TheIslandNamePoliceNeverLeave()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] players =
        [
            new(new PlayerId(0), "SMGISLANDS", PlayerController.Human),
            new(new PlayerId(1), "TWO", PlayerController.Computer)
        ];
        var match = OriginalMatchFactory.Create(
            data, new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, players));
        var neutral = match.Sectors.Where(sector => sector.Owner is null).ToArray();
        Assert.NotEmpty(neutral);
        Assert.All(neutral, sector =>
            Assert.Equal(CrackdownResolver.PermanentCrackdownTurns, sector.CrackdownTurnsRemaining));

        for (var turn = 0; turn < 150; turn++) CrackdownResolver.FinishCombat(match);

        Assert.All(neutral, sector =>
        {
            Assert.Equal(CrackdownResolver.PermanentCrackdownTurns, sector.CrackdownTurnsRemaining);
            Assert.True(sector.CrackdownActive);
        });
    }

    private static void AdvanceCoordinatorTurn(MatchState match)
    {
        var coordinator = match.Coordinator;
        coordinator.FinishUpkeep();
        foreach (var player in match.Players) coordinator.FinishCommand(player.Id);
        while (coordinator.Phase == TurnPhase.Execution) coordinator.FinishExecutionPhase();
        foreach (var player in match.Players) coordinator.FinishHire(player.Id);
        coordinator.FinishPlayerElimination();
    }

    private static void FinishTurn(MatchState match)
    {
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
    }

    private static GameCommand Control(int player, int gang) =>
        new(new PlayerId(player), new GangId(gang), GangAction.Control, CommandTarget.None);

    private static short StrongControl(OriginalData data) =>
        data.Gangs.OrderByDescending(gang => gang.Stats.Control).First().Id;

    private static short WeakControl(OriginalData data) =>
        data.Gangs.OrderBy(gang => gang.Stats.Control).First().Id;

    private static MatchState CreateTakeoverMatch()
    {
        var data = BundledOriginalData.Load();
        var setup = Setup();
        var owner = new PlayerId(1);
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500,
            [
                new MatchGangState(new GangId(10), new PlayerId(0), StrongControl(data), 0, 10),
                new MatchGangState(new GangId(11), new PlayerId(0), StrongControl(data), 0, 10)
            ]),
            new(setup.Players[1], 500,
                [new MatchGangState(new GangId(20), owner, WeakControl(data), 0, 1)],
                support: data.Site(CorporateTowers).Support)
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                id == 0
                    ? new MatchSiteState(0, CorporateTowers, 0, owner)
                    : new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? owner : null, income: 2))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }

    private static MatchState CreateHeadquartersMatch(int baseTolerance, bool owned)
    {
        var data = BundledOriginalData.Load();
        var setup = Setup();
        MatchPlayerState[] players =
        [
            new(setup.Players[0], 500,
                [new MatchGangState(new GangId(10), new PlayerId(0), 1, 1, 5)]),
            new(setup.Players[1], 500,
                [new MatchGangState(new GangId(20), new PlayerId(1), 1, 2, 5)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0,
                    id == 0 ? MatchBootstrap.HeadquartersDefinitionId : (short)0, id == 0 ? 0 : 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 && owned ? new PlayerId(0) : null,
                tolerance: id == 0 ? baseTolerance : 14,
                crackdownActive: id == 5,
                crackdownTurnsRemaining: id == 5 ? 3 : 0))
            .ToArray();
        return new MatchState(data, setup, players, sectors);
    }

    private static MatchSetup Setup() => new(
        ScenarioId.Greed, GameDuration.SixMonths, 1996,
        [
            new MatchPlayerSetup(new PlayerId(0), "ONE", PlayerController.Human),
            new MatchPlayerSetup(new PlayerId(1), "TWO", PlayerController.Computer)
        ]);
}
