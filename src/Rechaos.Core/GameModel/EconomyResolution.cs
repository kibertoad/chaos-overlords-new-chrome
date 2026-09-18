namespace Rechaos.Core.GameModel;

public sealed record UpkeepResolutionResult(
    PlayerId Player,
    EconomyResolutionDetails Details,
    GameEvent Event);

public sealed record EconomyForecast(
    int CurrentCash,
    int SectorIncome,
    int SiteIncome,
    int GangUpkeep,
    int ResultCash)
{
    public int NetChange => ResultCash - CurrentCash;
    public bool IsInDebt => ResultCash < 0;
}

/// <summary>
/// Shipped-executable Upkeep calculation, projected through separate sector-tax
/// and influenced-site components rather than the original combined sector byte.
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
            var forecast = Project(state, player);
            var previousCash = forecast.CurrentCash;
            player.Cash = forecast.ResultCash;
            RecordStatistics(state, player);
            var details = new EconomyResolutionDetails(
                previousCash, forecast.SectorIncome, forecast.SiteIncome,
                forecast.GangUpkeep, forecast.ResultCash);
            var gameEvent = state.AppendUpkeepEvent(player.Id, details);
            state.QueueNotification(
                player.Id,
                GameNotificationKind.Economy,
                relatedEventSequence: gameEvent.Sequence);
            results.Add(new UpkeepResolutionResult(player.Id, details, gameEvent));
        }
        return results;
    }

    private static void RecordStatistics(MatchState state, MatchPlayerState player)
    {
        foreach (var gang in player.Gangs.Where(gang => gang.IsActive))
        {
            var upkeep = state.Definitions.Gangs
                .Single(definition => definition.Id == gang.DefinitionId).Upkeep;
            if (upkeep < 0)
                player.Statistics.CashEarned = checked(player.Statistics.CashEarned - upkeep);
            else
                player.Statistics.CashSpent = checked(player.Statistics.CashSpent + upkeep);
        }

        foreach (var sector in state.Sectors.Where(sector => sector.Owner == player.Id))
        {
            var income = SectorIncomeResolver.SectorCash(state, sector);
            if (income < 1)
                player.Statistics.CashSpent = checked(player.Statistics.CashSpent - income);
            else
                player.Statistics.CashEarned = checked(player.Statistics.CashEarned + income);
        }
    }

    public static EconomyForecast Project(MatchState state, MatchPlayerState player)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));
        var previousCash = player.Cash;
        var sectorIncome = state.Sectors.Count(sector => sector.Owner == player.Id)
            * ManualRules.ControlledSectorTax;
        var siteIncome = state.Sectors.SelectMany(sector => sector.Sites)
            .Where(site => site.InfluencedBy == player.Id)
            .Sum(site => state.Definitions.Sites.Single(definition => definition.Id == site.DefinitionId).Cash);
        var gangUpkeep = player.Gangs.Where(gang => gang.IsActive)
            .Sum(gang => state.Definitions.Gangs.Single(definition => definition.Id == gang.DefinitionId).Upkeep);
        return new EconomyForecast(previousCash, sectorIncome, siteIncome, gangUpkeep,
            checked(previousCash + sectorIncome + siteIncome - gangUpkeep));
    }
}
