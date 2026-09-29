using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// EXP-SETUP-001 to EXP-SETUP-004: new local games of the original, recorded from Begin to the
/// first planning phase under a debugger. Each run gives the seed, every roll(n) with its call site
/// and result, and the state the first planning phase starts from. The rebuild starts the same
/// match from the same seed and settings and has to reach the same generator position and state.
/// </summary>
public sealed class OriginalNewGameExperimentTests
{
    private static readonly string[] Experiments = ["EXP-SETUP-001", "EXP-SETUP-002", "EXP-SETUP-003", "EXP-SETUP-004"];

    private static readonly Lazy<IReadOnlyDictionary<string, RecordedRun[]>> Recorded =
        new(() => Experiments.ToDictionary(experiment => experiment, LoadRuns));

    public static TheoryData<string, int> Runs()
    {
        var data = new TheoryData<string, int>();
        foreach (var experiment in Experiments)
            for (var run = 0; run < Recorded.Value[experiment].Length; run++)
                data.Add(experiment, run);
        return data;
    }

    // RULE-RNG-001, RULE-RNG-002: every result the original returned follows from the seed.
    [Theory]
    [MemberData(nameof(Runs))]
    public void EveryRollFollowsFromTheSeed(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var random = new DeterministicRandom(recorded.Seed);
        foreach (var (call, bound, result) in recorded.Rolls)
            Assert.True(random.NextInclusive(bound) == result, $"roll({bound}) at {call}");
    }

    // RULE-SETUP-001, RULE-SETUP-003, RULE-SETUP-004, RULE-SETUP-005, RULE-SETUP-006,
    // RULE-SETUP-007, RULE-AI-014, RULE-AI-018, RULE-CITY-001, RULE-CITY-002, RULE-CITY-003,
    // RULE-CITY-004, RULE-RESEARCH-002, RULE-HIRE-002, RULE-HIRE-004, RULE-SITE-001,
    // RULE-GANG-001, RULE-DETECT-001, FND-HIRE-005: the rebuild's new match reaches the state the original's first planning phase starts from, with the same
    // number of draws. The state is read with the layouts of FMT-STATE-001, FMT-STATE-002 and
    // FMT-STATE-004.
    [Theory]
    [MemberData(nameof(Runs))]
    public void TheRebuildStartsTheSameMatch(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded);
        var human = recorded.Humans[0];

        Assert.Equal(recorded.Rolls.Count * 3L, match.Random.ConsumptionCount);
        var expectedState = new DeterministicRandom(recorded.Seed);
        for (var draw = 0; draw < recorded.Rolls.Count * 3; draw++) expectedState.NextRaw();
        Assert.Equal(expectedState.State, match.Random.State);

        foreach (var player in match.Players)
        {
            var slot = player.Id.Value;
            Assert.Equal(recorded.Term("portrait", slot), player.Setup.PortraitId);
            Assert.Equal(recorded.Term("cash", slot), player.Cash);
            Assert.Equal(recorded.Term("reaction", slot), match.AiStrategy.Reaction(player.Id));
            Assert.Equal(recorded.Term("difficulty_band", slot), (int)OriginalResolutionRules.Band(match, player.Id));
            Assert.Equal(recorded.Term("hire_force_modifier", slot) != 0, player.UsesMaximumHireForce);
            foreach (var other in match.Players)
                Assert.Equal(
                    recorded.Term("attitude", slot * 6 + other.Id.Value),
                    match.AiStrategy.Attitude(player.Id, other.Id));
            // The rebuild keeps no research for the item table's padding records (RULE-RESEARCH-002).
            foreach (var item in match.Definitions.Items.Select(definition => definition.Id))
                Assert.True(
                    recorded.Term("research_remaining", item * 6 + slot) == player.RemainingResearch(match.Definitions, item),
                    $"research_remaining of item {item} for player {slot}");
        }

        // An offer slot holds 0x9C while vacant (FND-SETUP-015).
        var offers = match.Players[human.Value].HireOfferSlots.Select(offer => (int?)offer.GangDefinitionId ?? -100);
        Assert.Equal(Enumerable.Range(0, 3).Select(slot => recorded.Term("hire_offers", human.Value * 3 + slot)), offers);

