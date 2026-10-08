using System.Reflection;
using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Core.Persistence;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// SeatView, the match as one seat may know it (docs/MULTIPLAYER.md, "What a seat may know").
/// OriginalNewGameExperimentTests.ASeatPlansTheSameTurnFromItsView holds every recorded run to it.
/// </summary>
public sealed class SeatViewTests
{
    // EXP-TURN-051: the human's gang attacks and the police kill a gang, so the last resolution
    // has fights to keep and to leave out.
    private static MatchState Whole() => OriginalNewGameExperimentTests.ReplayedMatch("EXP-TURN-051", 0);

    [Fact]
    public void AViewRefusesToDrawOrResolve()
    {
        var whole = Whole();
        var seat = whole.Coordinator.ActivePlayer!.Value;
        var view = SeatView.Project(whole, seat);

        Assert.Throws<InvalidOperationException>(() => view.FinishCommand(seat));
        Assert.Throws<InvalidOperationException>(() => view.PrepareHireOffers(seat));
        Assert.Throws<InvalidOperationException>(() => view.PrepareSimultaneousHireOffers());
        Assert.Throws<InvalidOperationException>(() => AiTurnPlanner.ChooseHire(view, seat));
        Assert.Throws<ArgumentException>(() => SeatView.Project(view, seat));
    }

    [Fact]
    public void AViewTravelsAsASavePayloadAndComesBackAsTheSameView()
    {
        var whole = Whole();
        var seat = whole.Coordinator.ActivePlayer!.Value;
        var view = SeatView.Project(whole, seat);
        using var stream = new MemoryStream();
        SeatView.Save(stream, view);
        stream.Position = 0;

        var loaded = SeatView.Load(stream, BundledOriginalData.Load(), seat);

        Assert.Equal(seat, loaded.ViewedBy);
        Assert.Equal(MatchStateHasher.ComputeFingerprint(view), MatchStateHasher.ComputeFingerprint(loaded));
        Assert.Throws<ArgumentException>(() => SeatView.Save(new MemoryStream(), whole));

        // The payload names its seat: a plain load still restores a view, and SeatView refuses a
        // payload that is not this seat's view.
        stream.Position = 0;
        Assert.Equal(seat, NativeSaveSerializer.Load(stream, BundledOriginalData.Load()).ViewedBy);
        stream.Position = 0;
        var other = whole.Players.First(player => player.Id != seat).Id;
        Assert.Throws<InvalidDataException>(() => SeatView.Load(stream, BundledOriginalData.Load(), other));
        using var wholeSave = new MemoryStream();
        NativeSaveSerializer.Save(wholeSave, whole);
        wholeSave.Position = 0;
        Assert.Throws<InvalidDataException>(() => SeatView.Load(wholeSave, BundledOriginalData.Load(), seat));
    }

    [Fact]
    public void AViewIsTakenOnlyAtAPlanningEntryOfAMatchStillBeingPlayed()
    {
        var whole = Whole();
        var seat = whole.Coordinator.ActivePlayer!.Value;
        Assert.Throws<ArgumentOutOfRangeException>(() => SeatView.Project(whole, new PlayerId(MatchLimits.PlayerCount)));
        whole.FinishCommand(seat);
        if (whole.Coordinator.Phase != TurnPhase.Command)
            Assert.Throws<InvalidOperationException>(() => SeatView.Project(whole, seat));
    }

    // SCR-OBJECTIVE-001: other seats' scores place their portraits where the true scores do, the
    // seat's own score is exact, and the spread is no wider than the true one.
    [Fact]
    public void RankingScoresPlaceEveryPortraitWhereTheTrueScoresDo()
    {
        var random = new Random(515);
        long[] spreads = [3, 40, 139, 140, 141, 1_000, 30_000, 2_000_000];
        for (var trial = 0; trial < 4_000; trial++)
        {
            var spread = spreads[trial % spreads.Length];
            var count = random.Next(2, MatchLimits.PlayerCount + 1);
            var players = Enumerable.Range(0, count)
                .Select(index => (Player: new PlayerId(index), Score: random.NextInt64(-spread / 4, spread), Active: true))
                .ToList();
            if (count > 2 && random.Next(3) == 0)
                players[^1] = players[^1] with { Score = -32000, Active = false };
            var seat = players[random.Next(players.Count(player => player.Active))].Player;

            var scores = SeatView.RankingScores(players, seat);

            var active = players.Where(player => player.Active).ToArray();
            long high = active.Max(player => player.Score), low = active.Min(player => player.Score);
            long shownHigh = active.Max(player => scores[player.Player]), shownLow = active.Min(player => scores[player.Player]);
            foreach (var player in active)
                Assert.Equal(ScenarioScoreRail.Offset(player.Score, high, low),
                    ScenarioScoreRail.Offset(scores[player.Player], shownHigh, shownLow));
            Assert.Equal(players.Single(player => player.Player == seat).Score, scores[seat]);
            Assert.True(shownHigh - shownLow <= high - low);
            foreach (var player in players.Where(player => !player.Active))
                Assert.Equal(player.Score, scores[player.Player]);
        }
    }

