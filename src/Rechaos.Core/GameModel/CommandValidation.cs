namespace Rechaos.Core.GameModel;

public enum CommandValidationCode
{
    Valid,
    InvalidPhase,
    InactivePlayer,
    PlayerEliminated,
    GangNotFound,
    GangNotOwned,
    GangEliminated,
    InvalidTargetKind,
    TargetNotFound,
    CannotTargetSelf,
    TargetNotFriendly,
    TargetNotEnemy,
    TargetOutsideSector,
    DestinationNotAdjacent,
    ItemAlreadyResearched,
    SiteAlreadyInfluenced,
    ItemNotResearched,
    InsufficientTechLevel,
    ItemNotEquipped,
    ItemAlreadyEquipped,
    DestinationAtCapacity,
    SectorNotControlled,
    CommandNotQueued,
    ResearchTechLevelUnavailable,
    SectorInCrackdown,
    SectorAlreadyControlled,
    GangAtFullForce
}

public readonly record struct CommandValidation(CommandValidationCode Code, string Message)
{
    public bool IsValid => Code == CommandValidationCode.Valid;
    public static CommandValidation Valid() => new(CommandValidationCode.Valid, string.Empty);
    public static CommandValidation Reject(CommandValidationCode code) => new(code, CommandValidationMessages.For(code));
}

public sealed record CommandSubmissionResult(CommandValidation Validation, GameEvent? Event)
{
    public bool Accepted => Validation.IsValid;
}

public enum GangTargetRelationship : byte
{
    Any,
    Friendly,
    Enemy
}

public enum SpatialConstraint : byte
{
    None,
    SameSector,
    AdjacentSector
}

public sealed record CommandRule(
    GangAction Action,
    CommandTargetKind PrimaryTarget,
    CommandTargetKind SecondaryTarget = CommandTargetKind.None,
    GangTargetRelationship GangRelationship = GangTargetRelationship.Any,
    SpatialConstraint SpatialConstraint = SpatialConstraint.None,
    int CashCost = 0);

/// <summary>
/// Declarative command shapes. Exact costs and resolution formulas are added to
/// these descriptors only after reference evidence exists.
/// </summary>
public static class CommandRules
{
    public static readonly IReadOnlyDictionary<GangAction, CommandRule> ByAction = new CommandRule[]
    {
        new(GangAction.Attack, CommandTargetKind.Gang, GangRelationship: GangTargetRelationship.Enemy,
            SpatialConstraint: SpatialConstraint.SameSector),
        new(GangAction.Bribe, CommandTargetKind.None, CashCost: ManualRules.BribeCost),
        new(GangAction.Chaos, CommandTargetKind.None),
        new(GangAction.Control, CommandTargetKind.None),
        new(GangAction.Equip, CommandTargetKind.Item),
        new(GangAction.Give, CommandTargetKind.Gang, CommandTargetKind.Item, GangTargetRelationship.Friendly,
            SpatialConstraint.SameSector),
        new(GangAction.Heal, CommandTargetKind.None),
        new(GangAction.Hide, CommandTargetKind.None),
        new(GangAction.Influence, CommandTargetKind.Site, SpatialConstraint: SpatialConstraint.SameSector),
        new(GangAction.Move, CommandTargetKind.Sector, SpatialConstraint: SpatialConstraint.AdjacentSector),
        new(GangAction.Research, CommandTargetKind.Item),
        new(GangAction.Sell, CommandTargetKind.Item),
        new(GangAction.Snitch, CommandTargetKind.None),
        new(GangAction.Terminate, CommandTargetKind.None)
    }.ToDictionary(rule => rule.Action);
}

