using Microsoft.Xna.Framework;
using Rechaos.Core.GameModel;

namespace Rechaos.Game;

public enum InformationEffect
{
    Combat,
    Defense,
    Chaos,
    Control,
    Heal,
    Influence,
    Research,
    Stealth,
    Detect,
    Strength,
    Blade,
    Range,
    Fighting,
    MartialArts
}

public static class InformationEffectTooltips
{
    private static readonly InformationEffect[] LeftEffects =
    [
        InformationEffect.Combat, InformationEffect.Defense, InformationEffect.Chaos,
        InformationEffect.Control, InformationEffect.Heal, InformationEffect.Influence,
        InformationEffect.Research
    ];

    private static readonly InformationEffect[] RightEffects =
    [
        InformationEffect.Stealth, InformationEffect.Detect, InformationEffect.Strength,
        InformationEffect.Blade, InformationEffect.Range, InformationEffect.Fighting,
        InformationEffect.MartialArts
    ];

    private static readonly InformationEffect[] HireEffects =
    [
        InformationEffect.Combat, InformationEffect.Defense, InformationEffect.Stealth,
        InformationEffect.Detect, InformationEffect.Chaos, InformationEffect.Control,
        InformationEffect.Heal, InformationEffect.Influence, InformationEffect.Research,
        InformationEffect.Strength, InformationEffect.Blade, InformationEffect.Range,
        InformationEffect.Fighting, InformationEffect.MartialArts
    ];

    /// <param name="modifiers">
    /// Optional per-statistic breakdown appended to a hovered statistic, used by live gangs to
    /// name the equipment and influenced sites behind the displayed value.
    /// </param>
    public static IReadOnlyList<string> GangAt(
        Point point,
        Func<InformationEffect, IReadOnlyList<string>>? modifiers = null)
    {
        if (Field(SharedPanelLayout.X(94), SharedPanelLayout.Y(92), 90).Contains(point))
            return ["FORCE", "CURRENT HEALTH AND THE BASE FOR MOST ACTION DICE.",
                "A GANG IS ELIMINATED WHEN FORCE REACHES ZERO."];
        if (Field(SharedPanelLayout.X(190), SharedPanelLayout.Y(92), 90).Contains(point))
            return ["UPKEEP", "CASH PAID FOR THIS GANG DURING EACH UPKEEP."];
        if (Field(SharedPanelLayout.X(190), SharedPanelLayout.Y(101), 90).Contains(point))
            return ["TECH LEVEL", "LIMITS WHICH ITEMS THIS GANG CAN USE OR RESEARCH."];
        return StatisticAt(point, GangInformationLayout.StatisticY,
            "GANG STAT; EQUIPMENT AND OWNED LOCAL SITES CAN MODIFY IT.", modifiers);
    }

    /// <param name="special">
    /// The site definition's special value; a special site explains its effect when its portrait
    /// is hovered.
    /// </param>
    public static IReadOnlyList<string> SiteAt(Point point, short special = 0)
    {
        if (SiteInformationLayout.Portrait.Contains(point))
            return SpecialSite(special);
        if (Field(SiteInformationLayout.DataLabelLeft, SiteInformationLayout.DataY(0), 122).Contains(point))
            return ["RESISTANCE", "INFLUENCE SUCCESSES REDUCE THIS VALUE.",
                "AT ZERO, THE ACTING PLAYER INFLUENCES THE SITE."];
        if (Field(SiteInformationLayout.DataLabelLeft, SiteInformationLayout.DataY(1), 122).Contains(point))
            // RULE-SITE-001
            return ["TOLERANCE", "ONCE COMPLETED, ADDED TO THE SECTOR'S BASE TOLERANCE",
                "WHEN THE SECTOR IS REBUILT BEFORE PLANNING."];
        if (Field(SiteInformationLayout.DataLabelLeft, SiteInformationLayout.DataY(2), 122).Contains(point))
            return ["SUPPORT", "WHEN INFLUENCED, ADDED AGAINST HOSTILE CONTROL ATTEMPTS."];
        if (Field(SiteInformationLayout.DataLabelLeft, SiteInformationLayout.DataY(3), 122).Contains(point))
            return ["CASH", "WHEN INFLUENCED, PAID TO THE SITE OWNER EACH UPKEEP."];
        return StatisticAt(point, SiteInformationLayout.StatisticY,
            "WHILE INFLUENCED, MODIFIES THE OWNER'S GANGS IN THIS SECTOR.",
            leftLabelLeft: SiteInformationLayout.LeftStatisticLabelLeft,
            rightLabelLeft: SiteInformationLayout.RightStatisticLabelLeft);
    }

    public static IReadOnlyList<string> ItemAt(Point point)
    {
        if (Field(SharedPanelLayout.X(94), SharedPanelLayout.Y(92), 90).Contains(point))
            return ["COST", "BASE PURCHASE COST; AN INFLUENCED FACTORY MAY REDUCE IT."];
        if (Field(SharedPanelLayout.X(190), SharedPanelLayout.Y(92), 90).Contains(point))
            return ["TECH LEVEL", "THE CARRIER MUST HAVE AT LEAST THIS TECH LEVEL."];
        return StatisticAt(point, ItemInformationLayout.StatisticY,
            "WHILE EQUIPPED, MODIFIES THE GANG CARRYING THIS ITEM.");
    }