    // Greed: the panel shows that the leader is far ahead, not by how much cash.
    [Fact]
    public void AWideGreedLeadIsShownAtTheRailsResolutionOnly()
    {
        var scores = SeatView.RankingScores(
            [(new PlayerId(0), 120, true), (new PlayerId(1), 48_317, true), (new PlayerId(2), 9_004, true)],
            new PlayerId(0));

        Assert.Equal(120, scores[new PlayerId(0)]);
        Assert.NotEqual(48_317, scores[new PlayerId(1)]);
        Assert.NotEqual(9_004, scores[new PlayerId(2)]);
        Assert.True(scores[new PlayerId(1)] - scores[new PlayerId(0)] < 1_000);
    }

    /// <summary>
    /// Every member of the save document the view is built from, and of every record nested under
    /// a member that can carry another seat's data, with what the view does with it. A member
    /// added to the save or to such a record without a line here fails the test, so nobody can add
    /// state without deciding who may know it. The words match the table in docs/MULTIPLAYER.md.
    /// </summary>
    private static readonly Dictionary<string, string> Decisions = new()
    {
        ["NativeSaveDocument.FormatVersion"] = "public",
        ["NativeSaveDocument.DefinitionsSha256"] = "public",
        ["NativeSaveDocument.StateFingerprint"] = "recomputed",
        ["NativeSaveDocument.Setup"] = "public",
        ["NativeSaveDocument.Players"] = "per player",
        ["NativeSaveDocument.Sectors"] = "per sector",
        ["NativeSaveDocument.Runtime"] = "per member",
        ["MatchSetupDocument.Scenario"] = "public",
        ["MatchSetupDocument.Duration"] = "public",
        ["MatchSetupDocument.InitialSeed"] = "hidden",
        ["MatchSetupDocument.Players"] = "public",
        ["MatchSetupDocument.AiMentality"] = "public",
        ["MatchSetupDocument.AiPolicy"] = "public",
        ["MatchSetupDocument.ComputerMovesToNeighboursOnly"] = "public",
        ["MatchSetupDocument.ComputerHiresWhereHumansCan"] = "public",
        ["PlayerSetupDocument.Id"] = "public",
        ["PlayerSetupDocument.Name"] = "public",
        ["PlayerSetupDocument.Controller"] = "public",
        ["PlayerSetupDocument.PortraitId"] = "public",
        ["PlayerDocument.Id"] = "public",
        ["PlayerDocument.Cash"] = "own",
        ["PlayerDocument.Support"] = "own",
        ["PlayerDocument.BigManPoints"] = "own",
        ["PlayerDocument.Status"] = "public",
        ["PlayerDocument.Gangs"] = "own, and detected",
        ["PlayerDocument.HirePool"] = "own",
        ["PlayerDocument.PendingHires"] = "own",
        ["PlayerDocument.ResearchProgress"] = "own",
        ["PlayerDocument.ResearchedItems"] = "own",
        ["PlayerDocument.Inventory"] = "own",
        ["PlayerDocument.Statistics"] = "own",
        ["PlayerDocument.SnubbedHireOffer"] = "own",
        ["PlayerDocument.HireOfferSlots"] = "own",
        ["PlayerDocument.SnubbedHireOfferSlot"] = "own",
        ["PlayerDocument.UsesMaximumHireForce"] = "public",
        ["PlayerDocument.ScenarioScore"] = "own, others coarsened",
        ["GangDocument.Id"] = "own, and detected",
        ["GangDocument.DefinitionId"] = "own, and detected",
        ["GangDocument.SectorId"] = "own, and detected",
        ["GangDocument.Force"] = "own, and detected",
        ["GangDocument.Hidden"] = "own",
        ["GangDocument.HiredThisTurn"] = "own",
        ["GangDocument.WeaponItemId"] = "own, and detected",
        ["GangDocument.ArmorItemId"] = "own, and detected",
        ["GangDocument.MiscellaneousItemId"] = "own, and detected",
        ["GangDocument.Statistics"] = "own, and detected",
        ["GangDocument.RetiredForce"] = "own",
        ["GangDocument.VisibilityMask"] = "seat's bit",
        ["StatisticsDocument.CashEarned"] = "own",
        ["StatisticsDocument.CashSpent"] = "own",
        ["StatisticsDocument.DamageInflicted"] = "own",
        ["StatisticsDocument.Casualties"] = "own",
        ["StatisticsDocument.Overthrows"] = "own",
        ["StatisticsDocument.TimesHidden"] = "own",
        ["SectorDocument.Id"] = "public",
        ["SectorDocument.Owner"] = "public",
        ["SectorDocument.Tolerance"] = "public",
        ["SectorDocument.CrackdownActive"] = "public",
        ["SectorDocument.IsImportant"] = "public",
        ["SectorDocument.Sites"] = "per site",
        ["SectorDocument.Income"] = "public",
        ["SectorDocument.CrackdownTurnsRemaining"] = "public, as its sign",
        ["SectorDocument.CrackdownHistory"] = "hidden",
        ["SectorDocument.Chaos"] = "hidden",
        ["SectorDocument.BaseTolerance"] = "owner",
        ["SectorDocument.Support"] = "owner",
        ["SectorDocument.CashYield"] = "owner",
        ["SiteDocument.Slot"] = "public",
        ["SiteDocument.DefinitionId"] = "public",
        ["SiteDocument.Resistance"] = "owner",
        ["SiteDocument.InfluencedBy"] = "owner",
        ["RuntimeDocument.Turn"] = "public",
        ["RuntimeDocument.Phase"] = "public",
        ["RuntimeDocument.ExecutionPhase"] = "public",
        ["RuntimeDocument.ActivePlayer"] = "the seat",
        ["RuntimeDocument.RandomState"] = "hidden",
        ["RuntimeDocument.RandomConsumptionCount"] = "hidden",
        ["RuntimeDocument.Commands"] = "own",
        ["RuntimeDocument.NextCommandSequence"] = "own",
        ["RuntimeDocument.Events"] = "shown events, without dice",
        ["RuntimeDocument.NextEventSequence"] = "shown events, without dice",
        ["RuntimeDocument.Notifications"] = "own",
        ["RuntimeDocument.PhaseHashes"] = "hidden",
        ["RuntimeDocument.Outcome"] = "public",
        ["RuntimeDocument.AiStrategy"] = "hidden",
        ["RuntimeDocument.AiPlanning"] = "hidden",
        ["RuntimeDocument.Comlink"] = "own",
        ["RuntimeDocument.ViewedBy"] = "the seat",
        ["PlayerNotificationsDocument.Player"] = "own",
        ["PlayerNotificationsDocument.NextSequence"] = "own",
        ["PlayerNotificationsDocument.Items"] = "own",
        ["PlayerComlinkDocument.Player"] = "own",
        ["PlayerComlinkDocument.NextSequence"] = "own",
        ["PlayerComlinkDocument.ReadThroughSequence"] = "own",
        ["PlayerComlinkDocument.Items"] = "own",
        ["PlayerComlinkDocument.ReadSequences"] = "own",
        // The events a view keeps include other seats' fights and police attacks, so every record
        // an event holds is listed too. A kept event travels whole, less its dice.
        ["GameEvent.Sequence"] = "renumbered",
        ["GameEvent.Turn"] = "kept with the event",
        ["GameEvent.Phase"] = "kept with the event",
        ["GameEvent.ExecutionPhase"] = "kept with the event",
        ["GameEvent.Kind"] = "kept with the event",
        ["GameEvent.Player"] = "kept with the event",
        ["GameEvent.Gang"] = "kept with the event",
        ["GameEvent.Action"] = "kept with the event",
        ["GameEvent.Target"] = "kept with the event",
        ["GameEvent.SecondaryTarget"] = "kept with the event",
        ["GameEvent.TertiaryTarget"] = "kept with the event",
        ["GameEvent.QuaternaryTarget"] = "kept with the event",
        ["GameEvent.Resolution"] = "kept with the event",
        ["GameEvent.Economy"] = "kept with the event",
        ["GameEvent.Hire"] = "kept with the event",
        ["GameEvent.HireOffer"] = "kept with the event",
        ["GameEvent.Elimination"] = "kept with the event",
        ["GameEvent.PoliceAttack"] = "kept with the event",
        ["GameEvent.BigManPoints"] = "kept with the event",
        ["GameEvent.MatchOutcome"] = "kept with the event",
        ["CommandResolutionDetails.Code"] = "kept with the event",
        ["CommandResolutionDetails.Rolls"] = "removed",
        ["CommandResolutionDetails.Successes"] = "kept with the event",
        ["CommandResolutionDetails.AttackValue"] = "kept with the event",
        ["CommandResolutionDetails.DefenseValue"] = "kept with the event",
        ["CommandResolutionDetails.Damage"] = "kept with the event",
        ["CommandResolutionDetails.PreviousValue"] = "kept with the event",
        ["CommandResolutionDetails.ResultValue"] = "kept with the event",
        ["CommandResolutionDetails.CashDelta"] = "kept with the event",
        ["CommandResolutionDetails.DetectionChance"] = "kept with the event",
        ["CommandResolutionDetails.DetectionRoll"] = "removed",
        ["CommandResolutionDetails.ChanceSides"] = "kept with the event",
        ["CommandResolutionDetails.ChanceRoll"] = "removed",
        ["CommandResolutionDetails.ItemId"] = "kept with the event",
        ["CommandResolutionDetails.ReplacedItemId"] = "kept with the event",
        ["CommandResolutionDetails.RetaliationRolls"] = "removed",
        ["CommandResolutionDetails.RetaliationSuccesses"] = "kept with the event",
        ["CommandResolutionDetails.RetaliationDamage"] = "kept with the event",
        ["CommandResolutionDetails.RetaliationItemId"] = "kept with the event",
        ["CommandResolutionDetails.ItemIds"] = "kept with the event",
        ["CommandResolutionDetails.ReplacedItemIds"] = "kept with the event",
        ["CommandResolutionDetails.Attacker"] = "kept with the event",
        ["CommandResolutionDetails.Defender"] = "kept with the event",
        ["CombatantDetails.Owner"] = "kept with the event",
        ["CombatantDetails.DefinitionId"] = "kept with the event",
        ["CombatantDetails.SectorId"] = "kept with the event",
        ["CombatantDetails.Force"] = "kept with the event",
        ["CombatantDetails.WeaponItemId"] = "kept with the event",
        ["CombatantDetails.ArmorItemId"] = "kept with the event",
        ["CombatantDetails.MiscellaneousItemId"] = "kept with the event",
        ["CombatantDetails.RosterSlot"] = "kept with the event",
        ["PoliceAttackResolutionDetails.SectorId"] = "kept with the event",
        ["PoliceAttackResolutionDetails.Detected"] = "kept with the event",
        ["PoliceAttackResolutionDetails.DetectionChance"] = "kept with the event",
        ["PoliceAttackResolutionDetails.DetectionRoll"] = "removed",
        ["PoliceAttackResolutionDetails.AttackValue"] = "kept with the event",
        ["PoliceAttackResolutionDetails.DefenseValue"] = "kept with the event",
        ["PoliceAttackResolutionDetails.Rolls"] = "removed",
        ["PoliceAttackResolutionDetails.Successes"] = "kept with the event",
        ["PoliceAttackResolutionDetails.Damage"] = "kept with the event",
        ["PoliceAttackResolutionDetails.PreviousForce"] = "kept with the event",
        ["PoliceAttackResolutionDetails.ResultForce"] = "kept with the event",
        ["PoliceAttackResolutionDetails.Target"] = "kept with the event",
        ["EconomyResolutionDetails.PreviousCash"] = "kept with the event",
        ["EconomyResolutionDetails.SectorIncome"] = "kept with the event",
        ["EconomyResolutionDetails.SiteIncome"] = "kept with the event",
        ["EconomyResolutionDetails.GangUpkeep"] = "kept with the event",
        ["EconomyResolutionDetails.NetChange"] = "kept with the event",
        ["EconomyResolutionDetails.ResultCash"] = "kept with the event",
        ["EconomyResolutionDetails.IsInDebt"] = "kept with the event",
        ["HireResolutionDetails.Cost"] = "kept with the event",
        ["HireResolutionDetails.Gang"] = "kept with the event",
        ["HireResolutionDetails.GangDefinitionId"] = "kept with the event",
        ["HireResolutionDetails.InitialForce"] = "kept with the event",
        ["HireResolutionDetails.ReplacementOffer"] = "kept with the event",
        ["HireResolutionDetails.SectorId"] = "kept with the event",
        ["HireOfferDetails.RemovedOffer"] = "kept with the event",
        ["HireOfferDetails.AddedOffer"] = "kept with the event",
        ["EliminationDetails.EliminatedPlayer"] = "kept with the event",
        ["EliminationDetails.RemainingPlayers"] = "kept with the event",
        ["BigManPointDetails.ControlledCentralSectors"] = "kept with the event",
        ["BigManPointDetails.PreviousPoints"] = "kept with the event",
        ["BigManPointDetails.ResultPoints"] = "kept with the event",
        ["MatchOutcomeDetails.Scenario"] = "public",
        ["MatchOutcomeDetails.Reason"] = "public",
        ["MatchOutcomeDetails.CompletedTurn"] = "public",
        ["MatchOutcomeDetails.Winners"] = "public",
        ["MatchOutcomeDetails.Standings"] = "public",
        ["MatchOutcomeDetails.Awards"] = "public",
        ["MatchOutcome.Scenario"] = "public",
        ["MatchOutcome.Reason"] = "public",
        ["MatchOutcome.Turn"] = "public",
        ["MatchOutcome.Winners"] = "public",
        ["MatchOutcome.Standings"] = "public",
        ["MatchOutcome.Awards"] = "public",
        ["MatchStanding.Place"] = "public",
        ["MatchStanding.Player"] = "public",
        ["MatchStanding.Score"] = "public",
        ["EndgameAwardResult.Award"] = "public",
        ["EndgameAwardResult.Recipients"] = "public",
        ["EndgameAwardResult.Value"] = "public",
    };

