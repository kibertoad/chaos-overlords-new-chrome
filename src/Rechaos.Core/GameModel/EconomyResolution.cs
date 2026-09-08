namespace Rechaos.Core.GameModel;

public sealed record UpkeepResolutionResult(
    PlayerId Player,
    EconomyResolutionDetails Details,
    GameEvent Event);

/// <summary>
/// Manual-backed Upkeep calculation. Desertion and special modifiers remain
/// excluded until their original ordering and behavior are recovered.
/// </summary>
public static class EconomyResolver
{
    public static IReadOnlyList<UpkeepResolutionResult> ResolveUpkeep(MatchState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Coordinator.Phase != TurnPhase.Upkeep)
            throw new InvalidOperationException($"Cannot resolve Upkeep while in {state.Coordinator.Phase}.");

        var results = new List<UpkeepResolutionResult>();
        foreach (var player in state.Players.Where(item => item.Status == PlayerStatus.Active).OrderBy(item => item.Id.Value))
        {
            var previousCash = player.Cash;
            var sectorIncome = state.Sectors.Count(sector => sector.Owner == player.Id)
                * ManualRules.ControlledSectorTax;
            var siteIncome = state.Sectors.SelectMany(sector => sector.Sites)
                .Where(site => site.InfluencedBy == player.Id)
                .Sum(site => state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Cash);
            var gangUpkeep = player.Gangs.Where(gang => gang.IsActive)
                .Sum(gang => state.Definitions.Gangs.Single(definition => definition.Id == gang.DefinitionId).Upkeep);
            player.Cash = Math.Max(0, previousCash + sectorIncome + siteIncome - gangUpkeep);

            var details = new EconomyResolutionDetails(
                previousCash, sectorIncome, siteIncome, gangUpkeep, player.Cash);
            var gameEvent = state.AppendUpkeepEvent(player.Id, details);
            state.QueueNotification(
                player.Id,
                GameNotificationKind.Economy,
                relatedEventSequence: gameEvent.Sequence);
            results.Add(new UpkeepResolutionResult(player.Id, details, gameEvent));
        }
        return results;
    }
}
