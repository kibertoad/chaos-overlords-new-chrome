using System.Runtime.CompilerServices;
using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Projects every currently legal command instance for one gang by expanding
/// declarative target shapes and applying the authoritative validator.
/// Presentation and AI may consume this without duplicating rule logic.
/// </summary>
public static class CommandOptionCatalog
{
    /// <summary>Every rule in action order, the order the options are listed in.</summary>
    private static readonly CommandRule[] RulesInActionOrder =
        CommandRules.ByAction.Values.OrderBy(rule => rule.Action).ToArray();

    public static IReadOnlyList<GameCommand> LegalCommands(
        MatchState state,
        PlayerId player,
        GangId gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        var options = new List<GameCommand>();
        // Every candidate shares the actor, and the validator rejects all of them when it rejects
        // the actor, so that check runs once here instead of once per candidate.
        if (!CommandValidator.ValidateActor(state, player, gang, out var actor).IsValid)
            return options;
        var targets = new TargetLists(state);
        foreach (var rule in RulesInActionOrder)
        {
            foreach (var primary in targets.For(rule.PrimaryTarget))
            {
                if (rule.SecondaryTarget == CommandTargetKind.None)
                {
                    Consider(state, actor!, options, new GameCommand(player, gang, rule.Action, primary));
                    continue;
                }
                // A rejected primary target rejects every command built on it, so the secondary
                // product is expanded only for primaries that pass.
                if (!CommandValidator.AcceptsPrimaryTarget(state, actor!, primary, rule)) continue;
                foreach (var secondary in targets.For(rule.SecondaryTarget))
                {
                    Consider(state, actor!, options, new GameCommand(
                        player, gang, rule.Action, primary, SecondaryTarget: secondary));
                }
            }
        }
        return options;
    }

    private static void Consider(
        MatchState state,
        MatchGangState actor,
        List<GameCommand> options,
        GameCommand command)
    {
        if (CommandValidator.ValidateForActor(state, command, actor).IsValid) options.Add(command);
    }

    /// <summary>
    /// The candidate targets of each kind, each list built at most once per call.
    /// </summary>
    /// <remarks>
    /// The rules are expanded as a product of a primary and a secondary target list, and the
    /// secondary list used to be rebuilt, sorted, for every primary target: Give alone re-sorted
    /// the item definitions once per gang on the board, for every gang planned, every turn. A
    /// state's sectors and sites never change, and the item list depends only on the definitions.
    /// </remarks>
    private sealed class TargetLists(MatchState state)
    {
        private static readonly CommandTarget[] NoTarget = [CommandTarget.None];
        private static readonly ConditionalWeakTable<OriginalData, CommandTarget[]> ItemTargets = [];

        private CommandTarget[]? _gangs;
        private CommandTarget[]? _sectors;
        private CommandTarget[]? _sites;

        public CommandTarget[] For(CommandTargetKind kind) => kind switch
        {
            CommandTargetKind.None => NoTarget,
            CommandTargetKind.Gang => _gangs ??= state.Players.SelectMany(player => player.Gangs)
                .OrderBy(gang => gang.Id.Value).Select(gang => CommandTarget.Gang(gang.Id)).ToArray(),
            CommandTargetKind.Sector => _sectors ??= state.Sectors.OrderBy(sector => sector.Id)
                .Select(sector => CommandTarget.Sector(sector.Id)).ToArray(),
            CommandTargetKind.Site => _sites ??= state.Sectors.OrderBy(sector => sector.Id)
                .SelectMany(sector => sector.Sites.OrderBy(site => site.Slot)
                    .Select(site => CommandTarget.Site(
                        sector.Id * MatchLimits.SitesPerSector + site.Slot)))
                .ToArray(),
            CommandTargetKind.Item => ItemTargets.GetValue(state.Definitions, static definitions =>
                definitions.Items.Where(item => item.Type != 99).OrderBy(item => item.Id)
                    .Select(item => CommandTarget.Item(item.Id)).ToArray()),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }
}
