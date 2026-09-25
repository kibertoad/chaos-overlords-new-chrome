using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// The in-memory values the computer players read, compared with the procedures of their spec
/// entries on concrete cases: <c>research_level</c> through <c>local_tech_cap</c> (FMT-STATE-002),
/// the first combat record read as the owner of sector index 64 (FMT-STATE-003) and
/// <c>needs_family</c> (FMT-STATE-007).
/// </summary>
public sealed class ComputerPlayerStateParityTests
{
    private const short ScienceCenterSite = 8;
    private const short ResearchLabSite = 4;

    // RULE-AI-005, RULE-AI-026, FND-AI-054: local_tech_cap lowers the gang definition's Tech Level
    // to 5, 8 or 10 by the research level of the gang's sector, whoever owns the sector. The
    // Research list a human sees counts the level only in the player's own sector (FND-RESEARCH-003).
    [Theory]
    [InlineData(null, 5, 5)]
    [InlineData(ScienceCenterSite, 8, 5)]
    [InlineData(ResearchLabSite, 10, 5)]
    public void TheComputerTechCapReadsTheResearchLevelOfAnotherPlayersSector(
        short? site, int computerCap, int researchListCap)
    {
        var match = CreateResearchMatch(site, sectorOwner: new PlayerId(1));
        var gang = match.FindGang(new GangId(10))!;

        Assert.Equal(computerCap, SpecialSiteRules.ComputerTechLimit(match, gang));
        Assert.Equal(researchListCap, SpecialSiteRules.ResearchTechLimit(match, gang));
    }

    [Theory]
    [InlineData(null, 5)]
    [InlineData(ScienceCenterSite, 8)]
    [InlineData(ResearchLabSite, 10)]
    public void InThePlayersOwnSectorBothCapsAgree(short? site, int cap)
    {
        var match = CreateResearchMatch(site, sectorOwner: new PlayerId(0));
        var gang = match.FindGang(new GangId(10))!;

        Assert.Equal(cap, SpecialSiteRules.ComputerTechLimit(match, gang));
        Assert.Equal(cap, SpecialSiteRules.ResearchTechLimit(match, gang));
    }

    // RULE-AI-005 first_affordable and RULE-AI-026 research_first apply the gang's own Tech Level
    // below the cap: a Tech 1 gang stays at 1 beside a Research Lab.
    [Fact]
    public void TheComputerTechCapNeverRaisesTheGangsOwnTechLevel()
    {
        var data = BundledOriginalData.Load();
        var lowTech = data.Gangs.Where(gang => gang.TechLevel < 5).MinBy(gang => gang.TechLevel)!;
        var match = CreateResearchMatch(ResearchLabSite, new PlayerId(1), lowTech.Id);

        Assert.Equal(lowTech.TechLevel,
            SpecialSiteRules.ComputerTechLimit(match, match.FindGang(new GangId(10))!));
    }

    // RULE-AI-026 research_first: with every item of the type up to Tech 5 researched, a family-7
    // gang in another player's sector with a Science Center finds the first item up to Tech 8.
    [Fact]
    public void AFamilySevenGangResearchesUnderTheCapOfAnotherPlayersSector()
    {
        var data = BundledOriginalData.Load();
        var (type, expected) = TypeWithFirstPendingItemAboveFive(data);
        var researched = data.Items
            .Where(item => item.Id is > 0 and < 64 && item.Type == type && item.TechLevel <= 5)
            .Select(item => item.Id)
            .ToHashSet();
        var match = CreateResearchMatch(ScienceCenterSite, new PlayerId(1), researched: researched);
        var player = match.FindPlayer(new PlayerId(0))!;

        Assert.Equal(expected, OriginalAiFamilySevenRules.SelectFirstResearchItemOfType(
            match, player, match.FindGang(new GangId(10))!, type));

        var ownHome = CreateResearchMatch(null, new PlayerId(1), researched: researched);
        Assert.Null(OriginalAiFamilySevenRules.SelectFirstResearchItemOfType(
            ownHome, ownHome.FindPlayer(new PlayerId(0))!, ownHome.FindGang(new GangId(10))!, type));
    }

    // RULE-RESEARCH-001 tests no Tech Level, so the original resolves a Research a computer player
    // plans under local_tech_cap above the Research list's limit. DEV-AI-002 holds computer players
    // to the orders a human could give, so the order is refused for either controller.
    [Theory]
    [InlineData(PlayerController.Computer)]
    [InlineData(PlayerController.Human)]
    public void AResearchAboveTheResearchListLimitIsRefusedForEitherController(
        PlayerController controller)
    {
        var data = BundledOriginalData.Load();
        var techEight = data.Items.First(item =>
            item.Id is > 0 and < 64 && item.Type != 99 && item.ResearchDifficulty > 0
            && item.TechLevel is > 5 and <= 8);
        var match = CreateResearchMatch(
            ScienceCenterSite, new PlayerId(1), controller: controller);
        match.FinishUpkeep();

        var validation = CommandValidator.Validate(match, new GameCommand(
            new PlayerId(0), new GangId(10), GangAction.Research, CommandTarget.Item(techEight.Id)));

        Assert.False(validation.IsValid);
        Assert.Equal(CommandValidationCode.ResearchTechLevelUnavailable, validation.Code);
    }