public static class CommandValidator
{
    public static CommandValidation Validate(MatchState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        var actorValidation = ValidateActor(state, command.Player, command.Gang);
        if (!actorValidation.IsValid) return actorValidation;
        if (!CommandRules.ByAction.TryGetValue(command.Action, out var rule))
            return CommandValidation.Reject(CommandValidationCode.InvalidTargetKind);
        if (!HasValidTargetShape(command, rule))
            return CommandValidation.Reject(CommandValidationCode.InvalidTargetKind);

        var actor = state.FindGang(command.Gang)!;
        var primaryValidation = ValidateTarget(state, actor, command.Target, rule);
        if (!primaryValidation.IsValid) return primaryValidation;
        var secondaryValidation = command.SecondaryTarget is { } secondary
            ? ValidateTarget(state, actor, secondary, rule with { SpatialConstraint = SpatialConstraint.None })
            : CommandValidation.Valid();
        if (!secondaryValidation.IsValid) return secondaryValidation;
        var tertiaryValidation = command.TertiaryTarget is { } tertiary
            ? ValidateTarget(state, actor, tertiary, rule with { SpatialConstraint = SpatialConstraint.None })
            : CommandValidation.Valid();
        if (!tertiaryValidation.IsValid) return tertiaryValidation;
        if (command.Action == GangAction.Research
            && state.FindPlayer(command.Player)!.ResearchedItems.Contains((short)command.Target.Id))
            return CommandValidation.Reject(CommandValidationCode.ItemAlreadyResearched);
        if (command.Action == GangAction.Research
            && state.Definitions.Items[command.Target.Id].TechLevel
                > SpecialSiteRules.ResearchTechLimit(state, actor))
            return CommandValidation.Reject(CommandValidationCode.ResearchTechLevelUnavailable);
        if (command.Action == GangAction.Influence)
        {
            var sector = state.Sectors[command.Target.Id / MatchLimits.SitesPerSector];
            if (sector.Owner != command.Player)
                return CommandValidation.Reject(CommandValidationCode.SectorNotControlled);
            if (state.FindSite(command.Target.Id)!.InfluencedBy is not null)
                return CommandValidation.Reject(CommandValidationCode.SiteAlreadyInfluenced);
        }
        if (command.Action == GangAction.Move
            && state.FindPlayer(command.Player)!.Gangs.Count(gang =>
                gang.IsActive && gang.SectorId == command.Target.Id) >= MatchLimits.FriendlyGangsPerSector)
            return CommandValidation.Reject(CommandValidationCode.DestinationAtCapacity);
        if (command.Action == GangAction.Control && state.Sectors[actor.SectorId].CrackdownActive)
            return CommandValidation.Reject(CommandValidationCode.SectorInCrackdown);
        if (command.Action == GangAction.Control && state.Sectors[actor.SectorId].Owner == command.Player)
            return CommandValidation.Reject(CommandValidationCode.SectorAlreadyControlled);
        if (command.Action == GangAction.Heal && actor.Force >= ManualRules.MaximumForce)
            return CommandValidation.Reject(CommandValidationCode.GangAtFullForce);
        return ValidateTransaction(state, command, actor);
    }

    private static bool HasValidTargetShape(GameCommand command, CommandRule rule)
    {
        if (command.Target.Kind != rule.PrimaryTarget) return false;
        if (command.Action == GangAction.Sell)
            return (command.SecondaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.TertiaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.TertiaryTarget is null || command.SecondaryTarget is not null)
                && command.SellTargets().Select(target => target.Id).Distinct().Count()
                    == command.SellTargets().Count();
        return (command.SecondaryTarget?.Kind ?? CommandTargetKind.None) == rule.SecondaryTarget
            && command.TertiaryTarget is null;
    }

    public static CommandValidation ValidateCancellation(MatchState state, PlayerId player, GangId gang) =>
        ValidateActor(state, player, gang);

    private static CommandValidation ValidateActor(MatchState state, PlayerId playerId, GangId gangId)
    {
        if (state.Coordinator.Phase != TurnPhase.Command)
            return CommandValidation.Reject(CommandValidationCode.InvalidPhase);
        if (state.Coordinator.ActivePlayer != playerId)
            return CommandValidation.Reject(CommandValidationCode.InactivePlayer);
        var player = state.FindPlayer(playerId);
        if (player is null)
            return CommandValidation.Reject(CommandValidationCode.InactivePlayer);
        if (player.Status != PlayerStatus.Active)
            return CommandValidation.Reject(CommandValidationCode.PlayerEliminated);
        var gang = state.FindGang(gangId);
        if (gang is null)
            return CommandValidation.Reject(CommandValidationCode.GangNotFound);
        if (gang.Owner != playerId)
            return CommandValidation.Reject(CommandValidationCode.GangNotOwned);
        return gang.IsActive
            ? CommandValidation.Valid()
            : CommandValidation.Reject(CommandValidationCode.GangEliminated);
    }