        foreach (var sector in match.Sectors)
        {
            var s = sector.Id;
            Assert.Equal(recorded.Sector(s, "owner"), sector.Owner?.Value ?? -1);
            Assert.Equal(recorded.Sector(s, "income"), sector.Income);
            Assert.Equal(recorded.Sector(s, "base_tolerance"), sector.BaseTolerance);
            Assert.Equal(recorded.Sector(s, "tolerance"), sector.Tolerance);
            Assert.Equal(recorded.Sector(s, "support"), sector.Support);
            Assert.Equal(recorded.Sector(s, "cash_yield"), sector.CashYield);
            Assert.Equal(recorded.Sector(s, "crackdown_turns"), sector.CrackdownTurnsRemaining);
            for (var site = 0; site < 3; site++)
            {
                Assert.Equal(recorded.Sector(s, $"sites[{site}].definition"), sector.Sites[site].DefinitionId);
                // FMT-STATE-004: the rebuild holds the Resistance still needed where the original
                // holds the progress made.
                var definition = match.Definitions.Sites.Single(entry => entry.Id == sector.Sites[site].DefinitionId);
                Assert.Equal(recorded.Sector(s, $"sites[{site}].progress"), definition.Resistance - sector.Sites[site].Resistance);
            }
        }

        foreach (var player in match.Players)
        {
            var gangs = player.Gangs.Where(gang => gang.IsActive).ToArray();
            var records = recorded.GangRecords(player.Id.Value);
            Assert.Equal(records.Count, gangs.Length);
            for (var slot = 0; slot < gangs.Length; slot++)
            {
                var record = records[slot];
                var gang = gangs[slot];
                Assert.Equal(recorded.Gang(record, "definition"), gang.DefinitionId);
                Assert.Equal(recorded.Gang(record, "sector"), gang.SectorId);
                Assert.Equal(recorded.Gang(record, "force"), gang.Force);
                Assert.Equal(recorded.Gang(record, "weapon"), gang.WeaponItemId ?? -1);
                Assert.Equal(recorded.Gang(record, "armor"), gang.ArmorItemId ?? -1);
                Assert.Equal(recorded.Gang(record, "misc"), gang.MiscellaneousItemId ?? -1);
                var stats = EffectiveStatisticsCalculator.ForGang(match, gang);
                int[] values =
                [
                    stats.Combat, stats.Defense, stats.Stealth, stats.Detect, stats.Chaos, stats.Control,
                    stats.Heal, stats.Influence, stats.Research, stats.Strength, stats.Blade, stats.Range,
                    stats.Fighting, stats.MartialArts,
                ];
                string[] names =
                [
                    "combat", "defense", "stealth", "detect", "chaos", "control", "heal", "influence",
                    "research", "strength", "blade", "ranged", "fighting", "martial_arts",
                ];
                for (var i = 0; i < names.Length; i++)
                    Assert.True(recorded.Gang(record, names[i]) == values[i], $"{names[i]} of gang record {record}");
                foreach (var observer in match.Players)
                    Assert.Equal(
                        recorded.Gang(record, $"visible_to[{observer.Id.Value}]") != 0,
                        observer.Id == player.Id || match.CanPlayerDetectGang(observer.Id, gang.Id));
            }
        }

