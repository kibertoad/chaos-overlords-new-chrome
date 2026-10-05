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
        // A run whose match ended takes its steps on the endgame (SCR-AWARDS-001), and one with a
        // human eliminated on that human's elimination card (SCR-OBJECTIVE-002, controller -2): no order
        // menu opens there, and the order bytes are those the last resolution left.
        foreach (var (experiment, runs) in Recorded.Value)
            for (var run = 0; run < runs.Length; run++)
                if (runs[run].OrderSteps.Count > 0 && runs[run].EndgameRows is null
                    && runs[run].Humans.All(human => runs[run].Term("controller", human.Value) != -2)
                    && !KnownDivergences.ContainsKey((experiment, run)))
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
            var viewed = owner ?? human;
            if (sector is not null && step.Viewed >= 0)
                Assert.True(step.Viewed == viewed.Value,
                    $"after {label}: the original lists player {step.Viewed}'s gangs, the rebuild player {viewed.Value}'s");
            IReadOnlyList<MatchGangState> cards = sector is { } shown
                ? SectorOpponentGangs.InSector(match, human, viewed, shown)
                : [];
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
                "card" => viewed == human ? CardMenu(cards, step) : (-1, []),
                "strip" => viewed == human ? StripMenu(cards, step) : (-1, []),
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
    // one-off part and menu 2 for its recurring part.
    private static (int Menu, IReadOnlyList<MatchGangState> Gangs) CardMenu(
        IReadOnlyList<MatchGangState> cards, RecordedOrderStep step)
    {
        var point = new Point(
            SectorGangCardLayout.Left + step.Target % 2 * SectorGangCardLayout.ColumnStride + step.X,
            SectorGangCardLayout.Top + step.Target / 2 * SectorGangCardLayout.RowStride + step.Y);
        var card = SectorGangCardLayout.CardAt(point);
        if (card < 0 || card >= cards.Count) return (-1, []);
        return SectorGangCardLayout.ActionRepeatAt(card, point) switch
        {
            false => (1, [cards[card]]),
            true => (2, [cards[card]]),
            null => (-1, []),
        };
    }

    // SCR-UI-004, FND-UI-015, FND-UI-021: the group order strip, drawn over two or more cards,
    // opens menu 3 on its left part and menu 5 on its right.
    private static (int Menu, IReadOnlyList<MatchGangState> Gangs) StripMenu(
        IReadOnlyList<MatchGangState> cards, RecordedOrderStep step)
    {
        var point = new Point(step.X, step.Y);
        if (cards.Count < 2 || !SectorDetailLayout.GroupOrderStrip.Contains(point)) return (-1, []);
        return (SectorDetailLayout.GroupOrderIsRecurring(point) ? 5 : 3, cards);
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
            foreach (var gang in gangs)
                if (gang.QueuedCommand is not null) Assert.True(match.Cancel(human, gang.Id).Accepted);
            return;
        }
        if (menu is 1 or 2)
        {
            // FND-UI-021: these orders first run their picker panel and are dropped when it is
            // cancelled, so the order is the picker's; the gangs compared after the picker's own
            // steps show whether it gave one (EXP-UI-009 cancels each).
            if (menu == 1 && CommandOverlayLayout.OpensTargetPicker(action)) return;
            var result = match.Submit(new GameCommand(human, gangs[0].Id, action, CommandTarget.None, recurring));
            Assert.True(result.Accepted, $"the rebuild refused {action}: {result}");
            return;
        }
        var plan = BulkGangCommands.Plan(match, human, gangs.Select(gang => gang.Id).ToArray(),
            new BulkCommandIntent(action, CommandTarget.None, recurring), group: true);
        foreach (var command in plan.Commands) Assert.True(match.Submit(command).Accepted);
    }

    // FND-UI-021, DEV-UI-021: the orders the rebuild's panel offers where the original's menu
    // leaves an item enabled. None is always offered.
    private static IReadOnlySet<GangAction> Offered(MatchState match, PlayerId human, int menu, IReadOnlyList<MatchGangState> gangs)
    {
        var recurring = menu is 2 or 5;
        var options = menu is 1 or 2
            ? CommandOptionCatalog.LegalCommands(match, human, gangs[0].Id)
                .Where(command => !recurring || CommandRules.CanRepeat(command.Action)).ToArray()
            : BulkGangCommands.Options(match, human, gangs.Select(gang => gang.Id).ToArray(), recurring, group: true);
        var offered = options.Select(command => command.Action).ToHashSet();
        offered.Add(GangAction.None);
        if (menu is 1 or 2)
            offered.UnionWith(CommandOverlayLayout.ActionsFor(recurring).Where(CommandOverlayLayout.OpensWithoutTargets));
        return offered;
    }
}
