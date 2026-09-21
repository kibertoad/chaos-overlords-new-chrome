namespace Rechaos.Core.GameModel;

/// <summary>The possible Chaos contribution from one player's queued orders in a sector.</summary>
public readonly record struct ChaosRange(int Minimum, int Maximum)
{
    public bool CanTriggerCrackdown(int tolerance) =>
        ManualRules.TriggersCrackdown(Maximum, tolerance);
}

/// <summary>The dice calculation for one queued Chaos order.</summary>
public readonly record struct ChaosRangeContribution(
    GangId Gang,
    int Income,
    int Force,
    int EffectiveChaos,
    int RawDice,
    int Dice);

/// <summary>The projected range plus the queued orders that create it.</summary>
public readonly record struct ChaosRangeEstimate(
    ChaosRange Range,
    IReadOnlyList<ChaosRangeContribution> Contributions);

/// <summary>
/// Projects the range of Chaos a player's currently queued Chaos orders can add to a sector's
/// crackdown check. This is presentation-only: it deliberately reads the live order queue and
/// never changes match state.
/// </summary>
public static class ChaosRangeProjection
{
    public static ChaosRange Project(MatchState state, PlayerId playerId, int sectorId)
        => Detail(state, playerId, sectorId).Range;

    public static ChaosRangeEstimate Detail(MatchState state, PlayerId playerId, int sectorId)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.FindPlayer(playerId) is null)
            throw new ArgumentOutOfRangeException(nameof(playerId));
        if (sectorId < 0 || sectorId >= state.Sectors.Count)
            throw new ArgumentOutOfRangeException(nameof(sectorId));

        var sector = state.Sectors[sectorId];
        var band = OriginalResolutionRules.Band(state, playerId);
        var contributions = state.Commands.ExecutionPlan()
            .Where(queued => queued.Command.Player == playerId
                && queued.Command.Action == GangAction.Chaos)
            .Select(queued => state.FindGang(queued.Command.Gang))
            .OfType<MatchGangState>()
            .Where(gang => gang.IsActive && gang.SectorId == sectorId)
            .Select(gang =>
            {
                var effectiveChaos = EffectiveStatisticsCalculator.ForGang(state, gang).Chaos;
                var rawDice = checked(sector.Income + gang.Force + effectiveChaos);
                var dice = OriginalResolutionRules.ActionPool(band, GangAction.Chaos, rawDice);
                return new ChaosRangeContribution(
                    gang.Id, sector.Income, gang.Force, effectiveChaos, rawDice, dice);
            })
            .ToArray();
        var dice = contributions.Sum(contribution => contribution.Dice);
        var maximum = OriginalResolutionRules.CrackdownContribution(
            band, sector.Owner == playerId, dice);
        return new ChaosRangeEstimate(new ChaosRange(0, maximum), contributions);
    }
}
