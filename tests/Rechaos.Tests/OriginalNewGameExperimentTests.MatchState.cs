using Rechaos.Core.GameModel;
using Rechaos.Game;
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
/// six-month Greed to its end and stops at the final city view, before the awards are given. In
/// EXP-TURN-043 a computer player's Moves would put seven of its gangs in one sector, and the Move
/// repair keeps the mover in its own sector (RULE-MOVE-002).
/// EXP-TURN-048 to EXP-TURN-051 also hold the computer players' planning state, which the replay
/// compares byte for byte (FMT-STATE-007, FND-AI-074): Greed, whose Terminate keeps the targets of
/// the action it replaces, Kill 'Em All with families 5 and 7, and a family-7 Equip that leaves the
/// focus its handler compares at the next pass (RULE-AI-004, RULE-AI-022, RULE-AI-024, RULE-AI-026).
/// They hold the combat records and result rows too, which the replay rebuilds from the last
/// resolution's attack and police events; in EXP-TURN-051 the human's gang attacks and the police
/// kill a gang (FMT-STATE-003, FMT-STATE-008, RULE-COMBAT-002, RULE-POLICE-001). EXP-TURN-053 plays
/// forty turns of Eliminate, where family-12 gangs keep their focus and their Moves' destinations
/// and carry those into a later family (RULE-AI-030, FND-AI-075). In EXP-TURN-054 a family-3 gang
/// that plans Influence after None stores -1 in the focus (RULE-AI-022, FND-AI-076). In
/// EXP-TURN-055 and EXP-TURN-056 a family-2 gang plans Equip in a human's sector, and the late
/// Control gates test the sector numbered like the item and keep the Equip (RULE-AI-021,
/// BUG-AI-008).
/// EXP-TURN-057 compares the planning state of thirty turns of Eliminate at Crime Lord, with
/// families 0, 3, 7 and 10 to 12 (RULE-AI-019, RULE-AI-022, RULE-AI-026, RULE-AI-028 to
/// RULE-AI-030). EXP-TURN-058 plays Big Man, with families 13 and 14, to its end and compares the
/// awards and the endgame rows (RULE-AI-031, RULE-OBJECTIVE-004, RULE-AWARDS-001, RULE-AWARDS-002).
/// EXP-TURN-061 to EXP-TURN-063 give the human's gang a standing Chaos order from turn 1 in Power,
/// Greed and Big 40, which pays Chaos in a player's own sectors and, halved, outside them and
/// brings on Crackdowns (RULE-CHAOS-001, RULE-CHAOS-002).
/// In EXP-TURN-064 and EXP-TURN-066 the human's gangs keep doing Chaos in a sector under police
/// presence, which cracks down again and pays (RULE-CHAOS-001, RULE-CHAOS-002). In EXP-TURN-065 the
/// human's recurring Research, Influence and Control end once done (RULE-TURN-004).
/// EXP-TURN-067 drops a recurring Heal at Force 10 and the recurring Chaos of gangs killed in
/// Combat (RULE-TURN-004), and in EXP-TURN-068 a Heal with a pool below 1 rolls nothing
/// (RULE-HEAL-001). In EXP-TURN-069 an Influence with a pool below 1 rolls nothing
/// (RULE-INFLUENCE-001). EXP-TURN-070 and EXP-TURN-071 drop a recurring Control in a sector
/// under police presence (RULE-TURN-004), and EXP-TURN-072 prices an item at full Cost beside
/// another player's completed Factory (RULE-EQUIP-003).
/// EXP-TURN-073 to EXP-TURN-077 write a family into the computer gangs' planning records and reach
/// branches no match had reached: family 7 out of research (RULE-AI-026), family-4 attack draws
/// (RULE-AI-023), the family-10 Heal (RULE-AI-028), an accepted family-3 attack
/// (RULE-AI-022) and a family-6 guard target already covered (RULE-AI-025).
/// EXP-TURN-078 to EXP-TURN-081 do the same in Siege, Big Man and Armageddon and reach the further family-6
/// draws (RULE-AI-025), the family-3 Heal (RULE-AI-022), family-4 attacks after Chaos or Equip
/// (RULE-AI-023), the family-12 Detect Equip and draw at weight 1 (RULE-AI-030) and the family-0
/// Move after a Snitch (RULE-AI-019).
/// EXP-TURN-082 reaches the family-4 Control after two Moves (RULE-AI-023), EXP-TURN-083 the
/// family-7 draw from human gangs only (RULE-AI-026), EXP-TURN-084 a family-14 draw from an empty
/// pool (RULE-AI-031), and EXP-TURN-085 and EXP-TURN-086 the forced hunter hire roles
/// (RULE-AI-010). EXP-TURN-083 is a known divergence under DEV-AI-002.
/// EXP-TURN-087, EXP-TURN-089 and EXP-TURN-091 reach refused family-4, family-5 and family-0
/// draws at weight 10 (RULE-AI-023, RULE-AI-024, RULE-AI-019, BUG-AI-007), and EXP-TURN-088 a
/// family-7 draw that gives no Attack (RULE-AI-026).
/// EXP-TURN-090 records a computer hire into a sector the player neither controls nor holds a
/// gang in (RULE-HIRE-001, DEV-AI-008), and EXP-TURN-093 and EXP-TURN-094 the family-5 draw from
/// human gangs only (RULE-AI-024). EXP-UI-001 stops at the first planning entry of a Greed match,
/// as EXP-SETUP-001 does, and holds a capture of the screen there (ScreenCaptureTests). EXP-UI-003
/// takes the same captures with the probe's 32-bit white key.
/// EXP-EQUIP-001 to EXP-EQUIP-003 replay EXP-TURN-030, EXP-TURN-071 and EXP-TURN-026 and record the
/// item lists the Equip screen offers each of the human's gangs at the end (RULE-EQUIP-004).
/// EXP-ATTACK-001 to EXP-ATTACK-003 replay EXP-TURN-072, EXP-TURN-093 and EXP-TURN-026 and record
/// the targets the Attack picker offers each of them among each opponent's gangs (RULE-ATTACK-002).
/// EXP-SEARCH-001 and EXP-SEARCH-002 press the Search panel's controls at that entry, with the human
/// in slot 0 and in slot 2, and record the filter table after each press (RULE-SEARCH-001).
/// EXP-HIRE-001 and EXP-HIRE-002 drag offers and press Reject on the Hire dock at the endpoints of
/// EXP-UI-001 and EXP-TURN-071 and record hire_orders after each step (RULE-HIRE-003).
/// EXP-TURN-095 gives orders through the gang cards' and the group order strip's popup menus at
/// the endpoint of EXP-TURN-071 and records the menus and the orders after each step
/// (RULE-TURN-005). EXP-UI-004 and EXP-UI-005 repeat EXP-HIRE-001 and EXP-TURN-071 with hire steps
/// and a Search close and log every gang-status marker the original draws (RULE-UI-006).
/// EXP-TURN-096 presses each Overlord portrait of a sector view at the endpoint of EXP-TURN-026
/// and records whose gangs the cards list after each press (RULE-UI-010).
/// EXP-TURN-097 to EXP-TURN-100 write the human's cash before every Done press, hire a gang a
/// turn and order Moves that the Move repair sends back and then gives a random neighbour from a
/// corner of the city (RULE-MOVE-002, RULE-AI-007), until a hire with 80 gangs is refused and
/// reported (RULE-HIRE-001, RULE-EVENT-011).
/// EXP-TURN-101 writes families 13 and 14 into computer gangs in Greed, where they move to the
/// planned target, sector 0, with no selector call (RULE-AI-031).
/// EXP-UI-032 and EXP-UI-034 play hot seat in Eliminate with a write that takes a human's Right
/// Hands out of the match, which eliminates that human (RULE-TURN-006, RULE-OBJECTIVE-005).
/// EXP-UI-035 plays timed hot-seat turns, one of them run out, and EXP-TURN-102 opens the menu bar
/// in timed turns (RULE-TIMER-002, RULE-TIMER-003).
/// In EXP-TURN-103 a Bribe every turn takes a base Tolerance to 41, where it stays while the later
/// gangs of the phase act, and the clamp lowers it to 40 (RULE-TOLERANCE-002, RULE-TURN-003). In
/// EXP-TURN-104 a Research gang acts after its sector's site is completed earlier in the same
/// phase and rolls without the site's Research (RULE-RESEARCH-001, RULE-TURN-003). EXP-TURN-105
/// writes families 1, 5, 6 and 12 into four planning records before the closing turns of Greed,
/// whose handlers then plan Terminate and flag the records (FMT-STATE-007, FND-AI-042).
/// EXP-TURN-110 writes Force 10 into a gang after its Heal order, and the Heal rolls its pool and
/// leaves the Force at 10 (RULE-HEAL-001). EXP-TURN-111 and EXP-TURN-114 write a base Tolerance
/// that one Bribe or one Snitch takes past the signed byte, which wraps and is then clamped
/// (RULE-BRIBE-001, RULE-SNITCH-001, RULE-TOLERANCE-002). EXP-TURN-115 holds a base Tolerance at
/// 40 with a Bribe every turn, and each step takes it to 39 first (RULE-TOLERANCE-001).
/// EXP-TURN-117 nets a Snitch and a Bribe out in one sector and clamps two sectors no gang acted
/// in (RULE-TOLERANCE-002). EXP-TURN-116 keeps the island modifier's Crackdowns of 100 through a
/// countdown (RULE-POLICE-003), and EXP-SETUP-005 names two players with that modifier
/// (RULE-SETUP-005).
/// EXP-UI-030 drags players between setup cards and ends with the roster New Game opens with, so
/// the match it then starts is the one the seed alone gives (RULE-SETUP-009).
/// In EXP-TURN-109 a family-7 gang whose focus names the sector it stands in, its best research
/// sector, researches there although a Research site in it is unfinished (RULE-AI-026, FND-AI-078).
/// EXP-TURN-106 to EXP-TURN-108 play Power, Big 40 and Armageddon matches to the turn each one
/// ends, a computer player's win and a tie for the lead included (RULE-OBJECTIVE-002,
/// RULE-OBJECTIVE-004), and in EXP-TURN-107 a player gets more than 64 notifications in one
/// turn, whose Last Turn reports must all survive (RULE-EVENT-002). EXP-TURN-112 and EXP-TURN-113
/// play Siege for 96 turns and Kill 'Em All for 150 without either reaching its scenario's end
/// condition, and are held as known divergences late in each run.
/// Every computer player's pass starts from its sector weights and the hostility step
/// (RULE-AI-003). Its hires land in the sector the planner encodes (RULE-AI-012), and gangs of the
/// default family plan by their previous action (RULE-AI-019). Its upgrade choices test danger
/// around the gang's sector, the centre included, which EXP-TURN-017 needs (RULE-AI-005).
/// A slot refilled in the turn its gang died keeps the dead gang's family, which EXP-TURN-012 and
/// EXP-TURN-099 need (RULE-AI-001), and in EXP-TURN-039 a computer player meets the trigger of
/// BUG-AI-005 and keeps its families. Family-1 gangs buy armor while a weapon's cooldown runs
/// (EXP-TURN-018, EXP-TURN-049) and fail a weight-10 strength test in EXP-TURN-091 (RULE-AI-020).
/// In EXP-TURN-010's second run, EXP-TURN-039, EXP-TURN-043 and EXP-TURN-049's first run a family-7
/// gang outside its best research sector holds a focus equal to that sector and researches in
/// place, and in EXP-TURN-021 and EXP-TURN-049 one goes on from the site slot a rewritten Snitch
/// left as its previous target (RULE-AI-026, FND-AI-078).
/// </summary>
public sealed partial class OriginalNewGameExperimentTests
{
    // Runs that stop at the final city view, before the awards controller builds the award table
    // (FND-OBJECTIVE-004, FND-UI-041). Their fixtures hold no award rows; every other run that
    // ends the match has to.
    private static readonly HashSet<string> EndpointsBeforeAwards = ["EXP-TURN-041", "EXP-TURN-042"];

