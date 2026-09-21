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
    GangAtFullForce,
    ActionCannotRepeat,
    TargetUndetected
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
    private static readonly IReadOnlySet<GangAction> RepeatableActions = new HashSet<GangAction>
    {
        GangAction.Chaos,
        GangAction.Control,
        GangAction.Heal,
        GangAction.Hide,
        GangAction.Influence,
        GangAction.Research
    };

    public static readonly IReadOnlyDictionary<GangAction, CommandRule> ByAction = new CommandRule[]
    {
        new(GangAction.Attack, CommandTargetKind.Gang, GangRelationship: GangTargetRelationship.Enemy,
            SpatialConstraint: SpatialConstraint.SameSector),
        new(GangAction.Bribe, CommandTargetKind.None, CashCost: ManualRules.OriginalBribeCost),
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

    public static bool CanRepeat(GangAction action) => RepeatableActions.Contains(action);
}

public static class CommandValidator
{
    public static CommandValidation Validate(MatchState state, GameCommand command)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        var actorValidation = ValidateActor(state, command.Player, command.Gang, out var actor);
        return actorValidation.IsValid
            ? ValidateForActor(state, command, actor!)
            : actorValidation;
    }

    /// <summary>
    /// Everything <see cref="Validate"/> checks after the actor, for a caller that has already
    /// validated the actor and holds it: the option catalog validates hundreds of candidate
    /// commands for one gang, and looking the same gang up for each was most of its time.
    /// </summary>
    internal static CommandValidation ValidateForActor(
        MatchState state,
        GameCommand command,
        MatchGangState actor)
    {
        if (!CommandRules.ByAction.TryGetValue(command.Action, out var rule))
            return CommandValidation.Reject(CommandValidationCode.InvalidTargetKind);
        if (command.Repeat && !CommandRules.CanRepeat(command.Action))
            return CommandValidation.Reject(CommandValidationCode.ActionCannotRepeat);
        if (!HasValidTargetShape(command, rule))
            return CommandValidation.Reject(CommandValidationCode.InvalidTargetKind);

        var primaryValidation = ValidateTarget(state, actor, command.Target, rule);
        if (!primaryValidation.IsValid) return primaryValidation;
        var unconstrained = UnconstrainedRules[command.Action];
        var secondaryValidation = command.SecondaryTarget is { } secondary
            ? ValidateTarget(state, actor, secondary, unconstrained)
            : CommandValidation.Valid();
        if (!secondaryValidation.IsValid) return secondaryValidation;
        var tertiaryValidation = command.TertiaryTarget is { } tertiary
            ? ValidateTarget(state, actor, tertiary, unconstrained)
            : CommandValidation.Valid();
        if (!tertiaryValidation.IsValid) return tertiaryValidation;
        var quaternaryValidation = command.QuaternaryTarget is { } quaternary
            ? ValidateTarget(state, actor, quaternary, unconstrained)
            : CommandValidation.Valid();
        if (!quaternaryValidation.IsValid) return quaternaryValidation;
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
            var site = state.FindSite(command.Target.Id)!;
            if (site.Resistance == 0 || site.InfluencedBy is not null)
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

    /// <summary>
    /// The secondary and later targets of a command are validated without the rule's spatial
    /// constraint, which applies to the primary target only. Built once per action rather than
    /// with a record copy per validated target.
    /// </summary>
    private static readonly IReadOnlyDictionary<GangAction, CommandRule> UnconstrainedRules =
        CommandRules.ByAction.ToDictionary(
            pair => pair.Key,
            pair => pair.Value with { SpatialConstraint = SpatialConstraint.None });

    private static bool HasValidTargetShape(GameCommand command, CommandRule rule)
    {
        if (command.Target.Kind != rule.PrimaryTarget) return false;
        if (command.Action == GangAction.Sell)
            return (command.SecondaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.TertiaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.TertiaryTarget is null || command.SecondaryTarget is not null)
                && command.QuaternaryTarget is null
                && AreDistinctIds(command.Target, command.SecondaryTarget, command.TertiaryTarget);
        if (command.Action == GangAction.Give)
            return command.SecondaryTarget?.Kind == CommandTargetKind.Item
                && (command.TertiaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.QuaternaryTarget?.Kind ?? CommandTargetKind.Item) == CommandTargetKind.Item
                && (command.QuaternaryTarget is null || command.TertiaryTarget is not null)
                && AreDistinctIds(command.SecondaryTarget, command.TertiaryTarget, command.QuaternaryTarget);
        return (command.SecondaryTarget?.Kind ?? CommandTargetKind.None) == rule.SecondaryTarget
            && command.TertiaryTarget is null && command.QuaternaryTarget is null;
    }

    /// <summary>Whether the identifiers of the present targets are pairwise distinct.</summary>
    private static bool AreDistinctIds(CommandTarget? first, CommandTarget? second, CommandTarget? third) =>
        (first is not { } a || second is not { } b || a.Id != b.Id)
        && (first is not { } c || third is not { } d || c.Id != d.Id)
        && (second is not { } e || third is not { } f || e.Id != f.Id);

    /// <summary>
    /// Whether <paramref name="target"/> can be the primary target of an <paramref name="actor"/>
    /// command under <paramref name="rule"/>. A command whose primary target fails here fails
    /// <see cref="Validate"/> whatever its other targets are, which lets the option catalog skip
    /// expanding the secondary targets of a primary that is already rejected.
    /// </summary>
    internal static bool AcceptsPrimaryTarget(
        MatchState state,
        MatchGangState actor,
        CommandTarget target,
        CommandRule rule) =>
        target.Kind == rule.PrimaryTarget && ValidateTarget(state, actor, target, rule).IsValid;

    public static CommandValidation ValidateCancellation(MatchState state, PlayerId player, GangId gang) =>
        ValidateActor(state, player, gang, out _);

    /// <summary>Whether <paramref name="playerId"/> may command <paramref name="gangId"/> now; the gang when so.</summary>
    internal static CommandValidation ValidateActor(
        MatchState state,
        PlayerId playerId,
        GangId gangId,
        out MatchGangState? actor)
    {
        actor = null;
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
        if (!gang.IsActive)
            return CommandValidation.Reject(CommandValidationCode.GangEliminated);
        actor = gang;
        return CommandValidation.Valid();
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
        if (rule.SpatialConstraint == SpatialConstraint.SameSector && targetGang.SectorId != actor.SectorId)
            return CommandValidation.Reject(CommandValidationCode.TargetOutsideSector);
        return rule.Action == GangAction.Attack
            && !state.CanPlayerDetectGang(actor.Owner, targetGang.Id)
                ? CommandValidation.Reject(CommandValidationCode.TargetUndetected)
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

        if (command.Action == GangAction.Give)
        {
            var recipient = state.FindGang(new GangId(command.Target.Id))!;
            foreach (var target in command.GiveTargets())
            {
                var selectedItem = checked((short)target.Id);
                var selectedDefinition = state.Definitions.Items[selectedItem];
                var selectedSlot = EquipmentRules.SlotFor(selectedDefinition);
                if (EquipmentRules.EquippedItem(actor, selectedSlot) != selectedItem)
                    return CommandValidation.Reject(CommandValidationCode.ItemNotEquipped);
                if (!MeetsTechLevel(state, recipient, selectedDefinition))
                    return CommandValidation.Reject(CommandValidationCode.InsufficientTechLevel);
            }
            return CommandValidation.Valid();
        }

        var itemIndex = checked((short)command.Target.Id);
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
            [CommandValidationCode.InvalidPhase] = "Commands require Command phase.",
            [CommandValidationCode.InactivePlayer] = "Command player is not active.",
            [CommandValidationCode.PlayerEliminated] = "Eliminated player cannot act.",
            [CommandValidationCode.GangNotFound] = "Acting gang does not exist.",
            [CommandValidationCode.GangNotOwned] = "Gang is not owned by player.",
            [CommandValidationCode.GangEliminated] = "Eliminated gang cannot act.",
            [CommandValidationCode.InvalidTargetKind] = "Invalid target for this action.",
            [CommandValidationCode.TargetNotFound] = "Command target does not exist.",
            [CommandValidationCode.CannotTargetSelf] = "Gang cannot target itself.",
            [CommandValidationCode.TargetNotFriendly] = "Target must be friendly.",
            [CommandValidationCode.TargetNotEnemy] = "Target must be an enemy.",
            [CommandValidationCode.TargetOutsideSector] = "Target must be in gang sector.",
            [CommandValidationCode.DestinationNotAdjacent] = "Move requires neighbor sector.",
            [CommandValidationCode.ItemAlreadyResearched] = "Item is already researched.",
            [CommandValidationCode.ResearchTechLevelUnavailable] = "Research tech level too low.",
            [CommandValidationCode.SiteAlreadyInfluenced] = "Site is already influenced.",
            [CommandValidationCode.ItemNotResearched] = "Item has not been researched.",
            [CommandValidationCode.InsufficientTechLevel] = "Gang tech too low for item.",
            [CommandValidationCode.ItemNotEquipped] = "Gang does not have that item.",
            [CommandValidationCode.ItemAlreadyEquipped] = "Gang already has that item.",
            [CommandValidationCode.DestinationAtCapacity] = "Destination gang limit reached.",
            [CommandValidationCode.SectorNotControlled] = "Target sector is not controlled.",
            [CommandValidationCode.CommandNotQueued] = "Gang has no queued command.",
            [CommandValidationCode.SectorInCrackdown] = "Police block control attempt.",
            [CommandValidationCode.SectorAlreadyControlled] = "Player already controls sector.",
            [CommandValidationCode.GangAtFullForce] = "Gang already at full Force.",
            [CommandValidationCode.ActionCannotRepeat] = "Action cannot be recurring.",
            [CommandValidationCode.TargetUndetected] = "Attack target is hidden."
        };

    public static string For(CommandValidationCode code) => Messages.GetValueOrDefault(code, string.Empty);
}
