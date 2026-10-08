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
    /// Every member of the save document the view is built from, with what the view does with it.
    /// A member added to the save without a line here fails the test, so nobody can add state
    /// without deciding who may know it. The words match the table in docs/MULTIPLAYER.md.
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
        ["PlayerNotificationsDocument.Player"] = "own",
        ["PlayerNotificationsDocument.NextSequence"] = "own",
        ["PlayerNotificationsDocument.Items"] = "own",
        ["PlayerComlinkDocument.Player"] = "own",
        ["PlayerComlinkDocument.NextSequence"] = "own",
        ["PlayerComlinkDocument.ReadThroughSequence"] = "own",
        ["PlayerComlinkDocument.Items"] = "own",
        ["PlayerComlinkDocument.ReadSequences"] = "own",
    };

    [Fact]
    public void EveryPartOfTheSaveHasAVisibilityDecision()
    {
        Type[] documents =
        [
            typeof(NativeSaveDocument), typeof(MatchSetupDocument), typeof(PlayerSetupDocument),
            typeof(PlayerDocument), typeof(GangDocument), typeof(StatisticsDocument), typeof(SectorDocument),
            typeof(SiteDocument), typeof(RuntimeDocument), typeof(PlayerNotificationsDocument),
            typeof(PlayerComlinkDocument),
        ];
        var members = documents
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.Name != "EqualityContract")
                .Select(property => $"{type.Name}.{property.Name}"))
            .Order()
            .ToArray();

        Assert.Equal(Decisions.Keys.Order(), members);
    }
}
