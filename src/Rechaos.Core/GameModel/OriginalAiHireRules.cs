using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Three-offer ranking recovered from the original AI helper at 0x004078d9.
/// Returns the offer slot, not a gang ID, because later offers win most ties.
/// </summary>
internal static class OriginalAiHireRules
{
    public static int? SelectOfferIndex(
        IReadOnlyList<GangDefinition> offers,
        ScenarioId scenario,
        int requestedMode,
        int availableCash)
    {
        ArgumentNullException.ThrowIfNull(offers);
        if (offers.Count != 3)
            throw new ArgumentException("The original AI ranks exactly three hire offers.", nameof(offers));
        if (offers.Any(offer => offer is null))
            throw new ArgumentException("Hire offers cannot contain null definitions.", nameof(offers));

        var mode = requestedMode == 0 && availableCash > 200 && scenario != ScenarioId.Greed
            ? 3
            : requestedMode;
        var selected = mode switch
        {
            0 => SelectLastMinimum(offers, initialScore: 3,
                eligible: offer => offer.Stats.Control >= 0,
                score: offer => offer.Upkeep),
            1 => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => scenario != ScenarioId.Greed || offer.Upkeep <= 3,
                score: offer => offer.Stats.Heal),
            2 => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => scenario != ScenarioId.Greed || offer.Upkeep <= 3,
                score: offer => offer.Stats.Research),
            3 => SelectLastMaximum(offers, initialScore: 0,
                eligible: _ => true,
                score: CombatScore),
            4 when scenario == ScenarioId.Greed => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => offer.Upkeep <= 4
                    && offer.Stats.Strength >= 0
                    && offer.Stats.Stealth > 3,
                score: offer => offer.Stats.Stealth + offer.Stats.Strength),
            4 => SelectFirstStrictMaximum(offers, initialScore: 0,
                eligible: offer => offer.Stats.Strength >= 0,
                score: offer => offer.Stats.Stealth + offer.Stats.Strength),
            5 => SelectLastMaximum(offers, initialScore: 10,
                eligible: _ => true,
                score: offer => offer.Stats.Detect),
            _ => null
        };

        // The original ranks all three offers first, then rejects an unaffordable
        // winner without falling back to the next-best affordable candidate.
        return selected is { } index && offers[index].Force <= availableCash ? index : null;
    }

    private static int CombatScore(GangDefinition offer) => offer.Stats.Combat
        + Math.Max(0, (int)offer.Stats.Blade)
        + Math.Max(0, (int)offer.Stats.Range)
        + Math.Max(0, (int)offer.Stats.Fighting)
        + Math.Max(0, (int)offer.Stats.MartialArts);

    private static int? SelectLastMinimum(
        IReadOnlyList<GangDefinition> offers,
        int initialScore,
        Func<GangDefinition, bool> eligible,
        Func<GangDefinition, int> score)
    {
        int? selected = null;
        var best = initialScore;
        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            var candidate = score(offer);
            if (!eligible(offer) || candidate > best) continue;
            best = candidate;
            selected = index;
        }
        return selected;
    }

    private static int? SelectLastMaximum(
        IReadOnlyList<GangDefinition> offers,
        int initialScore,
        Func<GangDefinition, bool> eligible,
        Func<GangDefinition, int> score)
    {
        int? selected = null;
        var best = initialScore;
        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            var candidate = score(offer);
            if (!eligible(offer) || candidate < best) continue;
            best = candidate;
            selected = index;
        }
        return selected;
    }

    private static int? SelectFirstStrictMaximum(
        IReadOnlyList<GangDefinition> offers,
        int initialScore,
        Func<GangDefinition, bool> eligible,
        Func<GangDefinition, int> score)
    {
        int? selected = null;
        var best = initialScore;
        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            var candidate = score(offer);
            if (!eligible(offer) || candidate <= best) continue;
            best = candidate;
            selected = index;
        }
        return selected;
    }
}
