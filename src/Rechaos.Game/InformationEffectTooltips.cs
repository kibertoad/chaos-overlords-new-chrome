using Microsoft.Xna.Framework;

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

    public static IReadOnlyList<string> GangAt(Point point)
    {
        if (Field(198, 217, 90).Contains(point))
            return ["FORCE", "CURRENT HEALTH AND THE BASE FOR MOST ACTION DICE.",
                "A GANG IS ELIMINATED WHEN FORCE REACHES ZERO."];
        if (Field(294, 217, 90).Contains(point))
            return ["UPKEEP", "CASH PAID FOR THIS GANG DURING EACH UPKEEP."];
        if (Field(294, 226, 90).Contains(point))
            return ["TECH LEVEL", "LIMITS WHICH ITEMS THIS GANG CAN USE OR RESEARCH."];
        return StatisticAt(point, GangInformationLayout.StatisticY,
            "GANG STAT; EQUIPMENT AND OWNED LOCAL SITES CAN MODIFY IT.");
    }

    public static IReadOnlyList<string> SiteAt(Point point)
    {
        if (Field(262, SiteInformationLayout.DataY(0), 122).Contains(point))
            return ["RESISTANCE", "INFLUENCE SUCCESSES REDUCE THIS VALUE.",
                "AT ZERO, THE ACTING PLAYER INFLUENCES THE SITE."];
        if (Field(262, SiteInformationLayout.DataY(1), 122).Contains(point))
            return ["TOLERANCE", "WHEN INFLUENCED, ADDED TO THE SECTOR'S NORMAL TOLERANCE."];
        if (Field(262, SiteInformationLayout.DataY(2), 122).Contains(point))
            return ["SUPPORT", "WHEN INFLUENCED, ADDED AGAINST HOSTILE CONTROL ATTEMPTS."];
        if (Field(262, SiteInformationLayout.DataY(3), 122).Contains(point))
            return ["CASH", "WHEN INFLUENCED, PAID TO THE SITE OWNER EACH UPKEEP."];
        return StatisticAt(point, SiteInformationLayout.StatisticY,
            "WHILE INFLUENCED, MODIFIES THE OWNER'S GANGS IN THIS SECTOR.");
    }

    public static IReadOnlyList<string> ItemAt(Point point)
    {
        if (Field(198, 217, 90).Contains(point))
            return ["COST", "BASE PURCHASE COST; AN INFLUENCED FACTORY MAY REDUCE IT."];
        if (Field(294, 217, 90).Contains(point))
            return ["TECH LEVEL", "THE CARRIER MUST HAVE AT LEAST THIS TECH LEVEL."];
        return StatisticAt(point, ItemInformationLayout.StatisticY,
            "WHILE EQUIPPED, MODIFIES THE GANG CARRYING THIS ITEM.");
    }

    public static IReadOnlyList<string> HireAt(Point point)
    {
        for (var row = 0; row < 16; row++)
        {
            if (!new Rectangle(200, HireComparisonLayout.StatY(row) - 1, 184, 9)
                    .Contains(point)) continue;
            if (row == 0)
                return ["TECH LEVEL", "LIMITS WHICH ITEMS THE HIRED GANG CAN USE OR RESEARCH."];
            if (row == 1)
                return ["UPKEEP", "CASH PAID FOR THE HIRED GANG DURING EACH UPKEEP."];
            return Describe(HireEffects[row - 2], "BASE STAT OF EACH GANG OFFER.");
        }
        return [];
    }

    public static IReadOnlyList<string> Describe(InformationEffect effect, string scope) =>
    [Name(effect), Effect(effect), scope];

    private static IReadOnlyList<string> StatisticAt(
        Point point,
        Func<int, int> statisticY,
        string scope)
    {
        for (var row = 0; row < LeftEffects.Length; row++)
        {
            var y = statisticY(row);
            if (Field(198, y, 90).Contains(point))
                return Describe(LeftEffects[row], scope);
            if (Field(294, y, 90).Contains(point))
                return Describe(RightEffects[row], scope);
        }
        return [];
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