    private static CommandValidation ValidateTarget(
        MatchState state,
        MatchGangState actor,
        CommandTarget target,
        CommandRule rule) => target.Kind switch
        {
            CommandTargetKind.None => CommandValidation.Valid(),
            CommandTargetKind.Gang => ValidateGangTarget(state, actor, target, rule),
            CommandTargetKind.Sector => ValidateSectorTarget(state, actor, target, rule),
            CommandTargetKind.Site => ValidateSiteTarget(state, actor, target, rule),
            CommandTargetKind.Item => ValidateItemTarget(state, target),
            _ => CommandValidation.Reject(CommandValidationCode.InvalidTargetKind)
        };

    private static CommandValidation ValidateGangTarget(
        MatchState state,
        MatchGangState actor,
        CommandTarget target,
        CommandRule rule)
    {
        var targetGang = state.FindGang(new GangId(target.Id));
        if (targetGang is null || !targetGang.IsActive)
            return CommandValidation.Reject(CommandValidationCode.TargetNotFound);
        if (targetGang.Id == actor.Id)
            return CommandValidation.Reject(CommandValidationCode.CannotTargetSelf);
        if (rule.GangRelationship == GangTargetRelationship.Friendly && targetGang.Owner != actor.Owner)
            return CommandValidation.Reject(CommandValidationCode.TargetNotFriendly);
        if (rule.GangRelationship == GangTargetRelationship.Enemy && targetGang.Owner == actor.Owner)
            return CommandValidation.Reject(CommandValidationCode.TargetNotEnemy);
        return rule.SpatialConstraint == SpatialConstraint.SameSector && targetGang.SectorId != actor.SectorId
            ? CommandValidation.Reject(CommandValidationCode.TargetOutsideSector)
            : CommandValidation.Valid();
    }

    private static CommandValidation ValidateSectorTarget(
        MatchState state,
        MatchGangState actor,
        CommandTarget target,
        CommandRule rule)
    {
        if (target.Id < 0 || target.Id >= state.Sectors.Count)
            return CommandValidation.Reject(CommandValidationCode.TargetNotFound);
        if (rule.SpatialConstraint != SpatialConstraint.AdjacentSector)
            return CommandValidation.Valid();
        var sourceX = actor.SectorId % MatchLimits.BoardWidth;
        var sourceY = actor.SectorId / MatchLimits.BoardWidth;
        var targetX = target.Id % MatchLimits.BoardWidth;
        var targetY = target.Id / MatchLimits.BoardWidth;
        var deltaX = Math.Abs(sourceX - targetX);
        var deltaY = Math.Abs(sourceY - targetY);
        return Math.Max(deltaX, deltaY) == 1
            ? CommandValidation.Valid()
            : CommandValidation.Reject(CommandValidationCode.DestinationNotAdjacent);
    }

    private static CommandValidation ValidateSiteTarget(
        MatchState state,
        MatchGangState actor,
        CommandTarget target,
        CommandRule rule)
    {
        if (state.FindSite(target.Id) is null)
            return CommandValidation.Reject(CommandValidationCode.TargetNotFound);
        var sectorId = target.Id / MatchLimits.SitesPerSector;
        return rule.SpatialConstraint == SpatialConstraint.SameSector && sectorId != actor.SectorId
            ? CommandValidation.Reject(CommandValidationCode.TargetOutsideSector)
            : CommandValidation.Valid();
    }

    private static CommandValidation ValidateItemTarget(MatchState state, CommandTarget target) =>
        target.Id >= 0 && target.Id < state.Definitions.Items.Count && state.Definitions.Items[target.Id].Type != 99
            ? CommandValidation.Valid()
            : CommandValidation.Reject(CommandValidationCode.TargetNotFound);

