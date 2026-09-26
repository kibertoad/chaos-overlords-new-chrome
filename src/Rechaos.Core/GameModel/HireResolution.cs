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
    public static HireValidation Reject(HireValidationCode code) => new(code, HireValidationMessages.For(code));
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

internal static class HireValidationMessages
{
    private static readonly IReadOnlyDictionary<HireValidationCode, string> Messages =
        new Dictionary<HireValidationCode, string>
        {
            [HireValidationCode.Valid] = string.Empty,
            [HireValidationCode.InvalidPhase] = "Hire requires planning turn.",
            [HireValidationCode.InactivePlayer] = "Hiring player is not active.",
            [HireValidationCode.PlayerEliminated] = "Eliminated player cannot hire.",
            [HireValidationCode.OfferUnavailable] = "Hire offer is not available.",
            [HireValidationCode.OfferAlreadySnubbed] = "One hire snub allowed per turn.",
            [HireValidationCode.HireAlreadyPending] = "Hire already reserved this turn.",
            [HireValidationCode.InsufficientCash] = "Not enough cash to hire.",
            [HireValidationCode.SectorNotControlled] = "Use owned or occupied sector.",
            [HireValidationCode.GangCapacityReached] = "Player gang limit reached.",
            [HireValidationCode.SectorCapacityReached] = "Sector gang limit reached."
        };

