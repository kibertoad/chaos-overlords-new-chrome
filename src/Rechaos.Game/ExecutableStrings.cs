using Rechaos.Core.GameModel;

namespace Rechaos.Game;

/// <summary>
/// The texts of the executable's STRING resources that the rebuild draws, by resource number.
/// RULE-UI-009, SCR-EVENT-001, FND-UI-013, FND-UI-040, FND-UI-049 and FND-SETUP-013 name them by number. ExecutableStringTableTests compares every
/// one with the string table of BLD-GOG-EN-1.1.
/// </summary>
public static class ExecutableStrings
{
    /// <summary>
    /// FND-AWARDS-004: the endgame's score caption, the zero-terminated string at 0x00487704.
    /// ExecutableStringTableTests reads it there.
    /// </summary>
    public const string ScoreCaption = "SCORE";

    public static IReadOnlyDictionary<int, string> Drawn { get; } = new Dictionary<int, string>
    {
        // RULE-UI-009: the scenario names, at the original's scenario number plus 1.
        [0x01] = "GREED",
        [0x02] = "POWER",
        [0x03] = "ACCEPTANCE",
        [0x04] = "DOMINANCE",
        [0x05] = "KILL 'EM ALL",
        [0x06] = "THE BIG 40",
        [0x07] = "SIEGE",
        [0x08] = "ELIMINATE",
        [0x09] = "BIG MAN",
        [0x0A] = "ARMAGEDDON",
        // FND-UI-040: the calendar companion in the final planning view.
        [0x13] = "COMPLETE",
        // SCR-UI-006, FND-UI-013: the item types of Item Information, at 25 plus the type.
        [0x19] = "MELEE",
        [0x1A] = "BLADE",
        [0x1B] = "RANGE",
        [0x1C] = "ARMOR",
        [0x1D] = "MISC",
        // SCR-UI-007, FND-UI-049: the special effects' lines of Site Information.
        [0x1E] = "RESEARCH TECH     8",
        [0x1F] = "RESEARCH TECH    10",
        [0x20] = "EQUIP DISCOUNT   30%",
        // SCR-EVENT-001: the Last Turn Events captions.
        [0x21] = "NO EVENTS.",
        [0x22] = "POLICE CRACKDOWN.",
        [0x23] = "SECTOR CONTROL ATTAINED.",
        [0x24] = "SECTOR CONTROL LOST.",
        [0x25] = "SITE COOPERATION ACHIEVED.",
        [0x26] = "RESEARCH COMPLETED.",
        [0x27] = "INSUFFICIENT CASH TO BRIBE.",
        [0x28] = "INSUFFICIENT CASH TO EQUIP.",
        [0x29] = "INSUFFICIENT CASH TO HIRE.",
        [0x2A] = "UNABLE TO HIRE, SECTOR AT CAPACITY.",
        [0x2B] = "UNABLE TO HIRE, MAX GANGS REACHED.",
        [0x2C] = "PLAYER HAS BEEN ELIMINATED.",
        // RULE-UI-009: the Mentalities, the planning time limits, the scenario lengths and the
        // player labels.
        [0x2E] = "GOON",
        [0x2F] = "CRIMINAL",
        [0x30] = "CRIME LORD",
        [0x31] = "HOMICIDAL MANIAC",
        [0x32] = "NONE",
        [0x33] = "30 SECONDS",
        [0x34] = "2 MINUTES",
        [0x35] = "5 MINUTES",
        [0x36] = "6 MONTHS",
        [0x37] = "1 YEAR",
        [0x38] = "2 YEARS",
        [0x39] = "4 YEARS",
        [0x3A] = "HUMAN",
        [0x3B] = "AI",
        [0x3C] = "ELIMINATED",
        // FND-SETUP-013: the scenario descriptions of the setup screen, at the scenario number
        // plus 95.
        [0x5F] =
            "THE FORCE THAT DRIVES THE WORLD.  GREED BRINGS GROUPS TOGETHER AND RIPS THEM APART.  THE OVERLORD WITH THE MOST CASH AT THE END OF THE TIME PERIOD WILL WALK AWAY ON TOP.",
        [0x60] =
            "THE MOST EFFECTIVE WAY TO CONTROL PEOPLE IS TO CONTROL THE GROUND UNDER THEIR FEET.  TAKE CONTROL OF AS MANY SECTORS AS YOU CAN, BECAUSE IT WILL DETERMINE YOUR FUTURE.",
        [0x61] =
            "THE CITIZENS OF THIS CITY ARE LIKE BEES.  HAVING ONE \"STING\" YOU IS NOT A REAL PROBLEM, BUT WHEN THOUSANDS \"STING\" YOU, YOU WILL NOT SURVIVE.  GIVE THEM WHAT THEY WANT.",
        [0x62] =
            "REAL POWER RELIES ON MORE THAN A SINGLE POINT OF VIEW.  YOU MUST BE ABLE TO SEE THE COMPLETE PICTURE.  GREED, POWER AND ACCEPTANCE ARE NECESSARY TO BE TRULY DOMINANT.",
        [0x63] =
            "TO ENSURE THAT YOUR ORGANIZATION IS A FORCE IN THE CITY, YOU TAKE THE LOGICAL ROUTE.  ELIMINATE ALL OTHER OVERLORDS, LEAVING NO OPPOSITION.",
        [0x64] =
            "YOU ARE CIVILIZED IN YOUR BELIEF THAT THE CITY IS LARGE ENOUGH TO BE SHARED.  YOU ARE WILLING TO SPLIT THE CITY AMONGST THE OVERLORDS, 40 SECTORS FOR YOU, 20 FOR THEM.",
        [0x65] =
            "SOME PEOPLE SAY THAT THE QUICKEST WAY TO KILL SOMEONE IS TO RIP THEIR HEART OUT.  THE OVERLORD THAT HOLDS ALL SIX HEADQUARTERS WILL DOMINATE THE CITY.",
        [0x66] =
            "AN ORGANIZATION IS A LOT LIKE A TANK.  ELIMINATE THE MAN INSIDE, AND THE TANK IS RENDERED USELESS.  THE \"RIGHT HANDS\" ARE THE MAN INSIDE.",
        [0x67] =
            "OFTEN TIMES, FIGHTS ARE OVER PETTY THINGS.  THIS DISPUTE IS OVER FOUR SECTORS IN THE MIDDLE OF THE CITY.  THE OVERLORD TO ACCUMULATE 40 WEEKS WORTH OF CONTROL IS THE WINNER.",
        [0x68] =
            "500 CASH, UNLIMITED ACCESS TO EVERY TECHNOLOGICAL DEVICE, AND AN URGE TO TAKE CONTROL OF THE ENTIRE CITY.  ANY QUESTIONS?"
    };

