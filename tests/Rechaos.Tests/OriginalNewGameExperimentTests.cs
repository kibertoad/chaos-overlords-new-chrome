using System.Text.Json;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// EXP-SETUP-001 to EXP-SETUP-004: new local games of the original, recorded from Begin to the
/// first planning phase under a debugger. EXP-TURN-001 to EXP-TURN-033 go on to press Done for one
/// to thirty turns, EXP-TURN-009 to EXP-TURN-022 other than EXP-TURN-018, EXP-TURN-027 and
/// EXP-TURN-029 to EXP-TURN-033 with orders for the human's gangs, and stop at the next planning
/// phase. Each run gives the seed, every roll(n) with its call site and result, and the state the recording stops at. The rebuild plays the same match from the same seed and
/// settings and has to make the same rolls in the same order and reach the same generator position
/// and state. The turns check the turn order (RULE-TURN-001), the computer players' planning passes,
/// family dispatch, sector choices, hire choices and hire placement (RULE-AI-001, RULE-AI-002,
/// RULE-AI-006, RULE-AI-008, RULE-AI-009, RULE-AI-010, RULE-AI-013) and the hire resolution
/// (RULE-HIRE-001). EXP-TURN-012 to EXP-TURN-014 play Siege, Eliminate and Big Man, whose computer
/// gangs take families 10 to 14 (RULE-AI-028, RULE-AI-029, RULE-AI-030, RULE-AI-031). The computer
/// players' orders take the resolution through its fixed order of steps (RULE-TURN-002): the instant
/// phase with Heal, Influence and Research (RULE-TURN-003, RULE-HEAL-001, RULE-INFLUENCE-001,
/// RULE-RESEARCH-001), Equip in the transaction pass (RULE-EQUIP-001, RULE-EQUIP-002,
/// RULE-EQUIP-003), Move (RULE-MOVE-001), Control (RULE-CONTROL-001), Chaos and its payout
/// (RULE-CHAOS-001, RULE-CHAOS-002) and upkeep (RULE-UPKEEP-001). EXP-TURN-009 also gives the human's
/// gang orders: Snitch, Bribe and Hide (RULE-SNITCH-001, RULE-BRIBE-001, RULE-HIDE-001) and a
/// recurring Chaos (RULE-TURN-004), which bring on a Crackdown. EXP-TURN-011 has the human's gang
/// attack a computer player's gang, which strikes back (RULE-ATTACK-001), with the damage taken off
/// Force at the end of the combat phase (RULE-COMBAT-002). Every run compares each gang's Combat with
/// its weapon skills (RULE-COMBAT-001), each sector's base Tolerance after its return toward normal
/// (RULE-TOLERANCE-001) and every attitude after its rise, or at Homicidal Maniac in EXP-TURN-008
/// without one (RULE-AI-015). EXP-TURN-010's first run brings police attacks and a Crackdown that
/// neutralizes a sector (RULE-POLICE-001, RULE-POLICE-002, RULE-POLICE-003). EXP-TURN-016 has the
/// human's gang buy three items and sell them in one order (RULE-SELL-001). In EXP-TURN-017 computer
/// players' gangs attack the human's hiding gang, which evades three attacks and dies of the fourth
/// (RULE-ATTACK-001, RULE-GANG-002), and the replay stops where the eliminated human would plan. In
/// EXP-TURN-018 the human's gang does not hide and family-2 gangs attack it (RULE-AI-021), and in
/// EXP-TURN-019 it is ordered to Terminate (RULE-TERMINATE-001). In EXP-TURN-020 it walks into
/// computer land, where a family-5 and a family-7 gang attack it (RULE-AI-024, RULE-AI-026), and in
/// EXP-TURN-021 a family-1 gang in an enemy sector counts a third player's gangs there before it
/// plans Control (RULE-AI-004). In EXP-TURN-022, at Mentality 3, a family-3 gang draws it as its
/// target and then fails a strength test made on the record at its sector's slot number
/// (RULE-AI-022, BUG-AI-007). EXP-TURN-023 and EXP-TURN-024 leave the human idle through Power at
/// Goon and at Crime Lord, where family-1 gangs snitch and attack (RULE-AI-020) and a Goon computer
/// player's Influence sets a site's progress to its pool plus its successes (BUG-INFLUENCE-001). In
/// EXP-TURN-025 and EXP-TURN-026, Kill 'Em All and Armageddon with the human idle, family-6 gangs
/// fail their strength test and buy equipment (RULE-AI-025). In EXP-TURN-027 the human hires a
/// second gang and its first gang gives it a weapon and an armor (RULE-HIRE-001, RULE-GIVE-001).
/// EXP-TURN-028 plays a six-month Greed to its 25th turn, where the computer players stop hiring
/// in the closing turns (RULE-AI-011). In EXP-TURN-029 a hired Martial Artist attacks bare handed
/// and its armed target does not strike back (RULE-ATTACK-001). In EXP-TURN-030 two gangs swap
/// weapons by Give, then both give to a third that buys a weapon in the same turn (RULE-GIVE-001).
/// EXP-TURN-031 buys at the exact price, one short, and one short with a Sell by an earlier and by a
/// later roster slot (RULE-EQUIP-001, RULE-EQUIP-002). In EXP-TURN-032 six Snitches in a row drive
/// a sector's base Tolerance to the clamp at 1 (RULE-SNITCH-001, RULE-TOLERANCE-002). In
/// EXP-TURN-033 the human bribes every turn until a Bribe meets 2 cash and fails (RULE-BRIBE-001).
/// EXP-TURN-037 plays a six-month Greed to its end with the 26th resolution (RULE-OBJECTIVE-001,
/// RULE-OBJECTIVE-004) and compares the awards the endgame gives (RULE-AWARDS-001). EXP-TURN-039 and
/// EXP-TURN-038 do the same in Acceptance and Dominance with the human hiding every turn, where a
/// site completed in a turn counts in that turn's score (RULE-OBJECTIVE-002) and in the sector and
/// gang refresh that ends the match (RULE-SITE-001, RULE-GANG-001). EXP-TURN-041 plays another
/// six-month Greed to its end and stops at the final city view, before the awards are given.
/// Every computer player's pass starts from its sector weights and the hostility step
/// (RULE-AI-003). Its hires land in the sector the planner encodes (RULE-AI-012), and gangs of the
/// default family plan by their previous action (RULE-AI-019). Its upgrade choices test danger
/// around the gang's sector, the centre included, which EXP-TURN-017 needs (RULE-AI-005).
/// </summary>
public sealed class OriginalNewGameExperimentTests
{
    private static readonly string[] Experiments = ["EXP-SETUP-001", "EXP-SETUP-002", "EXP-SETUP-003", "EXP-SETUP-004", "EXP-TURN-001", "EXP-TURN-002", "EXP-TURN-003", "EXP-TURN-004", "EXP-TURN-005", "EXP-TURN-006", "EXP-TURN-007", "EXP-TURN-008", "EXP-TURN-009", "EXP-TURN-010", "EXP-TURN-011", "EXP-TURN-012", "EXP-TURN-013", "EXP-TURN-014", "EXP-TURN-015", "EXP-TURN-016", "EXP-TURN-017", "EXP-TURN-018", "EXP-TURN-019", "EXP-TURN-020", "EXP-TURN-021", "EXP-TURN-022", "EXP-TURN-023", "EXP-TURN-024", "EXP-TURN-025", "EXP-TURN-026", "EXP-TURN-027", "EXP-TURN-028", "EXP-TURN-029", "EXP-TURN-030", "EXP-TURN-031", "EXP-TURN-032", "EXP-TURN-033", "EXP-TURN-034", "EXP-TURN-035", "EXP-TURN-037", "EXP-TURN-038", "EXP-TURN-039", "EXP-TURN-040", "EXP-TURN-041", "EXP-TURN-042", "EXP-TURN-046", "EXP-TURN-047"];

