using Rechaos.Core.Assets;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Rechaos.Multiplayer.Protocol;
using Rechaos.Multiplayer.Session;
using Xunit;

namespace Rechaos.Tests;

/// <summary>
/// A seat's view (docs/MULTIPLAYER.md, "What a seat may know") at every planning entry of every
/// recorded run: it hides what the spec says the seat may not know, the screens a player plans
/// from read the same from it as from the whole match, and the human's recorded orders and hires,
/// given through the online client's planning copy, build the same order document on the view as
/// on the whole match.
/// </summary>
public sealed partial class OriginalNewGameExperimentTests
{
    [Theory]
    [MemberData(nameof(MatchingRuns))]
    public void ASeatPlansTheSameTurnFromItsView(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var definitions = BundledOriginalData.Load();
        SpeculativeTurn? onView = null;
        SpeculativeTurn? onWhole = null;
        var entries = 0;
        StartMatch(recorded, out _,
            atPlanningEntry: (match, human, turn) =>
            {
                onView = null;
                onWhole = null;
                if (match.Coordinator.Phase != TurnPhase.Command || match.Coordinator.ActivePlayer != human
                    || match.Outcome is not null || match.FindPlayer(human)!.Status != PlayerStatus.Active)
                    return;
                var view = SeatView.Project(match, human);
                entries++;
                SeatViewAssertions.HidesWhatTheSeatMayNotKnow(match, view, human);
                SeatViewAssertions.ShowsWhatTheSeatPlansFrom(match, view, human);
                onView = SpeculativeTurn.For(view, definitions, human.Value);
                onWhole = SpeculativeTurn.For(match, definitions, human.Value);
                Assert.Equal(human, onView.State.ViewedBy);
                // The client gives the orders the player gave, naming only gangs it sees.
                foreach (var order in recorded.Orders.Where(order => order.Turn == turn))
                {
                    var command = Command(match, human, order);
                    if (command.Target.Kind == CommandTargetKind.Gang)
                        Assert.NotNull(view.FindGang(new GangId(command.Target.Id)));
                    var result = onView.Submit(command);
                    Assert.True(result.Accepted, $"turn {turn}: the view refused {command.Action}: {result}");
                    Assert.Equal(result.Validation, onWhole.Submit(command).Validation);
                }
                foreach (var hire in recorded.Hires.Where(hire => hire.Turn == turn))
                {
                    var offered = view.Players[human.Value].HireOfferSlots[hire.OfferSlot].GangDefinitionId;
                    Assert.NotNull(offered);
                    var result = onView.QueueHire(offered.Value, hire.Sector);
                    Assert.True(result.Accepted, $"turn {turn}: the view refused the hire");
                    Assert.Equal(result.Validation, onWhole.QueueHire(offered.Value, hire.Sector).Validation);
                }
            },
            beforeDone: (match, human, turn) =>
            {
                if (onView is null || onWhole is null) return;
                Assert.Equal(OrderDigest.CanonicalTextOf(onWhole.Build()), OrderDigest.CanonicalTextOf(onView.Build()));
                Assert.Equal(OwnQueue(match, human), OwnQueue(onView.State, human));
                Assert.Equal(match.Players[human.Value].PendingHires, onView.State.Players[human.Value].PendingHires);
            });
        Assert.True(entries > 0 || recorded.DoneCount == 0, "No planning entry was viewed.");
    }

    private static IReadOnlyList<GameCommand> OwnQueue(MatchState state, PlayerId seat) =>
        state.Commands.ExecutionPlan()
            .Where(queued => queued.Command.Player == seat)
            .Select(queued => queued.Command)
            .ToArray();
}

