using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    // A replay that a test watches through hooks or with a deviation switched on. Any other test
    // takes its match from Replayed, which plays each distinct game once.
    private static MatchState StartMatch(
        RecordedRun recorded, out int donePresses, Action<MatchState, PlayerId, int>? beforeDone = null,
        bool computerMovesToNeighboursOnly = false, bool computerHiresWhereHumansCan = false,
        Action<MatchState, PlayerId, int>? atPlanningEntry = null, Action<MatchState>? afterDone = null) =>
        Play(ReplayInputs.Of(recorded), out donePresses, beforeDone, computerMovesToNeighboursOnly,
            computerHiresWhereHumansCan, atPlanningEntry, afterDone);

    // Reads nothing of the recording but the inputs, so the inputs decide the replay and can key
    // the cache of Replayed.
    private static MatchState Play(
        ReplayInputs inputs, out int donePresses, Action<MatchState, PlayerId, int>? beforeDone = null,
        bool computerMovesToNeighboursOnly = false, bool computerHiresWhereHumansCan = false,
        Action<MatchState, PlayerId, int>? atPlanningEntry = null, Action<MatchState>? afterDone = null)
    {
        var scenario = OriginalScenario(inputs.Scenario);
        var setup = new MatchSetup(
            scenario,
            ScenarioCatalog.Get(scenario).IsTimed ? Duration(inputs.TurnLimit) : GameDuration.OneYear,
            inputs.Seed,
            inputs.Humans.Select(seat => new MatchPlayerSetup(
                new PlayerId(seat.Slot), seat.Name, PlayerController.Human, seat.Portrait)).ToArray(),
            // DEV-AI-007 and DEV-AI-008 switched off unless a test asks for them, so the computer's
            // Moves and hires go where the original's do.
            new MatchDeviations(computerMovesToNeighboursOnly, computerHiresWhereHumansCan, AiPolicyMode.Original),
            (AiDifficulty)inputs.Mentality,
            allowSparsePlayerIds: true);
        var match = OriginalMatchFactory.Create(BundledOriginalData.Load(), setup);
        match.FinishUpkeep();
        // RULE-SETUP-008: with several local humans the planning phase waits on the Ready card
        // before it refills the offers, and the recording stops there.
        var initialRecorder = new MatchReplayRecorder(match);
        AdvanceToRecordedEndpoint(initialRecorder, new PlayerId(inputs.Humans[0].Slot), 0);
        if (inputs.Humans.Count == 1) match.PrepareHireOffers(new PlayerId(inputs.Humans[0].Slot));

        // Each Done ends the human's planning with no orders. The computer players then plan and
        // the turn resolves as in a headless match, up to the human's next planning entry.
        // A planning write changes the state outside the recorder, as the probe changes the
        // original's memory outside the game, so such a run's journal is not verified.
        var recorder = inputs.Planning.Count == 0
            ? new MatchReplayRecorder(match)
            : MatchReplayRecorder.Unverified(match);
        var human = new PlayerId(inputs.Humans[0].Slot);
        donePresses = 0;
        for (var turn = 0; turn < inputs.DoneCount; turn++)
        {
            atPlanningEntry?.Invoke(match, human, turn + 1);
            // DEV-EQUIP-001: the rebuild resolves Equip and Sell in the order they are submitted.
            // Every recording lists a turn's orders in roster order, the original's scan order.
            // The replay plays the lowest human's planning only, as the probe presses Done only in
            // the first human's; an order or hire for another human has no planning to go into.
            foreach (var order in inputs.Orders.Where(order => order.Turn == turn + 1))
            {
                Assert.Equal(human, new PlayerId(order.Player));
                Submit(recorder, match, human, order);
            }
            // RULE-HIRE-003: the probe writes the offer slot's hire order as the hire screen does;
            // the rebuild queues the gang that slot offers.
            foreach (var hire in inputs.Hires.Where(hire => hire.Turn == turn + 1))
            {
                var hiring = new PlayerId(hire.Player);
                Assert.Equal(human, hiring);
                var offered = match.Players[hiring.Value].HireOfferSlots[hire.OfferSlot].GangDefinitionId;
                Assert.NotNull(offered);
                var result = recorder.QueueHire(hiring, offered.Value, hire.Sector);
                Assert.True(result.Accepted, $"turn {hire.Turn}: the rebuild refused the hire: {result}");
            }
            // FMT-STATE-007, RULE-AI-023, RULE-AI-027: the probe writes a computer player's family or
            // raider flag straight into the original's memory, for branches no local match reaches.
            // A cleared player_active (FND-STATE-004) takes the player out of the match as the
            // elimination check does (RULE-TURN-006), with its gangs and sectors left where they are.
            // A cash write sets the player's cash (FND-AI-055) so a human can pay for a hire every turn.
            foreach (var write in inputs.Planning.Where(write => write.Turn == turn + 1))
                if (write.Cash is { } cash) match.Players[write.Player].Cash = cash;
                else if (write.Retired) match.Players[write.Player].Status = PlayerStatus.Eliminated;
                else if (write.Raider) match.AiPlanning.SetRaiderMode(new PlayerId(write.Player));
                else match.AiPlanning.SetFamily(new PlayerId(write.Player), write.Slot, write.Family);
            beforeDone?.Invoke(match, human, turn + 1);
            if (afterDone is null) recorder.FinishCommand(human);
            else
            {
                // The game ends the human's planning with FinishPlanningTurn, which runs on to the
                // next planning entry before an update shows the idle pointer. Its steps are the
                // ones AdvanceToRecordedEndpoint takes, so the replay is unchanged.
                GameplayTurnFlow.FinishPlanningTurn(recorder, human);
                afterDone(match);
            }
            donePresses++;
            AdvanceToRecordedEndpoint(recorder, human, inputs.HumanController);
            if (!IsActive(match, human) || match.Outcome is not null) break;

            recorder.PrepareHireOffers(human);
        }

        return match;
    }

    // The probe writes an order straight into the gang record of the human the input names
    // (FMT-STATE-001) as the order screens do (RULE-TURN-005); the rebuild takes the same order as
    // that human's command. A slot is the gang's roster slot (FMT-STATE-001), the index of its
    // player's gang list.
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
            // target of an Influence is the site slot, 0 to 2, of the gang's sector (FMT-STATE-001).
            GangAction.Influence => new GameCommand(human, gang.Id, action,
                CommandTarget.Site(gang.SectorId * MatchLimits.SitesPerSector + order.Target), order.Repeat),
            GangAction.Bribe or GangAction.Chaos or GangAction.Control or GangAction.Heal
                or GangAction.Hide or GangAction.Snitch or GangAction.Terminate =>
                new GameCommand(human, gang.Id, action, CommandTarget.None, order.Repeat),
            _ => throw new NotSupportedException($"No recorded order of action {action} is replayed yet."),
        };
        var result = recorder.Submit(command);
        Assert.True(result.Accepted, $"turn {order.Turn}: the rebuild refused {action}: {result}");
    }

    // The fixture keeps the flags a name set rather than the name (FND-SETUP-015), so the name is
    // the modifier string whose flag is set. A plain human keeps the slot's default name
    // (FND-SETUP-017).
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
            .Select(modifier => modifier.Name).SingleOrDefault() ?? LocalSetupPolicy.DefaultPlayerName(slot);
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
}