    // Runs that end after the awards but were recorded before the probe kept the endgame's rows
    // (FND-AWARDS-005). Every other such run has to hold endgame_rows, so a recording whose
    // renderer trace timed out fails instead of dropping out of the endgame comparison. A run
    // leaves this set when it is recorded again.
    private static readonly HashSet<string> EndgamesBeforeEndgameRows = ["EXP-TURN-037"];

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
        var (match, donePresses, rolls) = MatchingReplays.Get((experiment, run));

        var human = recorded.Humans[0];

        // The first roll that differs, named by the original's call instruction.
        for (var index = 0; index < Math.Min(rolls.Count, recorded.RollsAtDump); index++)
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

        Assert.Equal(recorded.RollsAtDump * 3L, match.Random.ConsumptionCount);
        var expectedState = new DeterministicRandom(recorded.Seed);
        for (var draw = 0; draw < recorded.RollsAtDump * 3; draw++) expectedState.NextRaw();
        Assert.Equal(expectedState.State, match.Random.State);

        foreach (var player in match.Players)
        {
            var slot = player.Id.Value;
            Assert.Equal(recorded.Term("portrait", slot), player.Setup.PortraitId);
            Assert.Equal(recorded.Term("cash", slot), player.Cash);
            Assert.Equal(recorded.Term("reaction", slot), match.AiStrategy.Reaction(player.Id));
            Assert.Equal(recorded.Term("difficulty_band", slot), (int)OriginalResolutionRules.Band(match, player.Id));
            Assert.Equal(recorded.Term("hire_force_modifier", slot) != 0, player.UsesMaximumHireForce);
            // FND-STATE-004, RULE-TURN-006: a player is out of the match once its player_active
            // byte is 0, whether the elimination check or a --retire write cleared it.
            if (recorded.HasTerm("player_active", slot))
                Assert.True(
                    (recorded.Term("player_active", slot) != 0) == (player.Status == PlayerStatus.Active),
                    $"player_active of player {slot}: the original holds {recorded.Term("player_active", slot)}, the rebuild {player.Status}");
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

        // RULE-MOVE-002: in EXP-TURN-043 a computer player's Moves would put seven of its gangs in
        // one sector, and the repair keeps the mover in its own sector; the gang sectors compared
        // here agree with that, and TheMoveRepairSendsTheMoverBack checks that the repair ran.
        foreach (var player in match.Players)
        {
            var gangs = player.Gangs.Where(gang => gang.IsActive).ToArray();
            var records = recorded.GangRecords(player.Id.Value);
            Assert.Equal(records.Count, gangs.Length);
            // A run that ends the match has no further turn start (RULE-TURN-004).
            var comparesRecurringOrders = recorded.Term("controller", player.Id.Value) == 0 && match.Outcome is null;
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
                // RULE-TURN-004: at a human's planning entry each gang's action is its recurring
                // order, or none once the turn start has cleared it; the rebuild keeps only the
                // recurring orders that carry on.
                if (comparesRecurringOrders)
                {
                    var carried = gang.QueuedCommand is { Command.Repeat: true } queued ? (int)queued.Command.Action : 0;
                    Assert.True(recorded.Gang(record, "repeat_action") == carried,
                        $"player {player.Id.Value} gang {slot}: the original has repeat_action {recorded.Gang(record, "repeat_action")}, the rebuild {carried}");
                    Assert.Equal(recorded.Gang(record, "repeat_action"), recorded.Gang(record, "action"));
                }
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

        if (recorded.HasTerm("ai_started", 0))
        {
            AssertPlanningStateMatches(recorded, match);
            // The fixtures that hold the planning state also hold the combat records and result rows.
            // Before the first Done no resolution has written them, and the original holds 0 in
            // every byte (EXP-UI-001).
            if (donePresses > 0) AssertLastCombatMatches(recorded, match);
            else Assert.All(recorded.Values("FMT-STATE-003"), value => Assert.Equal(0, value));
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
                // The original holds -1 in a human player's hire_role, and in a computer player's
                // until its first planning pass, and the rebuild 0. That pass writes 0 there before
                // anything reads it (FND-AI-042), so the difference is one of representation; any
                // other value is still compared.
                foreach (var (term, value) in totals)
                {
                    var original = recorded.Term(term, slot);
                    var expected = term == "hire_role" && original == -1
                        && (player.Setup.Controller == PlayerController.Human || !match.AiPlanning.HasPlanned(player.Id))
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
                // EndgameRuns would otherwise compare a drawing that has no recorded awards behind it.
                Assert.True(recorded.EndgameRows is null, "a run stopped before the awards holds no endgame_rows");
                return;
            }
            Assert.True(recorded.HasTerm("player_awards", 0), "a run that ends the match after the awards holds their rows");
            // TheEndgameListsThePlayersInTheOriginalsOrder compares these rows.
            Assert.True(EndgamesBeforeEndgameRows.Contains(experiment) == (recorded.EndgameRows is null),
                EndgamesBeforeEndgameRows.Contains(experiment)
                    ? "a run recorded before endgame_rows holds them, so it leaves EndgamesBeforeEndgameRows"
                    : "a run that ends the match after the awards holds endgame_rows");
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
}
