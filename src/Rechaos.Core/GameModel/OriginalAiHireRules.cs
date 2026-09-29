using Rechaos.Core.Assets;

namespace Rechaos.Core.GameModel;

/// <summary>
/// Three-offer ranking recovered from the original AI helper at 0x004078d9 (RULE-AI-008,
/// FND-AI-064) and the snub choice of selector 0x8E (RULE-AI-009, FND-AI-065).
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
        ValidateOffers(offers);

        var mode = requestedMode == 0 && availableCash > 200 && scenario != ScenarioId.Greed
            ? 3
            : requestedMode;
        var selected = mode switch
        {
            0 => SelectLastMinimum(offers, initialScore: 3,
                eligible: offer => offer.Stats.Chaos >= 0,
                score: offer => offer.Upkeep),
            1 => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => scenario != ScenarioId.Greed || offer.Upkeep <= 3,
                score: offer => offer.Stats.Control),
            2 => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => scenario != ScenarioId.Greed || offer.Upkeep <= 3,
                score: offer => offer.Stats.Influence),
            3 => SelectLastMaximum(offers, initialScore: 0,
                eligible: _ => true,
                score: CombatScore),
            4 when scenario == ScenarioId.Greed => SelectLastMaximum(offers, initialScore: 0,
                eligible: offer => offer.Upkeep <= 4
                    && offer.Stats.Research >= 0
                    && offer.TechLevel > 3,
                score: offer => offer.TechLevel + offer.Stats.Research),
            4 => SelectFirstStrictMaximum(offers, initialScore: 0,
                eligible: offer => offer.Stats.Research >= 0,
                score: offer => offer.TechLevel + offer.Stats.Research),
            5 => SelectLastMaximum(offers, initialScore: 10,
                eligible: _ => true,
                score: offer => offer.Stats.Stealth),
            _ => null
        };

        // The original ranks all three offers first, then rejects an unaffordable
        // winner without falling back to the next-best affordable candidate.
        return selected is { } index && offers[index].Force <= availableCash ? index : null;
    }

    public static int SelectRejectedOfferIndex(
        IReadOnlyList<GangDefinition> offers,
        ScenarioId scenario)
    {
        ValidateOffers(offers);
        if (scenario == ScenarioId.Greed) return 0;

        var selected = 0;
        var best = 5_000;
        for (var index = 0; index < offers.Count; index++)
        {
            var offer = offers[index];
            var stats = offer.Stats;
            var positiveTotal = Math.Max(0, (int)stats.Combat)
                + Math.Max(0, (int)stats.Defense)
                + Math.Max(0, (int)stats.Chaos)
                + Math.Max(0, (int)stats.Control)
                + Math.Max(0, (int)stats.Heal)
                + Math.Max(0, (int)stats.Influence)
                + Math.Max(0, (int)stats.Research)
                + Math.Max(0, (int)stats.Strength)
                + Math.Max(0, (int)stats.Blade)
                + Math.Max(0, (int)stats.Range)
                + Math.Max(0, (int)stats.Fighting)
                + Math.Max(0, (int)stats.MartialArts);
            var score = offer.TechLevel * positiveTotal * 20
                / (offer.Force + offer.Upkeep + 1);
            if (score >= best) continue;
            best = score;
            selected = index;
        }
        return selected;
    }

    private static int CombatScore(GangDefinition offer) => offer.Stats.Combat
        + Math.Max(0, (int)offer.Stats.Strength)
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

    private static void ValidateOffers(IReadOnlyList<GangDefinition> offers)
    {
        ArgumentNullException.ThrowIfNull(offers);
        if (offers.Count != 3)
            throw new ArgumentException("The original AI ranks exactly three hire offers.", nameof(offers));
        if (offers.Any(offer => offer is null))
            throw new ArgumentException("Hire offers cannot contain null definitions.", nameof(offers));
    }
}