    private static CommandValidation ValidateTransaction(
        MatchState state,
        GameCommand command,
        MatchGangState actor)
    {
        if (command.Action is not (GangAction.Equip or GangAction.Give or GangAction.Sell))
            return CommandValidation.Valid();
        if (command.Action == GangAction.Sell)
        {
            foreach (var target in command.SellTargets())
            {
                var selectedItem = checked((short)target.Id);
                var selectedSlot = EquipmentRules.SlotFor(state.Definitions.Items[selectedItem]);
                if (EquipmentRules.EquippedItem(actor, selectedSlot) != selectedItem)
                    return CommandValidation.Reject(CommandValidationCode.ItemNotEquipped);
            }
            return CommandValidation.Valid();
        }

        var itemIndex = checked((short)(command.Action == GangAction.Give
            ? command.SecondaryTarget!.Value.Id
            : command.Target.Id));
        var item = state.Definitions.Items[itemIndex];
        var slot = EquipmentRules.SlotFor(item);

        if (command.Action == GangAction.Equip)
        {
            var player = state.FindPlayer(command.Player)!;
            if (item.ResearchDifficulty > 0 && !player.ResearchedItems.Contains(itemIndex))
                return CommandValidation.Reject(CommandValidationCode.ItemNotResearched);
            if (!MeetsTechLevel(state, actor, item))
                return CommandValidation.Reject(CommandValidationCode.InsufficientTechLevel);
            return EquipmentRules.EquippedItem(actor, slot) == itemIndex
                ? CommandValidation.Reject(CommandValidationCode.ItemAlreadyEquipped)
                : CommandValidation.Valid();
        }

        if (EquipmentRules.EquippedItem(actor, slot) != itemIndex)
            return CommandValidation.Reject(CommandValidationCode.ItemNotEquipped);
        if (command.Action == GangAction.Give)
        {
            var recipient = state.FindGang(new GangId(command.Target.Id))!;
            if (!MeetsTechLevel(state, recipient, item))
                return CommandValidation.Reject(CommandValidationCode.InsufficientTechLevel);
        }
        return CommandValidation.Valid();
    }

    private static bool MeetsTechLevel(
        MatchState state,
        MatchGangState gang,
        Rechaos.Core.Assets.ItemDefinition item) =>
        state.Definitions.Gangs.Single(value => value.Id == gang.DefinitionId).TechLevel >= item.TechLevel;
}

internal static class CommandValidationMessages
{
    private static readonly IReadOnlyDictionary<CommandValidationCode, string> Messages =
        new Dictionary<CommandValidationCode, string>
        {
            [CommandValidationCode.InvalidPhase] = "Commands may only be changed during the Command phase.",
            [CommandValidationCode.InactivePlayer] = "The command player is not active.",
            [CommandValidationCode.PlayerEliminated] = "An eliminated player cannot issue commands.",
            [CommandValidationCode.GangNotFound] = "The acting gang does not exist.",
            [CommandValidationCode.GangNotOwned] = "The acting gang is not owned by the command player.",
            [CommandValidationCode.GangEliminated] = "An eliminated gang cannot issue commands.",
            [CommandValidationCode.InvalidTargetKind] = "The command target type is invalid for this action.",
            [CommandValidationCode.TargetNotFound] = "The command target does not exist.",
            [CommandValidationCode.CannotTargetSelf] = "A gang cannot target itself with this action.",
            [CommandValidationCode.TargetNotFriendly] = "The target must be a friendly gang.",
            [CommandValidationCode.TargetNotEnemy] = "The target must be an enemy gang.",
            [CommandValidationCode.TargetOutsideSector] = "The target must be in the acting gang's sector.",
            [CommandValidationCode.DestinationNotAdjacent] = "Movement requires a neighboring sector.",
            [CommandValidationCode.ItemAlreadyResearched] = "The targeted item has already been researched.",
            [CommandValidationCode.ResearchTechLevelUnavailable] = "The gang or its local research site cannot support that tech level.",
            [CommandValidationCode.SiteAlreadyInfluenced] = "The targeted site is already influenced.",
            [CommandValidationCode.ItemNotResearched] = "The item has not been researched.",
            [CommandValidationCode.InsufficientTechLevel] = "The gang's tech level is too low for this item.",
            [CommandValidationCode.ItemNotEquipped] = "The acting gang does not have that item equipped.",
            [CommandValidationCode.ItemAlreadyEquipped] = "The acting gang already has that item equipped.",
            [CommandValidationCode.DestinationAtCapacity] = "The destination already has the maximum friendly gangs.",
            [CommandValidationCode.SectorNotControlled] = "The player must control the target sector.",
            [CommandValidationCode.CommandNotQueued] = "The gang has no queued command.",
            [CommandValidationCode.SectorInCrackdown] = "A sector cannot be controlled while police are present.",
            [CommandValidationCode.SectorAlreadyControlled] = "The player already controls this sector.",
            [CommandValidationCode.GangAtFullForce] = "A gang at full Force does not need healing."
        };

    public static string For(CommandValidationCode code) => Messages.GetValueOrDefault(code, string.Empty);
}