    public static string Get(int id) => Drawn.TryGetValue(id, out var text)
        ? text
        : throw new ArgumentOutOfRangeException(nameof(id), id, "The rebuild does not draw this string.");

    /// <summary>RULE-UI-009, FND-UI-040: the scenario's title, string resource scenario + 1.</summary>
    public static string ScenarioTitle(ScenarioId scenario) => Get(ScenarioNumber(scenario) + 1);

    /// <summary>FND-SETUP-013: the scenario's description, string resource scenario + 95.</summary>
    public static string ScenarioDescription(ScenarioId scenario) => Get(ScenarioNumber(scenario) + 95);

    /// <summary>
    /// The original's number for a scenario (the glossary's <c>scenario</c>), which orders Siege
    /// before Eliminate.
    /// </summary>
    public static int ScenarioNumber(ScenarioId scenario) => scenario switch
    {
        ScenarioId.Greed => 0,
        ScenarioId.Power => 1,
        ScenarioId.Acceptance => 2,
        ScenarioId.Dominance => 3,
        ScenarioId.KillEmAll => 4,
        ScenarioId.Big40 => 5,
        ScenarioId.Siege => 6,
        ScenarioId.Eliminate => 7,
        ScenarioId.BigMan => 8,
        ScenarioId.Armageddon => 9,
        _ => throw new ArgumentOutOfRangeException(nameof(scenario))
    };
}