        // FND-SETUP-018: the turn limit the computer players read.
        Assert.Equal(recorded.Term("turn_limit", 0), ScenarioCatalog.TurnLimit(match.Setup.Scenario, match.Setup.Duration));
        Assert.Equal(recorded.Term("elapsed_turns", 0), match.Coordinator.Turn - 1);
        Assert.Equal(human, match.Coordinator.ActivePlayer);
    }

    private static MatchState StartMatch(RecordedRun recorded)
    {
        var scenario = OriginalScenario(recorded.Term("scenario", 0));
        var setup = new MatchSetup(
            scenario,
            ScenarioCatalog.Get(scenario).IsTimed ? Duration(recorded.Term("turn_limit", 0)) : GameDuration.OneYear,
            recorded.Seed,
            recorded.Humans.Select(slot => new MatchPlayerSetup(
                slot, Name(recorded, slot.Value), PlayerController.Human, (short)recorded.Term("portrait", slot.Value))).ToArray(),
            (AiDifficulty)recorded.Term("mentality", 0),
            allowSparsePlayerIds: true);
        var match = OriginalMatchFactory.Create(BundledOriginalData.Load(), setup);
        match.FinishUpkeep();
        // RULE-SETUP-008: with several local humans the planning phase waits on the Ready card
        // before it refills the offers, and the recording stops there.
        if (recorded.Humans.Count == 1) match.PrepareHireOffers(recorded.Humans[0]);
        return match;
    }

    // The fixture keeps the flags a name set rather than the name (FND-SETUP-015), so the name is
    // the modifier string whose flag is set.
    private static string Name(RecordedRun recorded, int slot)
    {
        (string Flag, string Name)[] modifiers =
        [
            ("modifier_right_hands", OriginalSetupNameRules.ExtraRightHandsName),
            ("modifier_visibility", OriginalSetupNameRules.OmniscienceName),
            ("hire_force_modifier", OriginalHireCheatRules.MaximumForceName),
            ("modifier_elite", OriginalSetupNameRules.AssaultTeamName),
            ("modifier_islands", OriginalSetupNameRules.IslandsName),
            ("modifier_cash", OriginalSetupNameRules.MaximumStartingCashName),
        ];
        return modifiers.Where(modifier => recorded.Term(modifier.Flag, slot) != 0)
            .Select(modifier => modifier.Name).SingleOrDefault() ?? $"PROBE{slot}";
    }

    private static GameDuration Duration(int turns) => turns switch
    {
        26 => GameDuration.SixMonths,
        52 => GameDuration.OneYear,
        104 => GameDuration.TwoYears,
        208 => GameDuration.FourYears,
        _ => throw new ArgumentOutOfRangeException(nameof(turns), turns, null),
    };

    // The original numbers Siege 6 and Eliminate 7, the rebuild the other way round (RULE-SETUP-002).
    private static ScenarioId OriginalScenario(int value) => value switch
    {
        6 => ScenarioId.Siege,
        7 => ScenarioId.Eliminate,
        _ => (ScenarioId)value,
    };

    private static RecordedRun Run(string experiment, int run) => Recorded.Value[experiment][run];

    private static RecordedRun[] LoadRuns(string experiment)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{experiment}.json")));
        return fixture.RootElement.GetProperty("runs").EnumerateArray()
            .Select(run => new RecordedRun(run)).ToArray();
    }

    private sealed class RecordedRun
    {
        private readonly Dictionary<(string, int), int> _terms = [];
        private readonly Dictionary<(string, int, string), int> _fields = [];

        public RecordedRun(JsonElement run)
        {
            Seed = run.GetProperty("rng_state").GetInt32();
            Rolls = run.GetProperty("rolls").EnumerateArray()
                .Select(roll => (roll[0].GetString()!, roll[1].GetInt32(), roll[2].GetInt32()))
                .ToArray();
            foreach (var row in run.GetProperty("end_state").EnumerateArray())
            {
                var value = row.GetProperty("value").GetInt32();
                if (row.TryGetProperty("term", out var term))
                    _terms[(term.GetString()!, row.GetProperty("index").GetInt32())] = value;
                else
                    _fields[(row.GetProperty("format").GetString()!, row.GetProperty("record").GetInt32(),
                        row.GetProperty("field").GetString()!)] = value;
            }
        }

        public int Seed { get; }

        // controller: 0 for a human at this computer (FND-SETUP-002).
        public IReadOnlyList<PlayerId> Humans => Enumerable.Range(0, 6)
            .Where(slot => Term("controller", slot) == 0).Select(slot => new PlayerId(slot)).ToArray();
        public IReadOnlyList<(string Call, int Bound, int Result)> Rolls { get; }

        public int Term(string term, int index) => _terms[(term, index)];

        public int Sector(int sector, string field) => _fields[("FMT-STATE-002", sector, field)];

        public int Gang(int record, string field) => _fields[("FMT-STATE-001", record, field)];

        public IReadOnlyList<int> GangRecords(int player) => _fields.Keys
            .Where(key => key.Item1 == "FMT-STATE-001" && key.Item3 == "player" && key.Item2 / 81 == player)
            .Select(key => key.Item2).Order().ToArray();
    }
}