/// <summary>What a seat's view must hide and what it must show, checked against the whole match.</summary>
internal static class SeatViewAssertions
{
    public static void HidesWhatTheSeatMayNotKnow(MatchState whole, MatchState view, PlayerId seat)
    {
        Assert.Equal(seat, view.ViewedBy);
        Assert.Equal(seat, view.Coordinator.ActivePlayer);
        Assert.Equal(0u, view.Random.State);
        Assert.Equal(0, view.Random.ConsumptionCount);
        Assert.Equal(0, view.Setup.InitialSeed);
        Assert.Empty(view.PhaseHashes);
        Assert.All(view.Commands.ExecutionPlan(), queued => Assert.Equal(seat, queued.Command.Player));
        for (var observer = 0; observer < MatchLimits.PlayerCount; observer++)
        {
            Assert.Equal(0, view.AiStrategy.Reaction(new PlayerId(observer)));
            for (var other = 0; other < MatchLimits.PlayerCount; other++)
                Assert.Equal(0, view.AiStrategy.Attitude(new PlayerId(observer), new PlayerId(other)));
        }
        Assert.All(view.Events, gameEvent =>
        {
            if (gameEvent.Resolution is { } resolution)
            {
                Assert.Empty(resolution.Rolls);
                Assert.True(resolution.RetaliationRolls is null || resolution.RetaliationRolls.Count == 0);
                Assert.Null(resolution.DetectionRoll);
                Assert.Null(resolution.ChanceRoll);
            }
            if (gameEvent.PoliceAttack is { } police) Assert.Empty(police.Rolls);
            if (gameEvent.Kind is GameEventKind.CommandQueued or GameEventKind.CommandReplaced
                or GameEventKind.CommandCancelled or GameEventKind.HireQueued
                or GameEventKind.HireOfferSnubbed or GameEventKind.HireOfferRefilled
                or GameEventKind.UpkeepResolved or GameEventKind.BigManPointsAwarded)
                Assert.Equal(seat, gameEvent.Player);
        });

        foreach (var other in whole.Players.Where(player => player.Id != seat))
        {
            var seen = view.FindPlayer(other.Id)!;
            Assert.Equal(other.Status, seen.Status);
            Assert.Equal(other.Setup, seen.Setup);
            Assert.Equal(0, seen.Cash);
            Assert.Equal(0, seen.Support);
            Assert.Equal(0, seen.BigManPoints);
            Assert.Empty(seen.ResearchProgress);
            Assert.Empty(seen.ResearchedItems);
            Assert.Empty(seen.Inventory);
            Assert.Empty(seen.PendingHires);
            Assert.Empty(seen.HirePool);
            Assert.Null(seen.SnubbedHireOffer);
            Assert.Equal(0, seen.Statistics.CashEarned + seen.Statistics.CashSpent + seen.Statistics.DamageInflicted
                + seen.Statistics.Casualties + seen.Statistics.Overthrows + seen.Statistics.TimesHidden);
            Assert.Empty(view.NotificationsFor(other.Id));
            Assert.Empty(view.ComlinkFor(other.Id).Messages);
            // RULE-DETECT-001: exactly the gangs the seat detected at its planning entry.
            var detected = other.Gangs
                .Where(gang => gang.IsActive && (gang.VisibilityMask & (1 << seat.Value)) != 0)
                .ToArray();
            Assert.Equal(detected.Select(gang => gang.Id), seen.Gangs.Select(gang => gang.Id));
            foreach (var gang in seen.Gangs)
            {
                var original = whole.FindGang(gang.Id)!;
                Assert.True(whole.CanPlayerDetectGang(seat, gang.Id));
                Assert.Equal((original.DefinitionId, original.SectorId, original.Force, original.WeaponItemId,
                        original.ArmorItemId, original.MiscellaneousItemId, original.StoredStatistics),
                    (gang.DefinitionId, gang.SectorId, gang.Force, gang.WeaponItemId,
                        gang.ArmorItemId, gang.MiscellaneousItemId, gang.StoredStatistics));
                Assert.False(gang.Hidden);
                Assert.Null(gang.QueuedCommand);
            }
        }

        foreach (var sector in whole.Sectors)
        {
            var seen = view.Sectors[sector.Id];
            Assert.Equal((sector.Owner, sector.Income, sector.Tolerance, sector.CrackdownActive, sector.IsImportant),
                (seen.Owner, seen.Income, seen.Tolerance, seen.CrackdownActive, seen.IsImportant));
            Assert.Equal(Math.Sign(sector.CrackdownTurnsRemaining), seen.CrackdownTurnsRemaining);
            Assert.Empty(seen.CrackdownHistory);
            Assert.Equal(sector.Sites.Select(site => site.DefinitionId), seen.Sites.Select(site => site.DefinitionId));
            if (sector.Owner == seat)
            {
                Assert.Equal(sector.Sites.Select(site => (site.Resistance, site.InfluencedBy)),
                    seen.Sites.Select(site => (site.Resistance, site.InfluencedBy)));
                Assert.Equal((sector.Support, sector.CashYield, sector.BaseTolerance),
                    (seen.Support, seen.CashYield, seen.BaseTolerance));
                continue;
            }
            Assert.All(seen.Sites, site =>
            {
                Assert.Equal(whole.Definitions.Site(site.DefinitionId).Resistance, site.Resistance);
                Assert.Null(site.InfluencedBy);
            });
            Assert.Equal(0, seen.Support);
        }
    }

