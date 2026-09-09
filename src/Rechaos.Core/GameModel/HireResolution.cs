using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

public enum HireValidationCode : byte
{
    Valid,
    InvalidPhase,
    InactivePlayer,
    PlayerEliminated,
    OfferUnavailable,
    OfferAlreadySnubbed,
    HireAlreadyPending,
    InsufficientCash,
    SectorNotControlled,
    GangCapacityReached,
    SectorCapacityReached
}

public readonly record struct HireValidation(HireValidationCode Code, string Message)
{
    public bool IsValid => Code == HireValidationCode.Valid;
    public static HireValidation Accept() => new(HireValidationCode.Valid, string.Empty);
}

public sealed record HireSubmissionResult(
    HireValidation Validation,
    PendingHireState? PendingHire = null,
    GameEvent? Event = null)
{
    public bool Accepted => Validation.IsValid;
}

public sealed record HireOfferSnubResult(
    HireValidation Validation,
    short? GangDefinitionId = null,
    GameEvent? Event = null)
{
    public bool Accepted => Validation.IsValid;
}

public sealed record HireResolutionResult(
    PlayerId Player,
    short GangDefinitionId,
    int SectorId,
    int Cost,
    GangId Gang,
    short? ReplacementOffer,
    int InitialForce,
    GameEvent Event);

public static class HireRules
{
    private sealed record Context(
        MatchState State,
        PlayerId PlayerId,
        short GangDefinitionId,
        int TargetSectorId)
    {
        public MatchPlayerState? Player => State.FindPlayer(PlayerId);
    }

    private static readonly IReadOnlyList<IValidationRule<Context, HireValidationCode>> Rules =
    [
        new DelegateRule(HireValidationCode.InvalidPhase,
            "Gangs may only be hired during the Hire phase.",
            context => context.State.Coordinator.Phase != TurnPhase.Hire),
        new DelegateRule(HireValidationCode.InactivePlayer,
            "The hiring player is not active.",
            context => context.State.Coordinator.ActivePlayer != context.PlayerId),
        new DelegateRule(HireValidationCode.InactivePlayer,
            "The hiring player does not exist.",
            context => context.Player is null),
        new DelegateRule(HireValidationCode.PlayerEliminated,
            "An eliminated player cannot hire gangs.",
            context => context.Player!.Status != PlayerStatus.Active),
        new DelegateRule(HireValidationCode.OfferUnavailable,
            "The selected gang is not in the player's hire pool.",
            context => !context.Player!.HirePool.Contains(context.GangDefinitionId)),
        new DelegateRule(HireValidationCode.HireAlreadyPending,
            "The player has already selected a recruit this turn.",
            context => context.Player!.PendingHires.Count != 0),
        new DelegateRule(HireValidationCode.InsufficientCash,
            "The player cannot afford the selected gang.",
            context => context.Player!.Cash < InitialCost(
                context.State.Definitions.Gangs.Single(item => item.Id == context.GangDefinitionId))),
        new DelegateRule(HireValidationCode.SectorNotControlled,
            "A recruit must be placed in a sector controlled by the hiring player.",
            context => context.TargetSectorId is < 0 or >= MatchLimits.SectorCount ||
                context.State.Sectors[context.TargetSectorId].Owner != context.PlayerId),
        new DelegateRule(HireValidationCode.GangCapacityReached,
            "The player already commands the maximum number of gangs.",
            context => context.Player!.Gangs.Count(gang => gang.IsActive) >= MatchLimits.GangsPerPlayer),
        new DelegateRule(HireValidationCode.SectorCapacityReached,
            "The target sector already contains the maximum number of friendly gangs.",
            context => context.Player!.Gangs.Count(gang =>
                gang.IsActive && gang.SectorId == context.TargetSectorId) >= MatchLimits.FriendlyGangsPerSector)
    ];

    private sealed class DelegateRule(
        HireValidationCode code,
        string message,
        Func<Context, bool> rejects)
        : IValidationRule<Context, HireValidationCode>
    {
        private readonly ValidationFailure<HireValidationCode> _failure = new(code, message);

        public ValidationFailure<HireValidationCode>? Evaluate(Context context) =>
            rejects(context) ? _failure : null;
    }

    public static int InitialCost(GangDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Force;
    }