    // RULE-COMBAT-002, FMT-STATE-003: byte 0 of player 0's first combat record takes the definition
    // of the gang in roster slot 0 when that gang fights, here as an attack's target. A fight of
    // another slot leaves it at 0.
    [Theory]
    [InlineData(0, true)]
    [InlineData(1, false)]
    public void TheFirstCombatRecordTakesTheDefinitionOfSlotZeroWhenItFights(
        int targetSlot, bool written)
    {
        var expected = written ? VisibleDefinition() : 0;
        var match = CreateCombatMatch();
        Assert.Equal(0, match.AiPlanning.FirstCombatRecordDefinition);

        ResolveCombatTurn(match, match.FindPlayer(new PlayerId(0))!.Gangs[targetSlot].Id);

        Assert.Equal(expected, match.AiPlanning.FirstCombatRecordDefinition);
    }

    // FND-STATE-005: the record keeps its bytes through a phase in which its slot does not fight,
    // and the save carries it (FMT-STATE-003 records are saved with the game).
    [Fact]
    public void TheFirstCombatRecordOutlivesLaterPhasesAndASaveRoundTrip()
    {
        var match = CreateCombatMatch();
        ResolveCombatTurn(match, new GangId(10));
        foreach (var player in match.Players) match.FinishHire(player.Id);
        match.FinishPlayerElimination();
        match.FinishUpkeep();
        foreach (var player in match.Players) match.FinishCommand(player.Id);
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();

        Assert.Equal(VisibleDefinition(), match.AiPlanning.FirstCombatRecordDefinition);
        using var stream = new MemoryStream();
        NativeSaveSerializer.Save(stream, match);
        stream.Position = 0;
        var loaded = NativeSaveSerializer.Load(stream, match.Definitions);
        Assert.Equal(VisibleDefinition(), loaded.AiPlanning.FirstCombatRecordDefinition);
    }

    // RULE-AI-005 danger_near: for a gang in sector 56 the scan reaches index 64, whose owner is
    // combat_records[0].definition. Player 0 reads 0 as its own sector; once a Tech gang of
    // definition 3 has fought in slot 0, index 64 is another player's and calls for equipment.
    [Theory]
    [InlineData(0, false)]
    [InlineData(3, true)]
    public void DangerNearReadsTheFirstCombatRecordAsTheOwnerOfSectorSixtyFour(
        short definition, bool danger)
    {
        var match = CreateAnchorMatch(ScenarioId.Dominance, [], gangSectors: [56]);
        match.AiPlanning.RecordFirstCombatRecordDefinition(definition);
        var player = match.FindPlayer(new PlayerId(0))!;

        Assert.Equal(danger,
            OriginalAiEquipmentRules.NeedsFamilyOneEquipment(match, player, player.Gangs[0]));
    }

    // RULE-AI-013: the third pass takes the owned sector with the fewest cells the player does not
    // own, counting index 64 as owned by combat_records[0].definition. Sectors 56 and 63 each have
    // three foreign neighbours; with a definition of 3 at index 64, sector 56 has four.
    [Theory]
    [InlineData(0, 56)]
    [InlineData(3, 63)]
    public void TheHireAnchorCountsSectorSixtyFourAsOwnedByTheFirstCombatRecord(
        short definition, int anchor)
    {
        var match = CreateAnchorMatch(
            ScenarioId.Greed, owned: [56, 63], gangSectors: [56, 63], rivalOwnsRest: true);
        var player = new PlayerId(0);
        match.AiPlanning.RecordFirstCombatRecordDefinition(definition);
        // The second pass fails: each owned sector holds a gang whose previous action was Chaos.
        var gangs = match.FindPlayer(player)!.Gangs;
        for (var slot = 0; slot < gangs.Count; slot++)
            match.AiPlanning.SetPlannedAction(player, slot, GangAction.Chaos);
        match.AiPlanning.RollActiveGangActions(player, gangs);

        AiPlanningPreparation.RefreshHireAnchor(match, player);

        Assert.Equal(anchor, match.AiPlanning.SectorAnchor(player) - AiPlanningState.SectorAnchorOffset);
    }

    // RULE-AI-010 writes family 0 over the first surplus hunter and nothing else, so the
    // needs_family flag a family-6 Greed Terminate set earlier in the pass (RULE-AI-025) stays set.
    [Fact]
    public void TheSurplusHunterRewriteKeepsTheNeedsFamilyFlag()
    {
        var match = CreateAnchorMatch(
            ScenarioId.Greed, owned: [0, 1, 2, 3, 4, 5, 6], gangSectors: [0, 1]);
        var player = new PlayerId(0);
        match.AiPlanning.SetFamily(player, 0, 6);
        match.AiPlanning.SetFamily(player, 1, 12);
        match.AiPlanning.SetNeedsFamily(player, 0);

        AiPlanningPreparation.RevertSurplusHunter(match, player);

        Assert.Equal(0, match.AiPlanning.Family(player, 0));
        Assert.True(match.AiPlanning.NeedsFamily(player, 0));
        Assert.Equal(12, match.AiPlanning.Family(player, 1));
    }

