using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

public sealed class AiPolicyTests
{
    [Fact]
    public void OriginalIsDefaultAndAdvancedOnlyRecoversAnIdleGang()
    {
        var original = IdleMatch(AiPolicyMode.Original);
        var advanced = IdleMatch(AiPolicyMode.Advanced);
        var player = new PlayerId(0);

        Assert.Equal(AiPolicyMode.Original, original.Setup.AiPolicy);
        Assert.Empty(AiPolicyPlanner.Plan(original, player));
        var randomBefore = advanced.Random.ConsumptionCount;

        var command = Assert.Single(AiPolicyPlanner.Plan(advanced, player));

        Assert.Equal(GangAction.Heal, command.Action);
        Assert.Equal(randomBefore, advanced.Random.ConsumptionCount);
        Assert.True(CommandValidator.Validate(advanced, command).IsValid);
    }

    [Fact]
    public void AdvancedExpertExpandsHealthyIdleGangButCriminalKeepsFallbackOrder()
    {
        var criminal = IdleMatch(AiPolicyMode.Advanced, AiDifficulty.Criminal,
            force: 10, ownsStartingSector: true, gangCount: 2);
        var expert = IdleMatch(AiPolicyMode.Advanced, AiDifficulty.CrimeLord,
            force: 10, ownsStartingSector: true, gangCount: 2);

        Assert.DoesNotContain(AiPolicyPlanner.Plan(criminal, new PlayerId(0)),
            command => command.Action == GangAction.Move);
        var expertCommand = Assert.Single(
            AiPolicyPlanner.Plan(expert, new PlayerId(0)),
            command => command.Action == GangAction.Move);
        Assert.Equal(GangAction.Move, expertCommand.Action);
        Assert.NotEqual(new PlayerId(0),
            expert.Sectors[expertCommand.Target.Id].Owner);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(3, 2)]
    public void AdvancedExpertExpansionLeavesDefenderAtSource(
        int gangCount,
        int expectedMoves)
    {
        var match = IdleMatch(AiPolicyMode.Advanced, AiDifficulty.CrimeLord,
            force: 10, ownsStartingSector: true, gangCount: gangCount);

        Assert.Equal(expectedMoves,
            AiPolicyPlanner.Plan(match, new PlayerId(0))
                .Count(command => command.Action == GangAction.Move));
    }

    [Fact]
    public void AdvancedExpertDoesNotExpandPastDetectableLocalRival()
    {
        var match = IdleMatch(AiPolicyMode.Advanced, AiDifficulty.CrimeLord,
            force: 10, ownsStartingSector: true, gangCount: 2,
            rivalSector: 0);

        Assert.DoesNotContain(AiPolicyPlanner.Plan(match, new PlayerId(0)),
            command => command.Action == GangAction.Move);
    }

    [Theory]
    [InlineData(GangAction.Hide)]
    [InlineData(GangAction.Snitch)]
    [InlineData(GangAction.Bribe)]
    public void AdvancedExpertReplacesRepeatedPassiveActionWithExpansion(
        GangAction passiveAction)
    {
        var match = IdleMatch(AiPolicyMode.Advanced, AiDifficulty.HomicidalManiac,
            force: 10, ownsStartingSector: true, gangCount: 2);
        var player = new PlayerId(0);
        match.AiPlanning.SetPlannedAction(player, 0, passiveAction);
        match.AiPlanning.RollActiveGangActions(player, match.Players[0].Gangs);
        match.AiPlanning.SetPlannedAction(player, 0, passiveAction);

        Assert.Equal(GangAction.Move, Assert.Single(
            AiPolicyPlanner.Plan(match, player),
            command => command.Gang == new GangId(10)).Action);
        Assert.Equal(passiveAction, match.AiPlanning.PlannedAction(player, 0));
    }

    [Fact]
    public void PolicyContributesToCanonicalHash()
    {
        var original = IdleMatch(AiPolicyMode.Original);
        var advanced = IdleMatch(AiPolicyMode.Advanced);

        Assert.NotEqual(MatchStateHasher.ComputeSha256(original),
            MatchStateHasher.ComputeSha256(advanced));
        Assert.Equal(MatchStateHasher.ComputeVersionTwentyFiveSha256(original),
            MatchStateHasher.ComputeVersionTwentyFiveSha256(advanced));
    }

    [Fact]
    public void AdvancedPolicyRoundTripsThroughSaveAndReplay()
    {
        var match = IdleMatch(AiPolicyMode.Advanced);
        var data = match.Definitions;
        using var save = new MemoryStream();
        NativeSaveSerializer.Save(save, match);
        save.Position = 0;

        var restored = NativeSaveSerializer.Load(save, data);
        Assert.Equal(AiPolicyMode.Advanced, restored.Setup.AiPolicy);

        var recorder = new MatchReplayRecorder(match);
        using var replay = new MemoryStream();
        MatchReplaySerializer.Save(replay, recorder);
        replay.Position = 0;
        var replayed = MatchReplaySerializer.LoadAndReplay(replay, data);
        Assert.Equal(AiPolicyMode.Advanced, replayed.Setup.AiPolicy);
    }

