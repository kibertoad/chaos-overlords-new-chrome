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
    public static int ValueLeft => 394;
    public static int ContractCountLeft => 316;
    public static int ContractCountY => ValueY(0);
    public static Rectangle Panel => new(128, 124, 320, 209);
    public static Rectangle BackgroundSource => new(0, 0, 320, 209);
    public static Rectangle Portrait => new(154, 141, 64, 64);
    public static Rectangle Ok => new(161, 293, 49, 22);

    /// <summary>SCR-FINANCE-001, FND-FINANCE-002: the Sector variant's sector code, buffer (396,216).</summary>
    public static Point SectorName => new(180, 196);

    public static int ValueY(int row) => row switch
    {
        0 => SharedPanelLayout.Y(27),
        1 => SharedPanelLayout.Y(36),
        2 => SharedPanelLayout.Y(54),
        3 => SharedPanelLayout.Y(72),
        4 => SharedPanelLayout.Y(90),
        5 => SharedPanelLayout.Y(99),
        6 => SharedPanelLayout.Y(117),
        7 => SharedPanelLayout.Y(144),
        _ => throw new ArgumentOutOfRangeException(nameof(row))
    };

    public static Rectangle ValueField(int row) => new(
        ValueLeft, ValueY(row), 4 * OriginalFontLayout.CellWidth, OriginalFontLayout.GlyphHeight);

    public static int ContractCountWidth(int gangCount)
    {
        if (gangCount is < 0 or > 99) throw new ArgumentOutOfRangeException(nameof(gangCount));
        return gangCount < 10 ? 1 : 2;
    }

    public static int ContractCountCloseLeft(int gangCount) =>
        ContractCountLeft + ContractCountWidth(gangCount) * OriginalFontLayout.CellWidth;
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

        // FND-FINANCE-002: the City variant counts every active gang and queued hire; the Sector
        // variant counts the gangs standing in the sector and those moving into it, and gives back
        // the Upkeep of gangs moving out. Both give back the Upkeep of a terminating gang.
        bool Includes(int candidate) => sectorId is null || sectorId == candidate;
        var orders = state.Commands.ExecutionPlan()
            .Where(queued => queued.Command.Player == player.Id)
            .Select(queued => queued.Command)
            .ToDictionary(command => command.Gang);
        GangAction ActionOf(MatchGangState gang) =>
            orders.TryGetValue(gang.Id, out var command) ? command.Action : GangAction.None;
        bool MovesInto(MatchGangState gang) => sectorId is { } sector
            && orders.TryGetValue(gang.Id, out var command)
            && command.Action == GangAction.Move
            && command.Target.Id == sector;
        bool LeavesUpkeep(MatchGangState gang) => ActionOf(gang) == GangAction.Terminate
            || (sectorId is not null && ActionOf(gang) == GangAction.Move);

        var activeGangs = player.Gangs.Where(gang => gang.IsActive).ToArray();
        var counted = activeGangs
            .Where(gang => MovesInto(gang) || (Includes(gang.SectorId) && !LeavesUpkeep(gang)))
            .ToArray();
        var pendingHires = player.PendingHires.Where(hire => Includes(hire.TargetSectorId)).ToArray();
        var gangUpkeep = -counted.Sum(gang => state.Definitions.Gang(gang.DefinitionId).Upkeep)
            - pendingHires.Sum(hire => state.Definitions.Gang(hire.GangDefinitionId).Upkeep);
        var newContracts = -pendingHires.Sum(hire =>
            HireRules.InitialCost(state.Definitions.Gang(hire.GangDefinitionId)));
        var commands = activeGangs
            .Where(gang => Includes(gang.SectorId) && orders.ContainsKey(gang.Id))
            .Select(gang => orders[gang.Id])
            .ToArray();
        var equipment = commands.Sum(command => EquipmentAdjustment(state, command));
        var cityOfficials = -commands.Count(command => command.Action == GangAction.Bribe)
            * ManualRules.OriginalBribeCost;
        var sectors = state.Sectors.Where(sector => Includes(sector.Id)).ToArray();
        var sectorTax = sectors.Count(sector => sector.Owner == player.Id)
            * ManualRules.ControlledSectorTax;
        // RULE-UPKEEP-001: the sites' part of each owned sector's Cash yield (RULE-SITE-001).
        var siteProtection = sectors.Where(sector => sector.Owner == player.Id)
            .Sum(sector => SectorIncomeResolver.SectorCash(state, sector)
                - ManualRules.ControlledSectorTax);
        var chaosEstimate = commands
            .Where(command => command.Action == GangAction.Chaos)
            .Sum(command => EstimateChaos(state, player, state.FindGang(command.Gang)!, sectorId is null));
        var adjustment = checked(gangUpkeep + newContracts + equipment + cityOfficials
            + sectorTax + siteProtection + chaosEstimate);
        return new FinanceProjection(
            gangUpkeep, newContracts, counted.Length + pendingHires.Length,
            equipment, cityOfficials, sectorTax, siteProtection, chaosEstimate, adjustment);
    }

    private static int EquipmentAdjustment(MatchState state, GameCommand command)
    {
        return command.Action switch
        {
            GangAction.Equip when command.Target.Kind == CommandTargetKind.Item =>
                -SpecialSiteRules.EquipmentCost(
                    state,
                    state.FindGang(command.Gang)!,
                    state.Definitions.Items[command.Target.Id]),
            GangAction.Sell => SellAdjustment(state, command),
            _ => 0
        };
    }

    private static int SellAdjustment(MatchState state, GameCommand command)
    {
        var credited = command.SellTargets()
            .Select(target => state.Definitions.Items[target.Id])
            .OrderBy(EquipmentRules.SlotFor)
            .Last();
        return EquipmentRules.SaleValue(credited);
    }

    // FND-FINANCE-002: a third of Income + Chaos + Force for each Chaos gang, halved outside the
    // player's sectors; both divisions truncate toward zero. The City variant drops an estimate
    // that is not above 0 and the Sector variant adds it as it is.
    private static int EstimateChaos(
        MatchState state,
        MatchPlayerState player,
        MatchGangState gang,
        bool city)
    {
        var sector = state.Sectors[gang.SectorId];
        var estimate = (sector.Income + EffectiveStatisticsCalculator.ForGang(state, gang).Chaos
            + gang.Force) / 3;
        if (city && estimate <= 0) return 0;
        return sector.Owner == player.Id ? estimate : estimate / 2;
    }
}