    private static (int Type, int Item) TypeWithFirstPendingItemAboveFive(OriginalData data)
    {
        foreach (var type in new[] { 0, 1, 2, 3 })
        {
            if (data.Items.FirstOrDefault(item => item.Id is > 0 and < 64
                    && item.Type == type && item.TechLevel is > 5 and <= 8
                    && item.ResearchDifficulty > 0) is { } item)
                return (type, item.Id);
        }
        throw new InvalidOperationException("The bundled data has no item of Tech 6 to 8.");
    }

    private static MatchState CreateResearchMatch(
        short? site,
        PlayerId sectorOwner,
        short? gangDefinitionId = null,
        IReadOnlySet<short>? researched = null,
        PlayerController controller = PlayerController.Computer)
    {
        var data = BundledOriginalData.Load();
        var definition = gangDefinitionId ?? data.Gangs.First(gang => gang.TechLevel == 10).Id;
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "SEEKER", controller),
            new(new PlayerId(1), "HOST", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 500, [new MatchGangState(new GangId(10), setups[0].Id, definition, 0, 10)],
                researchedItems: researched),
            new(setups[1], 500, [new MatchGangState(new GangId(20), setups[1].Id, 2, 63, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                id == 0 && site is { } special
                    ? new MatchSiteState(1, special, 0, sectorOwner)
                    : new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: id == 0 ? sectorOwner : null))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(ScenarioId.Greed, GameDuration.SixMonths, 1996, setups),
            players, sectors);
    }

    /// <summary>
    /// Player 0 (a computer) holds a gang of <see cref="VisibleDefinition"/> in slot 0 and one of definition 1 in
    /// slot 1, both in sector 0, where player 1's gang 20 can attack either. Player 1's second gang
    /// keeps it in the match whatever the fight does.
    /// </summary>
    private static MatchState CreateCombatMatch()
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "FIRST", PlayerController.Computer),
            new(new PlayerId(1), "RAIDER", PlayerController.Human)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 500,
            [
                new MatchGangState(new GangId(10), setups[0].Id, VisibleDefinition(), 0, 10),
                new MatchGangState(new GangId(11), setups[0].Id, 1, 0, 10)
            ]),
            new(setups[1], 500,
            [
                new MatchGangState(new GangId(20), setups[1].Id, 1, 0, 10),
                new MatchGangState(new GangId(21), setups[1].Id, 1, 63, 10)
            ])
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

    /// <summary>A definition above 1 whose Stealth a definition-1 gang detects.</summary>
    private static short VisibleDefinition()
    {
        var data = BundledOriginalData.Load();
        var detect = data.Gang(1).Stats.Detect;
        return data.Gangs.First(gang => gang.Id > 1 && gang.Stats.Stealth <= detect).Id;
    }

    private static void ResolveCombatTurn(MatchState match, GangId target)
    {
        match.FinishUpkeep();
        match.FinishCommand(new PlayerId(0));
        var submission = match.Submit(new GameCommand(
            new PlayerId(1), new GangId(20), GangAction.Attack, CommandTarget.Gang(target)));
        Assert.True(submission.Accepted, submission.Validation.Code.ToString());
        match.FinishCommand(new PlayerId(1));
        while (match.Coordinator.Phase == TurnPhase.Execution) match.FinishExecutionPhase();
    }

    /// <summary>
    /// Two computer players. Player 0 owns <paramref name="owned"/> and has one gang in each of
    /// <paramref name="gangSectors"/>; player 1 keeps one gang in sector 0 and, with
    /// <paramref name="rivalOwnsRest"/>, owns every other sector.
    /// </summary>
    private static MatchState CreateAnchorMatch(
        ScenarioId scenario,
        IReadOnlyList<int> owned,
        IReadOnlyList<int> gangSectors,
        bool rivalOwnsRest = false)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "FIRST", PlayerController.Computer),
            new(new PlayerId(1), "SECOND", PlayerController.Computer)
        ];
        MatchPlayerState[] players =
        [
            new(setups[0], 500, gangSectors
                .Select((sector, index) => new MatchGangState(
                    new GangId(10 + index), setups[0].Id, 1, sector, 10))
                .ToArray()),
            new(setups[1], 500, [new MatchGangState(new GangId(20), setups[1].Id, 1, 0, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: owned.Contains(id) ? setups[0].Id : rivalOwnsRest ? setups[1].Id : null))
            .ToArray();
        return new MatchState(data,
            new MatchSetup(scenario, GameDuration.SixMonths, 1996, setups),
            players, sectors);
    }
}