    public static string For(HireValidationCode code) => Messages[code];
}

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

    private static readonly IReadOnlyList<IValidationRule<Context, HireValidationCode>> SelectionRules =
    [
        new DelegateRule(HireValidationCode.InvalidPhase,
            context => context.State.Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire)),
        new DelegateRule(HireValidationCode.InactivePlayer,
            context => context.State.Coordinator.ActivePlayer != context.PlayerId),
        new DelegateRule(HireValidationCode.InactivePlayer,
            context => context.Player is null),
        new DelegateRule(HireValidationCode.PlayerEliminated,
            context => context.Player!.Status != PlayerStatus.Active),
        new DelegateRule(HireValidationCode.OfferUnavailable,
            context => !context.Player!.HirePool.Contains(context.GangDefinitionId)),
        new DelegateRule(HireValidationCode.SectorNotControlled,
            context => !MatchLimits.IsSectorId(context.TargetSectorId) ||
                context.State.Sectors[context.TargetSectorId].Owner != context.PlayerId
                && !context.Player!.Gangs.Any(gang =>
                    gang.IsActive && gang.SectorId == context.TargetSectorId))
    ];

    private static readonly IReadOnlyList<IValidationRule<Context, HireValidationCode>> LegacyImmediatePaymentRules =
    [
        .. SelectionRules.Take(5),
        new DelegateRule(HireValidationCode.HireAlreadyPending,
            context => context.Player!.PendingHires.Count != 0
                || context.Player.HasSnubbedHireOfferThisTurn),
        new DelegateRule(HireValidationCode.InsufficientCash,
            context => !CanAffordInitialCost(
                context.Player!.Cash,
                context.State.Definitions.Gang(context.GangDefinitionId))),
        new DelegateRule(HireValidationCode.SectorNotControlled,
            context => !MatchLimits.IsSectorId(context.TargetSectorId) ||
                context.State.Sectors[context.TargetSectorId].Owner != context.PlayerId),
        new DelegateRule(HireValidationCode.GangCapacityReached,
            context => context.Player!.Gangs.Count(gang => gang.IsActive) >= MatchLimits.GangsPerPlayer),
        new DelegateRule(HireValidationCode.SectorCapacityReached,
            context => context.Player!.Gangs.Count(gang =>
                gang.IsActive && gang.SectorId == context.TargetSectorId) >= MatchLimits.FriendlyGangsPerSector)
    ];

    private sealed class DelegateRule(
        HireValidationCode code,
        Func<Context, bool> rejects)
        : IValidationRule<Context, HireValidationCode>
    {
        private readonly ValidationFailure<HireValidationCode> _failure =
            new(code, HireValidationMessages.For(code));

        public ValidationFailure<HireValidationCode>? Evaluate(Context context) =>
            rejects(context) ? _failure : null;
    }

    public static int InitialCost(GangDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return definition.Force;
    }

    public static bool CanAffordInitialCost(int cash, GangDefinition definition)
    {
        var cost = InitialCost(definition);
        return cost == 0 || cash >= cost;
    }

    public static HireValidation Validate(
        MatchState state,
        PlayerId playerId,
        short gangDefinitionId,
        int targetSectorId) =>
        Evaluate(state, playerId, gangDefinitionId, targetSectorId, SelectionRules);

    internal static HireValidation ValidateLegacyImmediatePayment(
        MatchState state,
        PlayerId playerId,
        short gangDefinitionId,
        int targetSectorId) =>
        Evaluate(state, playerId, gangDefinitionId, targetSectorId, LegacyImmediatePaymentRules);

    private static HireValidation Evaluate(
        MatchState state,
        PlayerId playerId,
        short gangDefinitionId,
        int targetSectorId,
        IReadOnlyList<IValidationRule<Context, HireValidationCode>> rules)
    {
        var context = new Context(
            state ?? throw new ArgumentNullException(nameof(state)),
            playerId, gangDefinitionId, targetSectorId);
        var failure = ValidationRuleSet.Evaluate(context, rules);
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
        if (state.Coordinator.Phase is not (TurnPhase.Command or TurnPhase.Hire))
            return HireValidation.Reject(HireValidationCode.InvalidPhase);
        if (state.Coordinator.ActivePlayer != playerId || player is null)
            return HireValidation.Reject(HireValidationCode.InactivePlayer);
        if (player.Status != PlayerStatus.Active)
            return HireValidation.Reject(HireValidationCode.PlayerEliminated);
        if (!player.HirePool.Contains(gangDefinitionId))
            return HireValidation.Reject(HireValidationCode.OfferUnavailable);
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
            var definition = state.Definitions.Gang(pending.GangDefinitionId);
            var cost = HireRules.InitialCost(definition);
            // RULE-HIRE-001: the original scans only the hiring player's gang records here.
            var hasSectorCapacity = player.Gangs
                .Count(gang => gang.IsActive && gang.SectorId == pending.TargetSectorId)
                < MatchLimits.FriendlyGangsPerSector;
            var canAfford = HireRules.CanAffordInitialCost(player.Cash, definition);
            // RULE-HIRE-001: the resolver counts the hiring player's gang records in the
            // target sector first and only then looks at then-current cash. Neither test draws RNG,
            // so the order decides nothing but which failure notification the player is shown.
            if (!pending.InitialCostPaid && !hasSectorCapacity)
            {
                RecordFailure(state, player, pending, definition,
                    GameNotificationKind.HireSectorFull);
                continue;
            }
            if (!pending.InitialCostPaid && !canAfford)
            {
                RecordFailure(state, player, pending, definition,
                    GameNotificationKind.HireInsufficientCash);
                continue;
            }

            var initialForce = player.UsesMaximumHireForce
                ? ManualRules.MaximumForce
                : state.Random.NextInclusive(
                    ManualRules.MaximumHiredGangForce - ManualRules.MinimumHiredGangForce + 1)
                    + ManualRules.MinimumHiredGangForce - 1;
            var hasGangCapacity = player.Gangs.Count(gang => gang.IsActive) < MatchLimits.GangsPerPlayer;
            if (!pending.InitialCostPaid && !hasGangCapacity)
            {
                RecordFailure(state, player, pending, definition,
                    GameNotificationKind.HireGangLimit);
                continue;
            }

            if (!pending.InitialCostPaid)
            {
                player.Cash -= cost;
                player.Statistics.CashSpent += cost;
            }
            var gang = new MatchGangState(
                state.NextGangId(), player.Id, pending.GangDefinitionId,
                pending.TargetSectorId, initialForce)
            {
                HiredThisTurn = true
            };
            var reusableGangSlot = player.Gangs
                .Select((candidate, slot) => (candidate, slot))
                .Where(entry => !entry.candidate.IsActive)
                .Select(entry => entry.slot)
                .FirstOrDefault(-1);
            if (reusableGangSlot >= 0)
            {
                // The slot's previous occupant is about to stop existing, so anything still holding
                // its id has to let go first: an order left in the queue would resolve next turn
                // against a gang `FindGang` can no longer find.
                state.Commands.Cancel(player.Gangs[reusableGangSlot].Id);
                player.ReplaceGang(reusableGangSlot, gang);
                state.AiPlanning.ResetGangSlot(player.Id, reusableGangSlot);
            }
            else
            {
                if (player.Gangs.Count >= AiPlanningState.GangSlotsPerPlayer)
                    throw new InvalidOperationException(
                        "No original gang/planning slot is available for the resolved hire.");
                player.AddGang(gang);
            }
            var offerSlot = pending.OfferSlot >= 0
                ? pending.OfferSlot
                : player.FindHireOfferSlot(pending.GangDefinitionId);
            ReleaseOfferSlot(player, offerSlot, pending.GangDefinitionId);
            var details = new HireResolutionDetails(
                pending.GangDefinitionId, pending.TargetSectorId,
                cost, gang.Id, null, initialForce);
            var gameEvent = state.AppendHireEvent(GameEventKind.HireResolved, player.Id, details);
            state.QueueNotification(player.Id, GameNotificationKind.Hire, gang.Id,
                pending.TargetSectorId, gameEvent.Sequence);
            results.Add(new HireResolutionResult(
                player.Id, pending.GangDefinitionId, pending.TargetSectorId,
                details.Cost, gang.Id, null, initialForce, gameEvent));
        }
        player.ClearPendingHires();
        if (player.HasSnubbedHireOfferThisTurn)
        {
            var snubbed = player.SnubbedHireOffer!.Value;
            var offerSlot = player.SnubbedHireOfferSlot
                ?? player.FindHireOfferSlot(snubbed);
            ReleaseOfferSlot(player, offerSlot, snubbed);
            player.ClearSnubbedHireOffer();
        }
        return results;
    }

    /// <summary>
    /// Empties an offer slot whose gang was hired or snubbed. The slot takes its stored legacy
    /// replacement when it has one, and otherwise goes vacant excluding the gang that left it.
    /// </summary>
    private static void ReleaseOfferSlot(MatchPlayerState player, int offerSlot, short definitionId)
    {
        if (offerSlot < 0) return;
        var slot = player.HireOfferSlots[offerSlot];
        player.SetHireOfferSlot(offerSlot,
            slot.LegacyReplacementDefinitionId is { } replacement
                ? HireOfferSlotState.Available(replacement)
                : HireOfferSlotState.Vacant(definitionId));
    }

    private static void RecordFailure(
        MatchState state,
        MatchPlayerState player,
        PendingHireState pending,
        GangDefinition definition,
        GameNotificationKind notificationKind)
    {
        var details = new HireResolutionDetails(
            pending.GangDefinitionId, pending.TargetSectorId,
            HireRules.InitialCost(definition));
        var gameEvent = state.AppendHireEvent(
            GameEventKind.HireFailed, player.Id, details);
        state.QueueNotification(player.Id, notificationKind,
            sectorId: pending.TargetSectorId,
            relatedEventSequence: gameEvent.Sequence);
    }

    internal static void FillOffers(MatchState state, MatchPlayerState player)
    {
        for (var slot = 0; slot < player.HireOfferSlots.Count; slot++)
        {
            var current = player.HireOfferSlots[slot];
            if (current.GangDefinitionId.HasValue) continue;
            var replacement = DrawOffer(state, player, current.ExcludedDefinitionId);
            player.SetHireOfferSlot(slot, HireOfferSlotState.Available(replacement));
            if (current.ExcludedDefinitionId is { } removed)
            {
                var gameEvent = state.AppendHireOfferEvent(
                    GameEventKind.HireOfferRefilled, player.Id,
                    new HireOfferDetails(removed, replacement));
                state.QueueNotification(player.Id, GameNotificationKind.Hire,
                    relatedEventSequence: gameEvent.Sequence);
            }
        }
    }

    private static short DrawOffer(
        MatchState state,
        MatchPlayerState player,
        short? excludedDefinitionId)
    {
        short selected;
        do selected = checked((short)state.Random.NextInclusive(89));
        while (player.HirePool.Contains(selected)
               || player.HireOfferSlots.Any(slot => slot.LegacyReplacementDefinitionId == selected)
               || selected == excludedDefinitionId);
        if (!state.Definitions.Gangs.Any(definition => definition.Id == selected))
            throw new InvalidOperationException("Original hire refill requires gang definitions 1 through 89.");
        return selected;
    }
}