    public static void ShowsWhatTheSeatPlansFrom(MatchState whole, MatchState view, PlayerId seat)
    {
        var own = whole.FindPlayer(seat)!;
        var ownSeen = view.FindPlayer(seat)!;
        Assert.Equal((own.Cash, own.Support, own.ScenarioScore, own.BigManPoints),
            (ownSeen.Cash, ownSeen.Support, ownSeen.ScenarioScore, ownSeen.BigManPoints));
        Assert.Equal(own.HireOfferSlots, ownSeen.HireOfferSlots);
        Assert.Equal(own.ResearchProgress.OrderBy(pair => pair.Key), ownSeen.ResearchProgress.OrderBy(pair => pair.Key));
        Assert.Equal(own.Gangs.Select(gang => (gang.Id, gang.SectorId, gang.Force, gang.QueuedCommand?.Command)),
            ownSeen.Gangs.Select(gang => (gang.Id, gang.SectorId, gang.Force, gang.QueuedCommand?.Command)));
        Assert.Equal(whole.NotificationsFor(seat).Select(item => (item.Sequence, item.Kind, item.Gang, item.SectorId, item.Turn)),
            view.NotificationsFor(seat).Select(item => (item.Sequence, item.Kind, item.Gang, item.SectorId, item.Turn)));
        Assert.Equal(whole.ComlinkFor(seat).Messages, view.ComlinkFor(seat).Messages);

        // Every order the seat could give, and every hire, is judged the same.
        foreach (var gang in own.Gangs.Where(gang => gang.IsActive))
            Assert.Equal(CommandOptionCatalog.LegalCommands(whole, seat, gang.Id),
                CommandOptionCatalog.LegalCommands(view, seat, gang.Id));
        foreach (var offer in own.HirePool.Distinct())
        for (var sector = 0; sector < MatchLimits.SectorCount; sector++)
            Assert.Equal(HireRules.Validate(whole, seat, offer, sector), HireRules.Validate(view, seat, offer, sector));

        // The screens: rankings (SCR-OBJECTIVE-001), console values (RULE-UI-011), markers
        // (RULE-UI-006, RULE-SEARCH-002), the sector's Overlord bar and cards (RULE-UI-010),
        // finance (RULE-FINANCE-001) and combat (FND-COMBAT-012, RULE-COMBAT-004).
        Assert.Equal(PlayerRankingPresentation.Project(whole).Select(entry => (entry.Player, entry.Offset)),
            PlayerRankingPresentation.Project(view).Select(entry => (entry.Player, entry.Offset)));
        var wholeSight = GangSightSnapshot.Capture(whole, seat);
        var viewSight = GangSightSnapshot.Capture(view, seat);
        Assert.Equal(GangStatusMarkerPresentation.MapFrames(whole, seat, wholeSight),
            GangStatusMarkerPresentation.MapFrames(view, seat, viewSight));
        for (var sector = 0; sector < MatchLimits.SectorCount; sector++)
        {
            Assert.Equal(StatusConsolePresentation.SectorValues(whole, seat, sector),
                StatusConsolePresentation.SectorValues(view, seat, sector));
            Assert.Equal(SectorOpponentGangs.SeenSeats(whole, seat, sector), SectorOpponentGangs.SeenSeats(view, seat, sector));
            foreach (var owner in whole.Players)
                Assert.Equal(SectorOpponentGangs.InSector(whole, seat, owner.Id, sector).Select(gang => gang.Id),
                    SectorOpponentGangs.InSector(view, seat, owner.Id, sector).Select(gang => gang.Id));
            Assert.Equal(ControlledSites(whole, seat, sector), ControlledSites(view, seat, sector));
            Assert.Equal(FinanceProjection.Project(whole, own, sector), FinanceProjection.Project(view, ownSeen, sector));
        }
        Assert.Equal(FinanceProjection.Project(whole, own, null), FinanceProjection.Project(view, ownSeen, null));
        Assert.Equal(CombatPages(whole, seat), CombatPages(view, seat));
        Assert.Equal(Presented(whole, seat), Presented(view, seat));
    }

    // RULE-SEARCH-002: the sites the city marks as the seat's own.
    private static IEnumerable<bool> ControlledSites(MatchState state, PlayerId seat, int sector) =>
        state.Sectors[sector].Sites.Select(site => site.Resistance == 0 && state.Sectors[sector].Owner == seat);

    private static IReadOnlyList<string> CombatPages(MatchState state, PlayerId seat) =>
        CombatResultProjection.Pages(state, seat)
            .Select(page => $"{page.SectorId}:" + string.Join(",", page.Results.Select(result =>
                $"{result.FirstPlayer.Value}/{result.FirstGang.Value}/{result.SecondPlayer?.Value}/{result.SecondGang?.Value}/{result.Police}")))
            .ToArray();

    private static IReadOnlyList<string> Presented(MatchState state, PlayerId seat) =>
        CombatResultProjection.AutomaticPresentationEvents(state, seat, state.Events)
            .Select(gameEvent => $"{gameEvent.Kind}/{gameEvent.Player.Value}/{gameEvent.Gang?.Value}/{gameEvent.Target}")
            .ToArray();
}