    private static readonly Lazy<IReadOnlyDictionary<string, RecordedRun[]>> Recorded =
        new(() => Experiments.ToDictionary(experiment => experiment, LoadRuns));

    // Runs the rebuild does not replay, with the first roll that differs.
    private static readonly Dictionary<(string Experiment, int Run), int> KnownDivergences = new()
    {
        // DEV-AI-007: in turn 5 player 5's gang in sector 9 is given Move to sector 5, which the
        // original carries out and the rebuild refuses; player 5's next tie count differs.
        [("EXP-TURN-015", 0)] = 950,
    };

    // Runs that stop at the final city view, before the awards controller builds the award table
    // (FND-OBJECTIVE-004, FND-UI-041). Their fixtures hold no award rows; every other run that
    // ends the match has to.
    private static readonly HashSet<string> EndpointsBeforeAwards = ["EXP-TURN-041", "EXP-TURN-042"];

    public static TheoryData<string, int> MatchingRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var experiment in Experiments)
            for (var run = 0; run < Recorded.Value[experiment].Length; run++)
                if (!KnownDivergences.ContainsKey((experiment, run))) data.Add(experiment, run);
        return data;
    }

    public static TheoryData<string, int> DivergingRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var ((experiment, run), _) in KnownDivergences) data.Add(experiment, run);
        return data;
    }

    // A known divergence stays where it was found; when a fix moves it, the entry above goes.
    [Theory(SkipTestWithoutData = true)]
    [MemberData(nameof(DivergingRuns))]
    public void AKnownDivergenceIsStillWhereItWasFound(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var rolls = new List<(int Bound, int Result)>();
        DeterministicRandom.RollObserver = (bound, result) => rolls.Add((bound, result));
        try
        {
            StartMatch(recorded, out _);
        }
        finally
        {
            DeterministicRandom.RollObserver = null;
        }

        var first = Enumerable.Range(0, Math.Min(rolls.Count, recorded.Rolls.Count))
            .First(index => rolls[index] != (recorded.Rolls[index].Bound, recorded.Rolls[index].Result));
        Assert.Equal(KnownDivergences[(experiment, run)], first);
    }

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
    [MemberData(nameof(MatchingRuns))]
    public void TheRebuildStartsTheSameMatch(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var rolls = new List<(int Bound, int Result)>();
        MatchState match;
        int donePresses;
        DeterministicRandom.RollObserver = (bound, result) => rolls.Add((bound, result));
        try
        {
            match = StartMatch(recorded, out donePresses);
        }
        finally
        {
            DeterministicRandom.RollObserver = null;
        }

        var human = recorded.Humans[0];

        // The first roll that differs, named by the original's call instruction.
        for (var index = 0; index < Math.Min(rolls.Count, recorded.Rolls.Count); index++)
        {
            var (call, bound, result) = recorded.Rolls[index];
            Assert.True(
                rolls[index] == (bound, result),
                $"roll {index}: the original called roll({bound}) at {call} and got {result}, the rebuild roll({rolls[index].Bound}) and got {rolls[index].Result}");
        }

        // The replay stops early only at the human's elimination or the match's outcome, and an
        // eliminated human presses Done no more and a decided match has no planning phase
        // (RULE-OBJECTIVE-005), so the recording has to end there. Checked after the rolls, so a
        // divergence of the rebuild is still named by its first differing roll.
        Assert.True(
            donePresses == recorded.DoneCount,
            $"the replay stopped after {donePresses} of the recording's {recorded.DoneCount} Done presses");

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
            Assert.True(recorded.Sector(s, "owner") == (sector.Owner?.Value ?? -1),
                $"sector {s} owner: the original holds {recorded.Sector(s, "owner")}, the rebuild {sector.Owner?.Value ?? -1}");
            Assert.True(recorded.Sector(s, "income") == sector.Income,
                $"sector {s} income: the original holds {recorded.Sector(s, "income")}, the rebuild {sector.Income}");
            Assert.True(recorded.Sector(s, "base_tolerance") == sector.BaseTolerance,
                $"sector {s} base_tolerance: the original holds {recorded.Sector(s, "base_tolerance")}, the rebuild {sector.BaseTolerance}");
            Assert.True(recorded.Sector(s, "tolerance") == sector.Tolerance,
                $"sector {s} tolerance: the original holds {recorded.Sector(s, "tolerance")}, the rebuild {sector.Tolerance}");
            Assert.True(recorded.Sector(s, "support") == sector.Support,
                $"sector {s} support: the original holds {recorded.Sector(s, "support")}, the rebuild {sector.Support}");
            Assert.True(recorded.Sector(s, "cash_yield") == sector.CashYield,
                $"sector {s} cash_yield: the original holds {recorded.Sector(s, "cash_yield")}, the rebuild {sector.CashYield}");
            Assert.True(recorded.Sector(s, "crackdown_turns") == sector.CrackdownTurnsRemaining,
                $"sector {s} crackdown_turns: the original holds {recorded.Sector(s, "crackdown_turns")}, the rebuild {sector.CrackdownTurnsRemaining}");
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
                Assert.True(recorded.Gang(record, "sector") == gang.SectorId,
                    $"player {player.Id.Value} gang {slot}: the original has sector {recorded.Gang(record, "sector")}, the rebuild {gang.SectorId}");
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

        // RULE-EVENT-001, RULE-EVENT-002: each player's Last Turn reports of the last resolution, in
        // the order they were recorded, as the FMT-STATE-006 records the original holds. The runs
        // hold the reports of RULE-EVENT-003 (elimination), RULE-EVENT-004 (Crackdown),
        // RULE-EVENT-006 (site), RULE-EVENT-007 (Research), RULE-EVENT-008 and RULE-EVENT-014
        // (cash), RULE-EVENT-009 (hire cash), RULE-EVENT-010 (full sector), RULE-EVENT-012 and
        // RULE-EVENT-013 (Control). A Crackdown report goes to every player that had a gang in the
        // sector when resolution began (RULE-POLICE-004): EXP-TURN-023 and EXP-TURN-024 report one to
        // players that raised no Chaos there, and EXP-TURN-024 to one with no gang left there at the end.
        if (recorded.HasTerm("last_turn_report_count", 0))
        {
            var eventsBySequence = new Dictionary<long, GameEvent>();
            foreach (var gameEvent in match.Events) eventsBySequence[gameEvent.Sequence] = gameEvent;
            foreach (var player in match.Players)
            {
                var slot = player.Id.Value;
                var reports = LastTurnEventProjection.For(match, player.Id)
                    .Select(notification => LastTurnEventPresentation.Record(match, notification,
                        notification.RelatedEventSequence is { } sequence
                            ? eventsBySequence.GetValueOrDefault(sequence)
                            : null))
                    .ToArray();
                // DEV-AI-002: a computer player's Equip it cannot pay for gives no command in the
                // rebuild, so the cash report the original records when it fails has no counterpart.
                var expected = Enumerable.Range(0, recorded.Term("last_turn_report_count", slot))
                    .Select(index => recorded.Report(slot, index))
                    .Where(report => player.Setup.Controller == PlayerController.Human
                        || report is not { Type: LastTurnReportRecord.CashShort, Arg1: LastTurnReportRecord.CashShortEquip })
                    .ToArray();
                Assert.True(expected.Length == reports.Length,
                    $"player {slot}: the original holds {expected.Length} reports, the rebuild {reports.Length}: "
                    + $"original [{string.Join("; ", expected)}], rebuild [{string.Join("; ", reports)}]");
                for (var index = 0; index < expected.Length; index++)
                    Assert.True(expected[index] == reports[index],
                        $"player {slot} report {index}: the original holds {expected[index]}, the rebuild {reports[index]}");
            }
        }

        // The running totals the financial panel and the endgame awards read, and the hire roles
        // the computer players' planning keeps (RULE-AI-010). Damage Inflicted is RULE-COMBAT-003.
        // The attitudes compared above follow every Control takeover (RULE-AI-017) in EXP-TURN-011,
        // EXP-TURN-017 and EXP-TURN-018. Every Attack lowers the defender's attitude by the larger of
        // its reaction and the opening damage (RULE-AI-016). In EXP-TURN-017, EXP-TURN-018, EXP-TURN-020,
        // EXP-TURN-022 and EXP-TURN-025 a player is eliminated (RULE-OBJECTIVE-003) before the end
        // evaluation stores the scores (RULE-TURN-006).
        if (recorded.HasTerm("cash_spent", 0))
            foreach (var player in match.Players)
            {
                var slot = player.Id.Value;
                (string Term, long Value)[] totals =
                [
                    ("cash_earned", player.Statistics.CashEarned), ("cash_spent", player.Statistics.CashSpent),
                    ("damage_inflicted", player.Statistics.DamageInflicted),
                    ("casualties", player.Statistics.Casualties), ("overthrow_count", player.Statistics.Overthrows),
                    ("hide_count", player.Statistics.TimesHidden),
                    ("hire_role", match.AiPlanning.CurrentHireRole(player.Id)),
                    ("previous_hire_role", match.AiPlanning.PreviousHireRole(player.Id)),
                ];
                // The original holds -1 in a human player's hire_role and the rebuild 0. A player's
                // first planning pass writes 0 there before anything reads it (FND-AI-042), so the
                // difference is one of representation; any other value is still compared.
                foreach (var (term, value) in totals)
                {
                    var original = recorded.Term(term, slot);
                    var expected = term == "hire_role" && original == -1 && player.Setup.Controller == PlayerController.Human
                        ? 0
                        : original;
                    Assert.True(expected == value,
                        $"{term} of player {slot}: the original holds {original}, the rebuild {value}");
                }
            }

        // RULE-OBJECTIVE-001, RULE-OBJECTIVE-002: the scores and standings stored when the last
        // turn ended, which the Player Rankings panel shows. An eliminated player scores -32000 and
        // has standing 0xFF. In EXP-TURN-028's Greed the stored scores are the cash from before the
        // upkeep of the turn being planned.
        if (recorded.HasTerm("scenario_score", 0))
        {
            var ranking = PlayerRankingPresentation.Project(match);
            foreach (var player in match.Players)
            {
                var slot = player.Id.Value;
                var standing = ranking.SingleOrDefault(entry => entry.Player == player.Id)?.Standing ?? 0xFF;
                Assert.True(recorded.Term("scenario_score", slot) == player.ScenarioScore,
                    $"scenario_score of player {slot}: the original holds {recorded.Term("scenario_score", slot)}, the rebuild {player.ScenarioScore}");
                Assert.True(recorded.Term("scenario_standing", slot) == standing,
                    $"scenario_standing of player {slot}: the original holds {recorded.Term("scenario_standing", slot)}, the rebuild {standing}");
            }
        }

        // FND-SETUP-018: the turn limit the computer players read.
        Assert.Equal(recorded.Term("turn_limit", 0), ScenarioCatalog.TurnLimit(match.Setup.Scenario, match.Setup.Duration));

        // RULE-OBJECTIVE-001, RULE-OBJECTIVE-004, RULE-AWARDS-001: a run that ends the match stops at
        // the endgame, where each player's first three award entries hold the categories won in the
        // builder's order (0 Fist, 1 Skull, 2 Big Fat Chicken, 3 Dollar Sign, 4 Safe), then -1.
        if (recorded.HasTerm("match_over", 0))
        {
            Assert.NotNull(match.Outcome);
            // The match ends with the resolution of the last recorded Done.
            Assert.Equal(recorded.DoneCount, match.Outcome.Turn);
            // The original's elapsed-turn count stays one below the deciding turn at the final view
            // and after the awards. The rebuild's coordinator has already moved to the next turn, so
            // the count is compared with the outcome turn.
            Assert.Equal(recorded.Term("elapsed_turns", 0), match.Outcome.Turn - 1);
            if (EndpointsBeforeAwards.Contains(experiment))
            {
                // A pre-awards fixture holding award rows would leave them uncompared.
                Assert.False(recorded.HasTerm("player_awards", 0), "a run stopped before the awards holds no award rows");
                return;
            }
            Assert.True(recorded.HasTerm("player_awards", 0), "a run that ends the match after the awards holds their rows");
            EndgameAward[] order =
                [EndgameAward.Fist, EndgameAward.Skull, EndgameAward.BigFatChicken, EndgameAward.DollarSign, EndgameAward.Safe];
            foreach (var player in match.Players)
            {
                var slot = player.Id.Value;
                var won = order.Select((award, category) => (award, category))
                    .Where(entry => match.Outcome!.Awards.Any(result =>
                        result.Award == entry.award && result.Recipients.Contains(player.Id)))
                    .Select(entry => entry.category)
                    .Concat(Enumerable.Repeat(-1, 3)).Take(3).ToArray();
                var held = Enumerable.Range(0, 3).Select(entry => recorded.Term("player_awards", slot * 5 + entry)).ToArray();
                Assert.True(held.SequenceEqual(won),
                    $"awards of player {slot}: the original holds [{string.Join(",", held)}], the rebuild [{string.Join(",", won)}]");
            }
            return;
        }

        Assert.Equal(recorded.Term("elapsed_turns", 0), match.Coordinator.Turn - 1);
        if (recorded.Term("controller", human.Value) == 0) Assert.Equal(human, match.Coordinator.ActivePlayer);
        else Assert.Equal(PlayerStatus.Eliminated, match.FindPlayer(human)!.Status);
    }

    public static TheoryData<string, int> TimerRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].Timers.Count > 0) data.Add(experiment, run);
        return data;
    }

    // RULE-TIMER-001, RULE-TIMER-002, RULE-TIMER-003: the probe chooses a planning time limit and
    // lets turns run out without a Done press. It records the limit the match entry stored, each
    // redraw of the bar with the elapsed milliseconds, width and warning slot, and the elapsed
    // milliseconds of the last time-limit test that let planning go on and of the one that ended
    // it. The rebuild stores the same limit for the choice, draws the same width and plays the same
    // warning for each elapsed time, and lets the turn go on and end at the same elapsed times. The
    // original's redraws came between five and seven presentation ticks apart, the clock's jitter
    // around the six ticks the rebuild waits. The expiry ends the turn as a Done press does, which
    // the replay of every run checks.
    [Theory]
    [MemberData(nameof(TimerRuns))]
    public void ThePlanningClockMatchesTheOriginals(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var limit = PlanningTimerPolicy.LimitMilliseconds((PlanningTimeLimit)recorded.PlanningLimitChoice);
        var redrawInterval = (int)(PresentationClock.Period * PlanningTimerPolicy.RefreshCountdown).TotalMilliseconds;
        foreach (var timer in recorded.Timers)
        {
            Assert.Equal<int?>(timer.LimitMs, limit);
            foreach (var (elapsed, width, slot) in timer.Bars)
            {
                Assert.True(PlanningTimerPolicy.RawBarWidth(timer.LimitMs, elapsed) == width,
                    $"turn {timer.Turn}, {elapsed} ms: the original drew width {width}");
                Assert.True((PlanningTimerPolicy.WarningSoundSlot(timer.LimitMs - elapsed) ?? 0) == slot,
                    $"turn {timer.Turn}, {elapsed} ms: the original played slot {slot}");
            }
            Assert.Equal(0, timer.Bars[0].Elapsed);
            for (var bar = 2; bar < timer.Bars.Count; bar++)
            {
                var interval = timer.Bars[bar].Elapsed - timer.Bars[bar - 1].Elapsed;
                Assert.InRange(interval, redrawInterval - PresentationClock.PeriodMilliseconds,
                    redrawInterval + PresentationClock.PeriodMilliseconds);
            }
            Assert.False(PlanningTimerPolicy.Expired(timer.LimitMs, timer.LastUnexpiredMs));
            Assert.True(PlanningTimerPolicy.Expired(timer.LimitMs, timer.ExpiredMs));
        }
    }

    // RULE-OBJECTIVE-005: -2 stops before the card at the human's own slot; -1
    // has dismissed it and lets the remaining slots and resolution finish.
    private static void AdvanceToRecordedEndpoint(MatchReplayRecorder recorder, PlayerId human, int controller)
    {
        var match = recorder.State;
        while (match.Outcome is null
               && !(match.Coordinator.Phase == TurnPhase.Command && match.Coordinator.ActivePlayer == human))
            HeadlessMatchRunner.Advance(recorder);
        if (match.Outcome is not null || IsActive(match, human) || controller != -1) return;

        do
        {
            HeadlessMatchRunner.Advance(recorder);
        } while (match.Outcome is null && match.Coordinator.Phase != TurnPhase.Upkeep);
    }

    private static void AssertReplayEndpoint(MatchState match, PlayerId human, int controller)
    {
        if (controller != 0)
            Assert.Equal(PlayerStatus.Eliminated, match.FindPlayer(human)!.Status);
        else if (match.Outcome is null)
        {
            Assert.Equal(TurnPhase.Command, match.Coordinator.Phase);
            Assert.Equal(human, match.Coordinator.ActivePlayer);
        }
        else
        {
            Assert.Equal(PlayerStatus.Active, match.FindPlayer(human)!.Status);
            Assert.Equal(TurnPhase.Upkeep, match.Coordinator.Phase);
        }
    }
    // Synthetic harness cases, not recordings from the original: RULE-OBJECTIVE-005.
    [Theory]
    [InlineData(0, -2)]
    [InlineData(2, -2)]
    [InlineData(2, -1)]
    [InlineData(5, -1)]
    public void EliminatedHumanEndpointIncludesTheCorrectComputerPlanning(int slot, int controller)
    {
        MatchState Create()
        {
            var state = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
                ScenarioId.Power, GameDuration.OneYear, 12345,
                [new MatchPlayerSetup(new PlayerId(slot), "PROBE", PlayerController.Human, 0)],
                AiDifficulty.Goon, allowSparsePlayerIds: true));
            state.FindPlayer(new PlayerId(slot))!.Status = PlayerStatus.Eliminated;
            foreach (var gang in state.FindPlayer(new PlayerId(slot))!.Gangs) gang.Force = 0;
            state.FinishUpkeep();
            return state;
        }

        var expected = Create();
        var expectedRecorder = new MatchReplayRecorder(expected);
        for (var earlier = 0; earlier < slot; earlier++) HeadlessMatchRunner.Advance(expectedRecorder);
        if (controller == -1)
        {
            for (var remaining = slot; remaining < 6; remaining++) HeadlessMatchRunner.Advance(expectedRecorder);
            while (expected.Coordinator.Phase != TurnPhase.Upkeep) HeadlessMatchRunner.Advance(expectedRecorder);
        }

        var actual = Create();
        AdvanceToRecordedEndpoint(new MatchReplayRecorder(actual), new PlayerId(slot), controller);
        AssertReplayEndpoint(actual, new PlayerId(slot), controller);
        Assert.Equal(expected.Random.ConsumptionCount, actual.Random.ConsumptionCount);
        Assert.Equal(expected.Random.State, actual.Random.State);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(expected), MatchStateHasher.ComputeFingerprint(actual));
        Assert.Equal(controller == -2 ? TurnPhase.Command : TurnPhase.Upkeep, actual.Coordinator.Phase);
        if (controller == -2) Assert.Equal(new PlayerId(slot), actual.Coordinator.ActivePlayer);
        else Assert.Equal(MatchEndReason.NoHumansLeft, actual.Outcome!.Reason);
    }

    // RULE-OBJECTIVE-001: a surviving human's decided match has no next planning entry.
    [Fact]
    public void SurvivingHumanOutcomeDoesNotRequireAnActivePlanningSlot()
    {
        var human = new PlayerId(2);
        var match = OriginalMatchFactory.Create(BundledOriginalData.Load(), new MatchSetup(
            ScenarioId.Power, GameDuration.OneYear, 12345,
            [new MatchPlayerSetup(human, "PROBE", PlayerController.Human, 0)],
            AiDifficulty.Goon, allowSparsePlayerIds: true));
        foreach (var player in match.Players.Where(player => player.Id != human))
        {
            player.Status = PlayerStatus.Eliminated;
            foreach (var gang in player.Gangs) gang.Force = 0;
        }
        var recorder = new MatchReplayRecorder(match);
        while (match.Coordinator.Phase != TurnPhase.PlayerElimination)
        {
            if (match.Coordinator.Phase == TurnPhase.Command)
                recorder.FinishCommand(match.Coordinator.ActivePlayer!.Value);
            else HeadlessMatchRunner.Advance(recorder);
        }
        AdvanceToRecordedEndpoint(recorder, human, 0);
        Assert.NotNull(match.Outcome);
        Assert.Null(match.Coordinator.ActivePlayer);
        AssertReplayEndpoint(match, human, 0);
    }
    private static bool IsActive(MatchState match, PlayerId player) =>
        match.FindPlayer(player)!.Status == PlayerStatus.Active;

    private static MatchState StartMatch(RecordedRun recorded, out int donePresses)
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
        var initialRecorder = new MatchReplayRecorder(match);
        AdvanceToRecordedEndpoint(initialRecorder, recorded.Humans[0], 0);
        if (recorded.Humans.Count == 1) match.PrepareHireOffers(recorded.Humans[0]);

        // Each Done ends the human's planning with no orders. The computer players then plan and
        // the turn resolves as in a headless match, up to the human's next planning entry.
        // A planning write changes the state outside the recorder, as the probe changes the
        // original's memory outside the game, so such a run's journal is not verified.
        var recorder = recorded.Planning.Count == 0
            ? new MatchReplayRecorder(match)
            : MatchReplayRecorder.Unverified(match);
        var human = recorded.Humans[0];
        donePresses = 0;
        for (var turn = 0; turn < recorded.DoneCount; turn++)
        {
            // DEV-EQUIP-001: the rebuild resolves Equip and Sell in the order they are submitted.
            // Every recording lists a turn's orders in roster order, the original's scan order.
            foreach (var order in recorded.Orders.Where(order => order.Turn == turn + 1))
                Submit(recorder, match, human, order);
            // RULE-HIRE-003: the probe writes the offer slot's hire order as the hire screen does;
            // the rebuild queues the gang that slot offers.
            foreach (var hire in recorded.Hires.Where(hire => hire.Turn == turn + 1))
            {
                var offered = match.Players[human.Value].HireOfferSlots[hire.OfferSlot].GangDefinitionId;
                Assert.NotNull(offered);
                var result = recorder.QueueHire(human, offered.Value, hire.Sector);
                Assert.True(result.Accepted, $"turn {hire.Turn}: the rebuild refused the hire: {result}");
            }
            // FMT-STATE-007, RULE-AI-023, RULE-AI-027: the probe writes a computer player's family or
            // raider flag straight into the original's memory, for branches no local match reaches.
            foreach (var write in recorded.Planning.Where(write => write.Turn == turn + 1))
                if (write.Raider) match.AiPlanning.SetRaiderMode(new PlayerId(write.Player));
                else match.AiPlanning.SetFamily(new PlayerId(write.Player), write.Slot, write.Family);
            recorder.FinishCommand(human);
            donePresses++;
            AdvanceToRecordedEndpoint(recorder, human, recorded.Term("controller", human.Value));
            if (!IsActive(match, human) || match.Outcome is not null) break;

            recorder.PrepareHireOffers(human);
        }

        return match;
    }

    // The probe writes an order straight into the human's gang record (FMT-STATE-001) as the order
    // screens do (RULE-TURN-005); the rebuild takes the same order as a command. A slot is the
    // gang's roster slot (FMT-STATE-001), the index of its player's gang list.
    private static void Submit(MatchReplayRecorder recorder, MatchState match, PlayerId human, RecordedOrder order)
    {
        var gang = match.Players[human.Value].Gangs[order.Slot];
        var action = (GangAction)order.Action;
        var command = action switch
        {
            GangAction.Move => new GameCommand(human, gang.Id, action, CommandTarget.Sector(order.Target), order.Repeat),
            GangAction.Research or GangAction.Equip =>
                new GameCommand(human, gang.Id, action, CommandTarget.Item(order.Target), order.Repeat),
            // target_2 of an Attack is the target's roster slot (FMT-STATE-001), the index of its
            // player's gang list.
            GangAction.Attack => new GameCommand(human, gang.Id, action,
                CommandTarget.Gang(match.Players[order.Target].Gangs[order.Target2].Id), order.Repeat),
            // target of a Sell is the item mask, weapon 1, armor 2 and misc 4 (FMT-STATE-001); the
            // rebuild takes the items in that slot order (RULE-SELL-001).
            GangAction.Sell => SellCommand(human, gang, order.Target, order.Repeat),
            // target of a Give is the item mask and target_2 the recipient's roster slot
            // (FMT-STATE-001, RULE-GIVE-001).
            GangAction.Give => GiveCommand(human, gang, match.Players[human.Value].Gangs[order.Target2], order.Target),
            GangAction.Bribe or GangAction.Chaos or GangAction.Control or GangAction.Heal
                or GangAction.Hide or GangAction.Snitch or GangAction.Terminate =>
                new GameCommand(human, gang.Id, action, CommandTarget.None, order.Repeat),
            _ => throw new NotSupportedException($"No recorded order of action {action} is replayed yet."),
        };
        var result = recorder.Submit(command);
        Assert.True(result.Accepted, $"turn {order.Turn}: the rebuild refused {action}: {result}");
    }

    private sealed record RecordedOrder(int Turn, int Slot, int Action, int Target, int Target2, bool Repeat)
    {
        // "turn 3: gang slot 0 action 13 target 0 target_2 0 repeat 0", as the probe writes it.
        public static RecordedOrder Parse(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (-?\d+): gang slot (-?\d+) action (-?\d+) target (-?\d+) target_2 (-?\d+) repeat ([01])$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2], numbers[3], numbers[4], numbers[5] != 0);
        }
    }

    private sealed record RecordedPlanning(int Turn, int Player, int Slot, int Family, bool Raider)
    {
        // "turn 2: player 1 gang slot 0 family 4" or "turn 1: player 3 raider_mode 1", as the
        // probe writes them.
        public static RecordedPlanning Parse(string value)
        {
            var raider = System.Text.RegularExpressions.Regex.Match(value, @"^turn (\d+): player ([1-5]) raider_mode 1$");
            if (raider.Success)
                return new(int.Parse(raider.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                    int.Parse(raider.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture), 0, 0, true);
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (\d+): player ([1-5]) gang slot (\d+) family (\d+)$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2], numbers[3], false);
        }
    }

    // The planning clock of a turn that ran out: the limit, each redraw of the bar as elapsed
    // milliseconds, width and warning slot (0 for none), and the elapsed milliseconds of the last
    // time-limit test that let planning go on and of the one that ended it (RULE-TIMER-002).
    private sealed record RecordedTimer(
        int Turn, int LimitMs, IReadOnlyList<(int Elapsed, int Width, int Slot)> Bars, int LastUnexpiredMs, int ExpiredMs);

    private sealed record RecordedHire(int Turn, int OfferSlot, int Sector)
    {
        // "turn 1: offer slot 0 sector 12", as the probe writes it.
        public static RecordedHire Parse(string value)
        {
            var match = System.Text.RegularExpressions.Regex.Match(value,
                @"^turn (\d+): offer slot ([0-2]) sector (\d+)$");
            Assert.True(match.Success, value);
            var numbers = match.Groups.Values.Skip(1)
                .Select(group => int.Parse(group.Value, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            return new(numbers[0], numbers[1], numbers[2]);
        }
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

    private static GameCommand SellCommand(PlayerId human, MatchGangState gang, int mask, bool repeat)
    {
        short?[] slots = [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
        var items = Enumerable.Range(0, slots.Length)
            .Where(slot => (mask & (1 << slot)) != 0)
            .Select(slot => (CommandTarget?)CommandTarget.Item(slots[slot] ?? throw new InvalidOperationException(
                $"The Sell mask {mask} selects an empty slot.")))
            .ToArray();
        if (items.Length == 0)
            throw new InvalidOperationException($"The Sell mask {mask} selects no slot.");
        // The repeat flag goes through as recorded, so the rebuild's validation judges it.
        return new GameCommand(human, gang.Id, GangAction.Sell, items[0]!.Value, repeat,
            items.ElementAtOrDefault(1), items.ElementAtOrDefault(2));
    }

    private static GameCommand GiveCommand(PlayerId human, MatchGangState gang, MatchGangState recipient, int mask)
    {
        short?[] slots = [gang.WeaponItemId, gang.ArmorItemId, gang.MiscellaneousItemId];
        var items = Enumerable.Range(0, slots.Length)
            .Where(slot => (mask & (1 << slot)) != 0)
            .Select(slot => (CommandTarget?)CommandTarget.Item(slots[slot] ?? throw new InvalidOperationException(
                $"The Give mask {mask} selects an empty slot.")))
            .ToArray();
        return new GameCommand(human, gang.Id, GangAction.Give, CommandTarget.Gang(recipient.Id), false,
            items[0], items.ElementAtOrDefault(1), items.ElementAtOrDefault(2));
    }

    private static RecordedRun Run(string experiment, int run) => Recorded.Value[experiment][run];

    /// <summary>The rebuild's match after replaying a recorded run to its endpoint.</summary>
    internal static MatchState ReplayedMatch(string experiment, int run) => StartMatch(Run(experiment, run), out _);
    internal static int RecordedTerm(string experiment, int run, string term, int index) =>
        Run(experiment, run).Term(term, index);

    private static RecordedRun[] LoadRuns(string experiment)
    {
        using var fixture = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "spec", "experiments", $"{experiment}.json")));
        var inputs = fixture.RootElement.GetProperty("inputs");
        return fixture.RootElement.GetProperty("runs").EnumerateArray()
            .Select(run => new RecordedRun(run, inputs)).ToArray();
    }

    private sealed class RecordedRun
    {
        private readonly Dictionary<(string, int), int> _terms = [];
        private readonly Dictionary<(string, int, string), int> _fields = [];

        public RecordedRun(JsonElement run, JsonElement inputs)
        {
            Orders = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "order")
                .Select(input => RecordedOrder.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Hires = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "hire")
                .Select(input => RecordedHire.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Planning = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "planning")
                .Select(input => RecordedPlanning.Parse(input.GetProperty("value").GetString()!))
                .ToArray();
            Seed = run.GetProperty("rng_state").GetInt32();
            // "planning_limit_choice 1", as the probe writes a setup choice.
            PlanningLimitChoice = inputs.EnumerateArray()
                .Where(input => input.GetProperty("name").GetString() == "setup")
                .Select(input => input.GetProperty("value").GetString()!)
                .Where(value => value.StartsWith("planning_limit_choice ", StringComparison.Ordinal))
                .Select(value => int.Parse(value["planning_limit_choice ".Length..], System.Globalization.CultureInfo.InvariantCulture))
                .FirstOrDefault();
            Timers = run.TryGetProperty("timers", out var timers)
                ? timers.EnumerateArray().Select(timer => new RecordedTimer(
                    timer.GetProperty("turn").GetInt32(), timer.GetProperty("limit_ms").GetInt32(),
                    timer.GetProperty("bars").EnumerateArray()
                        .Select(bar => (bar[0].GetInt32(), bar[1].GetInt32(), bar[2].GetInt32())).ToArray(),
                    timer.GetProperty("last_unexpired_ms").GetInt32(), timer.GetProperty("expired_ms").GetInt32())).ToArray()
                : [];
            DoneCount = run.TryGetProperty("done_at_roll", out var done) ? done.GetArrayLength() : 0;
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
        public int DoneCount { get; }
        public int PlanningLimitChoice { get; }
        public IReadOnlyList<RecordedTimer> Timers { get; }
        public IReadOnlyList<RecordedOrder> Orders { get; }
        public IReadOnlyList<RecordedHire> Hires { get; }
        public IReadOnlyList<RecordedPlanning> Planning { get; }

        // controller: 0 for a human at this computer (FND-SETUP-002), -2 for one eliminated who has
        // not yet seen the card and -1 for one who has (RULE-OBJECTIVE-005). Setup fills every
        // empty slot with a computer player (FND-SETUP-002), so a -1 here is always a retired human.
        public IReadOnlyList<PlayerId> Humans => Enumerable.Range(0, 6)
            .Where(slot => Term("controller", slot) is 0 or -1 or -2).Select(slot => new PlayerId(slot)).ToArray();
        public IReadOnlyList<(string Call, int Bound, int Result)> Rolls { get; }

        public int Term(string term, int index) => _terms[(term, index)];

        public int Sector(int sector, string field) => _fields[("FMT-STATE-002", sector, field)];

        public int Gang(int record, string field) => _fields[("FMT-STATE-001", record, field)];

        public bool HasTerm(string term, int index) => _terms.ContainsKey((term, index));

        /// <summary>FMT-STATE-006: report <paramref name="index"/> of a player's Last Turn reports.</summary>
        public LastTurnReportRecord Report(int player, int index)
        {
            var record = player * 32 + index;
            return new(_fields[("FMT-STATE-006", record, "report_type")], _fields[("FMT-STATE-006", record, "arg1")],
                _fields[("FMT-STATE-006", record, "arg2")], _fields[("FMT-STATE-006", record, "arg3")]);
        }

        public IReadOnlyList<int> GangRecords(int player) => _fields.Keys
            .Where(key => key.Item1 == "FMT-STATE-001" && key.Item3 == "player" && key.Item2 / 81 == player)
            .Select(key => key.Item2).Order().ToArray();
    }
}