    public static IReadOnlyList<string> HireAt(Point point)
    {
        for (var row = 0; row < 16; row++)
        {
            if (!new Rectangle(HireComparisonLayout.Panel.X + 96,
                    HireComparisonLayout.StatY(row) - 1, 184, 9)
                    .Contains(point)) continue;
            if (row == 0)
                return ["TECH LEVEL", "LIMITS WHICH ITEMS THE HIRED GANG CAN USE OR RESEARCH."];
            if (row == 1)
                return ["UPKEEP", "CASH PAID FOR THE HIRED GANG DURING EACH UPKEEP."];
            return Describe(HireEffects[row - 2], "BASE STAT OF EACH GANG OFFER.");
        }
        return [];
    }

    public static IReadOnlyList<string> SpecialSite(short special) => special switch
    {
        SpecialSiteRules.Factory =>
        [
            "FACTORY",
            "WHILE INFLUENCED, EQUIPMENT BOUGHT BY THE OWNER'S",
            $"GANGS IN THIS SECTOR COSTS 1/{SpecialSiteRules.FactoryDiscountDivisor} LESS."
        ],
        SpecialSiteRules.ScienceCenter => ResearchSite("SCIENCE CENTER", SpecialSiteRules.ScienceCenterTechLimit),
        SpecialSiteRules.ResearchLab => ResearchSite("RESEARCH LAB", SpecialSiteRules.ResearchLabTechLimit),
        _ => []
    };

    private static IReadOnlyList<string> ResearchSite(string name, int techLimit) =>
    [
        name,
        "WHILE INFLUENCED, THE OWNER'S GANGS IN THIS SECTOR",
        $"CAN RESEARCH UP TO TECH {techLimit} INSTEAD OF {SpecialSiteRules.BaseResearchTechLimit}.",
        "EACH GANG'S OWN TECH LEVEL STILL APPLIES."
    ];

    public static IReadOnlyList<string> Describe(InformationEffect effect, string scope) =>
    [Name(effect), Effect(effect), scope];

    private static IReadOnlyList<string> StatisticAt(
        Point point,
        Func<int, int> statisticY,
        string scope,
        Func<InformationEffect, IReadOnlyList<string>>? modifiers = null,
        int leftLabelLeft = 198,
        int rightLabelLeft = 294)
    {
        for (var row = 0; row < LeftEffects.Length; row++)
        {
            var y = statisticY(row);
            if (Field(leftLabelLeft, y, 90).Contains(point))
                return Describe(LeftEffects[row], scope, modifiers);
            if (Field(rightLabelLeft, y, 90).Contains(point))
                return Describe(RightEffects[row], scope, modifiers);
        }
        return [];
    }

    private static IReadOnlyList<string> Describe(
        InformationEffect effect,
        string scope,
        Func<InformationEffect, IReadOnlyList<string>>? modifiers)
    {
        var description = Describe(effect, scope);
        if (modifiers is null) return description;
        var breakdown = modifiers(effect);
        return breakdown.Count == 0 ? description : [.. description, .. breakdown];
    }

    private static Rectangle Field(int x, int y, int width) => new(x, y - 1, width, 9);

    private static string Name(InformationEffect effect) => effect switch
    {
        InformationEffect.MartialArts => "MARTIAL ARTS",
        _ => effect.ToString().ToUpperInvariant()
    };

    private static string Effect(InformationEffect effect) => effect switch
    {
        InformationEffect.Combat => "ADDS TO FORCE WHEN ROLLING ATTACK DICE.",
        InformationEffect.Defense => "SUBTRACTED FROM AN ENEMY'S ATTACK DICE.",
        InformationEffect.Chaos => "ADDS TO FORCE FOR THE CHAOS ACTION'S DICE.",
        InformationEffect.Control => "ADDS ITS VALUE TO FORCE FOR CONTROL AND SECTOR DEFENSE.",
        InformationEffect.Heal => "MODIFIES THE HEAL ACTION'S BASE FOUR DICE.",
        InformationEffect.Influence => "ADDS ITS VALUE TO DICE FOR THE INFLUENCE ACTION.",
        InformationEffect.Research => "ADDS TO FORCE WHEN RESEARCHING AN ITEM.",
        InformationEffect.Stealth => "OPPOSES DETECT AND MAKES HIDDEN GANGS HARDER TO HIT.",
        InformationEffect.Detect => "HELPS REVEAL GANGS AND HIT HIDDEN TARGETS.",
        InformationEffect.Strength => "ADDS TO ATTACK DICE UNARMED OR WITH STRENGTH/BLADE TYPES.",
        InformationEffect.Blade => "ADDS ITS VALUE TO ATTACK DICE WITH A BLADE-TYPE WEAPON.",
        InformationEffect.Range => "ADDS ITS VALUE TO ATTACK DICE WITH A RANGE-TYPE WEAPON.",
        InformationEffect.Fighting => "ADDS ITS VALUE TO ATTACK DICE WHILE UNARMED.",
        InformationEffect.MartialArts => "ADDS UNARMED ATTACK DICE AND MAY BLOCK RETALIATION.",
        _ => throw new ArgumentOutOfRangeException(nameof(effect))
    };
}
