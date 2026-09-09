namespace Rechaos.Core.GameModel;

/// <summary>
/// Projects every currently legal command instance for one gang by expanding
/// declarative target shapes and applying the authoritative validator.
/// Presentation and AI may consume this without duplicating rule logic.
/// </summary>
public static class CommandOptionCatalog
{
    public static IReadOnlyList<GameCommand> LegalCommands(
        MatchState state,
        PlayerId player,
        GangId gang)
    {
        ArgumentNullException.ThrowIfNull(state);
        var options = new List<GameCommand>();
        foreach (var rule in CommandRules.ByAction.Values.OrderBy(rule => rule.Action))
        {
            foreach (var command in Expand(state, player, gang, rule))
            {
                if (CommandValidator.Validate(state, command).IsValid) options.Add(command);
            }
        }
        return options;
    }

    private static IEnumerable<GameCommand> Expand(
        MatchState state,
        PlayerId player,
        GangId gang,
        CommandRule rule)
    {
        foreach (var primary in Targets(state, rule.PrimaryTarget))
        {
            if (rule.SecondaryTarget == CommandTargetKind.None)
            {
                yield return new GameCommand(player, gang, rule.Action, primary);
                continue;
            }
            foreach (var secondary in Targets(state, rule.SecondaryTarget))
                yield return new GameCommand(player, gang, rule.Action, primary, SecondaryTarget: secondary);
        }
    }

    private static IEnumerable<CommandTarget> Targets(MatchState state, CommandTargetKind kind) => kind switch
    {
        CommandTargetKind.None => [CommandTarget.None],
        CommandTargetKind.Gang => state.Players.SelectMany(player => player.Gangs)
            .OrderBy(gang => gang.Id.Value).Select(gang => CommandTarget.Gang(gang.Id)),
        CommandTargetKind.Sector => state.Sectors.OrderBy(sector => sector.Id)
            .Select(sector => CommandTarget.Sector(sector.Id)),
        CommandTargetKind.Site => state.Sectors.OrderBy(sector => sector.Id)
            .SelectMany(sector => sector.Sites.OrderBy(site => site.Slot)
                .Select(site => CommandTarget.Site(
                    sector.Id * MatchLimits.SitesPerSector + site.Slot))),
        CommandTargetKind.Item => state.Definitions.Items
            .Where(item => item.Type != 99).OrderBy(item => item.Id)
            .Select(item => CommandTarget.Item(item.Id)),
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };
}