    [Fact]
    public void OnlineSettingsCarryPolicyAndOldBlobsDefaultToOriginal()
    {
        var advanced = new MultiplayerGameSettings(
            ScenarioId.Greed, GameDuration.SixMonths, AiDifficulty.Criminal,
            [0, 1, 2, 3, 4, 5], AiPolicyMode.Advanced);

        Assert.Equal(AiPolicyMode.Advanced,
            MultiplayerGameSettings.FromWire(advanced.ToWire()).AiPolicy);
        var legacy = advanced.ToWire().Where(pair => !string.Equals(
                pair.Key, "aiPolicy", StringComparison.OrdinalIgnoreCase))
            .ToDictionary();
        Assert.Equal(AiPolicyMode.Original,
            MultiplayerGameSettings.FromWire(legacy).AiPolicy);
    }

    [Fact]
    public void InGameHelpStatesExactAdvancedContract()
    {
        var document = new ExtractedHelpDocument(
            ExtractedHelpDocument.CurrentFormatVersion, "Help", [], [], []);

        var topic = Assert.Single(HelpContentAugmentation.AddExecutableNotes(document).Topics,
            topic => topic.Title == "Advanced AI");

        Assert.Contains("normally keeps every command selected by Original AI", topic.Text);
        Assert.Contains("Heal an injured gang, Attack a detectable rival", topic.Text);
        Assert.Contains("consumes no random numbers", topic.Text);
        Assert.Contains("Goon and Criminal do not use this expansion override", topic.Text);
        Assert.Contains("no extra cash, statistics, discounts, damage, or success chance", topic.Text);
        Assert.True(topic.ListedInContents);
    }

    [Theory]
    [InlineData(AiPolicyMode.Original, "ORIGINAL")]
    [InlineData(AiPolicyMode.Advanced, "ADVANCED")]
    public void SetupPresentationNamesPolicy(AiPolicyMode policy, string label)
    {
        Assert.Equal(label, AiPolicyPresentation.Label(policy));
        Assert.Contains(label, AiPolicyPresentation.SelectionMessage(policy));
        var tooltip = AiPolicyPresentation.Tooltip(policy);
        Assert.Contains(tooltip, line => line.Contains("IDLE FALLBACK: HEAL"));
        Assert.Contains(tooltip, line => line.Contains("FORCE 8+"));
        Assert.Contains(tooltip, line => line.Contains("KEEP 1 DEFENDER"));
        Assert.Contains(tooltip, line => line.Contains("NO RNG, CASH, STAT"));
    }

    private static MatchState IdleMatch(
        AiPolicyMode policy,
        AiDifficulty difficulty = AiDifficulty.Criminal,
        int force = 5,
        bool ownsStartingSector = false,
        int gangCount = 1,
        int rivalSector = 1)
    {
        var data = BundledOriginalData.Load();
        MatchPlayerSetup[] setups =
        [
            new(new PlayerId(0), "CPU", PlayerController.Computer),
            new(new PlayerId(1), "RIVAL", PlayerController.Human)
        ];
        var setup = new MatchSetup(
            ScenarioId.Greed, GameDuration.SixMonths, 31, setups,
            aiMentality: difficulty, aiPolicy: policy);
        MatchPlayerState[] players =
        [
            new(setups[0], 50,
                Enumerable.Range(0, gangCount).Select(index =>
                    new MatchGangState(new GangId(10 + index), setups[0].Id,
                        4, 0, force)).ToArray()),
            new(setups[1], 50,
                [new MatchGangState(new GangId(20), setups[1].Id, 2, rivalSector, 10)])
        ];
        var sectors = Enumerable.Range(0, MatchLimits.SectorCount)
            .Select(id => new MatchSectorState(id,
            [
                new MatchSiteState(0, 0, 7),
                new MatchSiteState(1, 1, 5),
                new MatchSiteState(2, 2, 4)
            ], owner: ownsStartingSector && id == 0 ? setups[0].Id : null))
            .ToArray();
        var match = new MatchState(data, setup, players, sectors);
        match.FinishUpkeep();
        match.AiPlanning.BeginPlanning(setups[0].Id);
        for (var slot = 0; slot < gangCount; slot++)
        {
            match.AiPlanning.SetFamily(setups[0].Id, slot, 0);
            match.AiPlanning.SetPlannedAction(setups[0].Id, slot, GangAction.None);
        }
        return match;
    }
}
