using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum FinanceScope
{
    City,
    Sector
}

public static class FinanceLayout
{
    public const int RowCount = 8;
    public const int ValueRight = 393;
    public const int ContractCountLeft = 293;
    public static Rectangle Panel => EquipmentCommandLayout.Panel;
    public static Rectangle Portrait => new(130, 143, 64, 64);
    public static Rectangle Ok => EquipmentCommandLayout.Ok;

    public static int ValueY(int row) => row switch
    {
        0 => 152,
        1 => 161,
        2 => 179,
        3 => 197,
        4 => 215,
        5 => 224,
        6 => 242,
        7 => 269,
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };
}

public sealed record FinanceProjection(
    int GangUpkeep,
    int NewContracts,
    int ProjectedGangCount,
    int Equipment,
    int CityOfficials,
    int SectorTax,
    int SiteProtection,
    int ChaosEstimate,
    int CashAdjustment)
{
    public static FinanceProjection Project(
        MatchState state,
        MatchPlayerState player,
        int? sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(player);
        if (state.FindPlayer(player.Id) != player)
            throw new ArgumentException("Player does not belong to the match.", nameof(player));
        if (sectorId is < 0 or >= MatchLimits.SectorCount)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        bool Includes(int candidate) => sectorId is null || sectorId == candidate;
        var activeGangs = player.Gangs.Where(gang => gang.IsActive && Includes(gang.SectorId)).ToArray();
        var pendingHires = player.PendingHires.Where(hire => Includes(hire.TargetSectorId)).ToArray();
        var gangUpkeep = -activeGangs.Sum(gang => state.Definitions.Gangs
            .Single(definition => definition.Id == gang.DefinitionId).Upkeep)
            - pendingHires.Sum(hire => state.Definitions.Gangs
                .Single(definition => definition.Id == hire.GangDefinitionId).Upkeep);
        var newContracts = -pendingHires.Sum(hire => HireRules.InitialCost(state.Definitions.Gangs
            .Single(definition => definition.Id == hire.GangDefinitionId)));
        var commands = state.Commands.ExecutionPlan()
            .Where(queued => queued.Command.Player == player.Id)
            .Where(queued => state.FindGang(queued.Command.Gang) is { } gang && Includes(gang.SectorId))
            .Select(queued => queued.Command)
            .ToArray();
        var equipment = commands.Sum(command => EquipmentAdjustment(state, command));
        var cityOfficials = -commands.Count(command => command.Action == GangAction.Bribe)
            * ManualRules.OriginalBribeCost;
        var sectors = state.Sectors.Where(sector => Includes(sector.Id)).ToArray();
        var sectorTax = sectors.Count(sector => sector.Owner == player.Id)
            * ManualRules.ControlledSectorTax;
        var siteProtection = sectors.SelectMany(sector => sector.Sites)
            .Where(site => site.InfluencedBy == player.Id)
            .Sum(site => state.Definitions.Sites.Single(
                definition => definition.Id == site.DefinitionId).Cash);
        var chaosEstimate = EstimateChaos(state, player, commands);
        var adjustment = checked(gangUpkeep + newContracts + equipment + cityOfficials
            + sectorTax + siteProtection + chaosEstimate);
        return new FinanceProjection(
            gangUpkeep, newContracts, activeGangs.Length + pendingHires.Length,
            equipment, cityOfficials, sectorTax, siteProtection, chaosEstimate, adjustment);
    }

    private static int EquipmentAdjustment(MatchState state, GameCommand command)
    {
        var item = command.Target.Kind == CommandTargetKind.Item
            ? state.Definitions.Items[command.Target.Id]
            : null;
        return command.Action switch
        {
            GangAction.Equip when item is not null => -SpecialSiteRules.EquipmentCost(
                state, state.FindGang(command.Gang)!, item),
            GangAction.Sell when item is not null => EquipmentRules.SaleValue(item),
            _ => 0
        };
    }

    private static int EstimateChaos(
        MatchState state,
        MatchPlayerState player,
        IReadOnlyList<GameCommand> commands) => commands
        .Where(command => command.Action == GangAction.Chaos)
        .GroupBy(command => state.FindGang(command.Gang)!.SectorId)
        .Sum(group =>
        {
            var sector = state.Sectors[group.Key];
            if (sector.CrackdownActive) return 0;
            var dice = group.Sum(command =>
            {
                var gang = state.FindGang(command.Gang)!;
                var pool = sector.Income + gang.Force
                    + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos;
                return player.Setup.Controller == PlayerController.Computer
                    && state.Setup.AiMentality == AiDifficulty.Goon
                    ? pool - pool / 5
                    : pool;
            });
            var successFaces = player.Setup.Controller == PlayerController.Computer
                && state.Setup.AiMentality is AiDifficulty.CrimeLord or AiDifficulty.HomicidalManiac
                ? 3
                : 2;
            var expectedSuccesses = dice * successFaces / 6;
            return ManualRules.ChaosIncome(expectedSuccesses, sector.Owner == player.Id);
        });
}