    [Fact]
    public void EveryPartOfTheSaveHasAVisibilityDecision()
    {
        // Walk from the save's documents down every member whose decision lets another seat's data
        // through, so a field added to a nested record that travels (a fight event's combatant,
        // say) needs a decision as well. A member that is the seat's own, or hidden, settles
        // everything below it.
        var members = new SortedSet<string>(StringComparer.Ordinal);
        var visited = new HashSet<Type>();
        var pending = new Queue<Type>(
        [
            typeof(NativeSaveDocument), typeof(MatchSetupDocument), typeof(PlayerSetupDocument),
            typeof(PlayerDocument), typeof(GangDocument), typeof(StatisticsDocument), typeof(SectorDocument),
            typeof(SiteDocument), typeof(RuntimeDocument), typeof(PlayerNotificationsDocument),
            typeof(PlayerComlinkDocument),
        ]);
        while (pending.TryDequeue(out var type))
        {
            if (!visited.Add(type)) continue;
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.Name == "EqualityContract") continue;
                var member = $"{type.Name}.{property.Name}";
                members.Add(member);
                if (Decisions.TryGetValue(member, out var decision) && decision is "own" or "hidden" or "the seat")
                    continue;
                foreach (var nested in NestedRecords(property.PropertyType))
                    pending.Enqueue(nested);
            }
        }

        var undecided = members.Except(Decisions.Keys).ToArray();
        Assert.True(undecided.Length == 0, "No visibility decision for: " + string.Join(", ", undecided));
        var stale = Decisions.Keys.Except(members).Order(StringComparer.Ordinal).ToArray();
        Assert.True(stale.Length == 0, "Decisions for members the save no longer has: " + string.Join(", ", stale));
    }

    // The game's own reference types a member's value holds, directly or as list elements.
    private static IEnumerable<Type> NestedRecords(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } underlying) type = underlying;
        if (type.IsArray) type = type.GetElementType()!;
        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
                foreach (var nested in NestedRecords(argument))
                    yield return nested;
            yield break;
        }
        if (type.IsClass && type.Namespace?.StartsWith("Rechaos.", StringComparison.Ordinal) == true)
            yield return type;
    }
}
