using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;
using Rechaos.Game;
using Xunit;

namespace Rechaos.Tests;

public sealed partial class OriginalNewGameExperimentTests
{
    public static TheoryData<string, int> OrderStepRuns()
    {
        var data = new TheoryData<string, int>();
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].OrderSteps.Count > 0 && !KnownDivergences.ContainsKey((experiment, run)))
                    data.Add(experiment, run);
        return data;
    }

    // RULE-TURN-005, SCR-UI-004: after the dump the probe opened a sector view and pressed the gang
    // cards' strips and the group order strip, answering each popup menu with a command, and kept
    // the popup, its items and the active player's orders after each press. The rebuild's hit tests
    // open the same menu for each press, its menus list the same commands and offer the orders the
    // original leaves enabled, and the order the rebuild gives through the chosen action leaves
    // each gang with the original's action and repeat_action. RULE-UI-010: a press on an overlord's
    // portrait lists that overlord's gangs on the cards where the original does, and a card of
    // another overlord's gang opens no menu.
    [Theory]
    [MemberData(nameof(OrderStepRuns))]
    public void TheOrderMenusGiveTheOriginalsOrders(string experiment, int run)
    {
        var recorded = Run(experiment, run);
        var match = StartMatch(recorded, out _);
        var human = recorded.Humans[0];
        var player = match.FindPlayer(human)!;
        int? sector = null;
        PlayerId? owner = null;
        foreach (var step in recorded.OrderSteps)
        {
            var label = $"{step.Kind} {step.Target} ({step.X}, {step.Y}) command {step.Choice}";
            if (step.Kind == "open") (sector, owner) = (step.Target, null);
            if (step.Kind == "back") sector = null;
            var portrait = step.Kind == "strip" && sector is not null
                ? SectorOpponentGangs.PortraitAt(match, new Point(step.X, step.Y))
                : null;
            if (portrait is { } pressed)
                owner = SectorOpponentGangs.PressPortrait(match, human, owner, pressed, sector!.Value);
            Assert.True(step.CityView == sector is null, $"after {label}");
            Assert.True(portrait is null || step.Viewed >= 0,
                $"after {label}: a portrait press is recorded with the player the cards list");
            IReadOnlyList<MatchGangState> cards = sector is { } shown
                ? SectorOpponentGangs.Cards(match, human, owner, shown)
                : [];
            var viewed = cards.Count > 0 ? cards[0].Owner : human;
            if (sector is not null && step.Viewed >= 0)
                Assert.True(step.Viewed == viewed.Value,
                    $"after {label}: the original lists player {step.Viewed}'s gangs, the rebuild player {viewed.Value}'s");
            if (sector is not null)
            {
                var roster = match.FindPlayer(viewed)!.Gangs.ToList();
                Assert.True(step.Cards.SequenceEqual(Enumerable.Range(0, SectorGangCardLayout.VisibleCards)
                    .Select(card => card < cards.Count ? roster.IndexOf(cards[card]) : -1)),
                    $"after {label}: the original's cards hold slots [{string.Join(",", step.Cards)}]");
            }

            var (menu, gangs) = step.Kind switch
            {
                _ when portrait is not null => (-1, (IReadOnlyList<MatchGangState>)[]),
                "card" => CardMenu(cards, human, step),
                "strip" => StripMenu(match, human, sector ?? -1, cards, step),
                _ => (-1, (IReadOnlyList<MatchGangState>)[]),
            };
            Assert.True(menu == step.Menu, $"{label}: the original opened menu {step.Menu}, the rebuild {menu}");
            if (menu > 0)
            {
                var actions = MenuActions(menu);
                Assert.Equal(step.Items!.Select(item => item.Command), actions.Select(entry => entry.Command));
                var offered = Offered(match, human, menu, gangs);
                foreach (var ((command, state), (_, action)) in step.Items!.Zip(actions))
                    Assert.True(state == 0 == offered.Contains(action),
                        $"{label}: menu {menu} {(state == 0 ? "enables" : "greys")} {action} (command {command}) in the original");
                if (step.Choice != 0) Choose(match, human, menu, gangs, actions.Single(entry => entry.Command == step.Choice).Action);
            }

            foreach (var gang in step.Gangs.Where(gang => gang[0] < 80))
            {
                var queued = player.Gangs[gang[0]].QueuedCommand?.Command;
                var action = queued is null ? 0 : (int)queued.Action;
                var repeat = queued is { Repeat: true } ? action : 0;
                Assert.True((gang[2], gang[5]) == (action, repeat),
                    $"{label}: slot {gang[0]} has action {gang[2]} and repeat_action {gang[5]} in the original, {action} and {repeat} in the rebuild");
            }
        }
    }

    // SCR-UI-004, FND-UI-015, FND-UI-021: a press on an own card's strip opens menu 1 for its
    // one-off part and menu 2 for its recurring part. Another player's card takes no orders
    // (RULE-UI-010), so it opens no menu.
    private static (int Menu, IReadOnlyList<MatchGangState> Gangs) CardMenu(
        IReadOnlyList<MatchGangState> cards, PlayerId human, RecordedOrderStep step)
    {
        var point = new Point(
            SectorGangCardLayout.Left + step.Target % 2 * SectorGangCardLayout.ColumnStride + step.X,
            SectorGangCardLayout.Top + step.Target / 2 * SectorGangCardLayout.RowStride + step.Y);
        var card = SectorGangCardLayout.CardAt(point);
        if (card < 0 || card >= cards.Count || cards[card].Owner != human) return (-1, []);
        return SectorGangCardLayout.ActionRepeatAt(card, point) switch
        {
            false => (1, [cards[card]]),
            true => (2, [cards[card]]),
            null => (-1, []),
        };
    }

    // SCR-UI-004, FND-UI-015, FND-UI-021: the group order strip, drawn over two or more cards,
    // opens menu 3 on its left part and menu 5 on its right, for every gang of the player in the
    // sector (FND-TURN-009). It is drawn only over the active player's own cards (FND-UI-018).
    private static (int Menu, IReadOnlyList<MatchGangState> Gangs) StripMenu(
        MatchState match, PlayerId human, int sector, IReadOnlyList<MatchGangState> cards, RecordedOrderStep step)
    {
        var point = new Point(step.X, step.Y);
        if (!ChaosGame.ShowsGroupOrderStrip(match, match.Coordinator.ActivePlayer, human, cards)
            || !SectorDetailLayout.GroupOrderStrip.Contains(point)) return (-1, []);
        return (SectorDetailLayout.GroupOrderIsRecurring(point) ? 5 : 3, ChaosGame.GroupOrderGangs(match, human, sector));
    }

    // FND-UI-021: menus 1, 2, 3 and 5 number their orders from 1 in the order the rebuild's menus
    // list them, then None and Terminate each two further on.
    private static IReadOnlyList<(int Command, GangAction Action)> MenuActions(int menu)
    {
        var listed = menu switch
        {
            1 => CommandOverlayLayout.ActionsFor(recurring: false),
            2 => CommandOverlayLayout.ActionsFor(recurring: true),
            3 => BulkGangCommands.GroupActionsFor(recurring: false),
            _ => BulkGangCommands.GroupActionsFor(recurring: true),
        };
        var orders = listed.Count(action => action is not (GangAction.None or GangAction.Terminate));
        return listed.Select((action, index) => (action switch
        {
            GangAction.None => orders + 2,
            GangAction.Terminate => orders + 4,
            _ => index + 1,
        }, action)).ToArray();
    }

    // RULE-TURN-005: a card's menu gives its gang the order; a group menu gives it to every gang of
    // the sector that the rebuild accepts it for (DEV-UI-003), and None cancels each gang's order.
    private static void Choose(MatchState match, PlayerId human, int menu, IReadOnlyList<MatchGangState> gangs, GangAction action)
    {
        var recurring = menu is 2 or 5;
        if (action == GangAction.None)
        {
            var (_, refusal) = ChaosGame.CancelOrders(match, gangs.Select(gang => gang.Id), gang => match.Cancel(human, gang));
            Assert.True(refusal is null, $"the rebuild refused None: {refusal}");
            return;
        }
        if (menu is 1 or 2)
        {
            var result = match.Submit(new GameCommand(human, gangs[0].Id, action, CommandTarget.None, recurring));
            Assert.True(result.Accepted, $"the rebuild refused {action}: {result}");
            return;
        }
        var plan = BulkGangCommands.Plan(match, human, gangs.Select(gang => gang.Id).ToArray(),
            new BulkCommandIntent(action, CommandTarget.None, recurring), group: true);
        foreach (var command in plan.Commands) Assert.True(match.Submit(command).Accepted);
    }

    // FND-UI-021, DEV-UI-021: the orders the rebuild's panel offers where the original's menu
    // leaves an item enabled.
    private static IReadOnlySet<GangAction> Offered(MatchState match, PlayerId human, int menu, IReadOnlyList<MatchGangState> gangs)
    {
        var recurring = menu is 2 or 5;
        var options = menu is 1 or 2
            ? CommandOptionCatalog.LegalCommands(match, human, gangs[0].Id)
                .Where(command => !recurring || CommandRules.CanRepeat(command.Action)).ToArray()
            : BulkGangCommands.Options(match, human, gangs.Select(gang => gang.Id).ToArray(), recurring, group: true);
        return MenuActions(menu).Select(entry => entry.Action)
            .Where(action => ChaosGame.OffersAction(action, options)).ToHashSet();
    }
}
