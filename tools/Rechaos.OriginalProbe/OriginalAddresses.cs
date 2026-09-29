namespace Rechaos.OriginalProbe;

/// <summary>Addresses in BLD-GOG-EN-1.1's executable, each from the spec finding named beside it.</summary>
internal static class OriginalAddresses
{
    public const string ExecutableSha256 = "a1430159bbe20869e277a5000311344f4ec141ab77c96b385336617149e97d89";

    // FND-RNG-001: the runtime srand, called once at process start with the clock.
    public const uint SeedGenerator = 0x00478CC0;

    // FND-RNG-003, RULE-RNG-002: the bounded wrapper roll(n), the only caller of rand.
    public const uint Roll = 0x0045D227;

    // FND-RNG-001: the startup function's call of the preference loader; the instruction after it
    // runs once the options are read.
    public const uint PreferenceLoaderCall = 0x00460D14;

    // FND-OPTIONS-001, FND-OPTIONS-003: pref_full_screen and the copy the display code reads.
    public const uint PrefFullScreen = 0x0048786C;
    public const uint PrefFullScreenCopy = 0x00498354;

    // FND-SETUP-002: the full local setup handler.
    public const uint LocalSetup = 0x0040E0A0;

    // SCR-UI-009: File, New Game.
    public const int NewGameCommand = 0x8101;

    // SCR-SETUP-001: the Begin button, (370, 375) with size 92 by 45.
    public const int BeginX = 370 + 46;
    public const int BeginY = 375 + 22;

    // FND-SETUP-013: the values the setup screen commits at Begin. The scenario dword and its
    // preference byte, the time limit, the Mentality, and the live roster the screen edits: six
    // INT32 player types, six portrait bytes and six 12-byte length-prefixed names.
    public const uint Scenario = 0x004ABBE8;
    public const uint PreferredScenario = 0x00487858;
    public const uint TurnLimit = 0x004A5EF8;
    public const uint Mentality = 0x00487850;
    public const uint RosterTypes = 0x004AB638;
    public const uint RosterPortraits = 0x004A5F00;
    public const uint RosterNames = 0x004A2588;
    public const int RosterNameLength = 12;
    public const byte EmptyPortrait = 15;

    // FND-SETUP-015: the six name modifier strings, in the order the new-match scan tests them,
    // each a placeholder byte followed by upper-case ASCII and a NUL until the first scan.
    public static readonly IReadOnlyDictionary<string, uint> ModifierNames = new Dictionary<string, uint>
    {
        ["right_hands"] = 0x00487B9C,
        ["visibility"] = 0x00487BA8,
        ["hire_force"] = 0x00487BB4,
        ["elite"] = 0x00487BC0,
        ["islands"] = 0x00487BCC,
        ["cash"] = 0x00487BD8,
    };
}