    public static HireValidation Validate(
        MatchState state,
        PlayerId playerId,
        short gangDefinitionId,
        int targetSectorId)
    {
        var context = new Context(
            state ?? throw new ArgumentNullException(nameof(state)),
            playerId, gangDefinitionId, targetSectorId);
        var failure = ValidationRuleSet.Evaluate(context, Rules);
        return failure is { } rejected
            ? new HireValidation(rejected.Code, rejected.Message)
            : HireValidation.Accept();
    }

    public static HireValidation ValidateSnub(
        MatchState state,
        PlayerId playerId,
        short gangDefinitionId)
    {
        ArgumentNullException.ThrowIfNull(state);
        var player = state.FindPlayer(playerId);
        if (state.Coordinator.Phase != TurnPhase.Hire)
            return new HireValidation(HireValidationCode.InvalidPhase, "Hire offers may only be snubbed during the Hire phase.");
        if (state.Coordinator.ActivePlayer != playerId || player is null)
            return new HireValidation(HireValidationCode.InactivePlayer, "The hiring player is not active.");
        if (player.Status != PlayerStatus.Active)
            return new HireValidation(HireValidationCode.PlayerEliminated, "An eliminated player cannot snub hire offers.");
        if (!player.HirePool.Contains(gangDefinitionId))
            return new HireValidation(HireValidationCode.OfferUnavailable, "The selected gang is not in the player's hire pool.");
        if (player.HasSnubbedHireOfferThisTurn)
            return new HireValidation(HireValidationCode.OfferAlreadySnubbed, "Only one hire offer may be snubbed per turn.");
        return HireValidation.Accept();
    }
}

internal static class HireResolver
{
    public static IReadOnlyList<HireResolutionResult> Resolve(MatchState state, MatchPlayerState player)
    {
        var results = new List<HireResolutionResult>(player.PendingHires.Count);
        foreach (var pending in player.PendingHires.ToArray())
        {
            var definition = state.Definitions.Gangs.Single(item => item.Id == pending.GangDefinitionId);
            var initialForce = state.Random.NextInclusive(
                ManualRules.MaximumHiredGangForce - ManualRules.MinimumHiredGangForce + 1)
                + ManualRules.MinimumHiredGangForce - 1;
            var gang = new MatchGangState(
                state.NextGangId(), player.Id, pending.GangDefinitionId,
                pending.TargetSectorId, initialForce)
            {
                HiredThisTurn = true
            };
            player.AddGang(gang);
            var replacement = RefillOffer(state, player);
            var details = new HireResolutionDetails(
                pending.GangDefinitionId, pending.TargetSectorId,
                HireRules.InitialCost(definition), gang.Id, replacement, initialForce);
            var gameEvent = state.AppendHireEvent(GameEventKind.HireResolved, player.Id, details);
            state.QueueNotification(player.Id, GameNotificationKind.Hire, gang.Id,
                pending.TargetSectorId, gameEvent.Sequence);
            results.Add(new HireResolutionResult(
                player.Id, pending.GangDefinitionId, pending.TargetSectorId,
                details.Cost, gang.Id, replacement, initialForce, gameEvent));
        }
        player.ClearPendingHires();
        if (player.HasSnubbedHireOfferThisTurn)
        {
            var replacement = RefillOffer(state, player);
            if (replacement is { } gangDefinitionId)
            {
                var gameEvent = state.AppendHireOfferEvent(
                    GameEventKind.HireOfferRefilled, player.Id,
                    new HireOfferDetails(player.SnubbedHireOffer, gangDefinitionId));
                state.QueueNotification(player.Id, GameNotificationKind.Hire,
                    relatedEventSequence: gameEvent.Sequence);
            }
            player.ClearSnubbedHireOffer();
        }
        return results;
    }

    private static short? RefillOffer(MatchState state, MatchPlayerState player)
    {
        if (player.HirePool.Count >= MatchLimits.HireOffersPerPlayer) return null;
        var candidates = state.Definitions.Gangs
            .Where(definition => definition.Id != 0 && !player.HirePool.Contains(definition.Id))
            .OrderBy(definition => definition.Id)
            .ToArray();
        if (candidates.Length == 0) return null;
        var selected = candidates[state.Random.NextInt(candidates.Length)].Id;
        player.AddHireOffer(selected);
        return selected;
    }
}
