namespace Rechaos.OriginalProbe;

/// <summary>Addresses in BLD-GOG-EN-1.1's executable, each from the spec finding named beside it.</summary>
internal static class OriginalAddresses
{
    // FMT-STATE-001: 81 gang records of 0x20 bytes per player.
    public const uint GangRecords = 0x00498DA8;
    public const int PlayerGangStride = 0xA20;
    public const int GangRecordSize = 0x20;

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

    // FND-OPTIONS-001, FND-OPTIONS-002: Warn if Idle Gangs, which asks before Done ends a turn
    // with a gang left idle (RULE-OPTIONS-003), and Detailed Combat, which plays combat as the
    // detailed presentation.
    public const uint PrefWarnIdle = 0x00487860;
    public const uint PrefDetailedCombat = 0x0048785C;

    // RULE-AUDIO-003: effects_level and music_level, the Options dialog's volumes, and the
    // effects_enabled and music_enabled flags the loader sets from them.
    public const uint EffectsLevel = 0x00487864;
    public const uint MusicLevel = 0x00487868;
    public const uint EffectsEnabled = 0x0048783C;
    public const uint MusicEnabled = 0x00487838;

    // FND-TIMER-003: fn_0041BDD5, the planning time-limit test, called on every pass of the human's
    // planning loop in fn_0046FD80.
    public const uint PlanningTimeCheck = 0x0041BDD5;

    // FND-TIMER-001, FND-TIMER-003, EXP-TURN-046: planning_limit_choice, which the match entry maps to
    // planning_limit_ms; in the start helper fn_0041B8BC, the instruction that stores timer_ms in
    // planning_start_ms; in the drawing helper fn_0041B8FC, the instruction after the width is
    // stored, with elapsed * 100 at [ebp-4] and the width at [ebp-8]; in the time-limit test, the
    // compare with elapsed in eax, and the instruction that runs only when the limit has passed.
    // fn_00464290 plays an effect slot.
    public const uint PlanningLimitChoice = 0x00487854;
    public const uint PlanningLimitMs = 0x0049069C;
    public const uint PlanningTimerStarted = 0x0041B8C8;
    public const uint PlanningBarWidth = 0x0041B96D;
    public const uint PlanningBarDrawStart = 0x0041B8FC;
    public const uint PlanningBarDrawEnd = 0x0041BCBB;
    public const uint PlanningTimeCompare = 0x0041BDFD;
    public const uint PlanningTimeExpired = 0x0041BE09;
    public const uint PlaySound = 0x00464290;

    // elapsed_turns: 0 through the first turn, up by one after each resolution.
    public const uint ElapsedTurns = 0x0049CA68;

    // FND-AWARDS-001: the endgame's row painter, which reads the awards the builder has given.
    public const uint AwardsRows = 0x0042CE61;

    // match_over: set by the end-of-turn evaluation when the match is finished.
    public const uint MatchOver = 0x004ABBD4;

    // SCR-UI-003: the Done control, (500, 282) with size 100 by 48.
    // SCR-COMBAT-001, RULE-COMBAT-004: with Detailed Combat off, the human's planning opens the
    // Combat Results panel through fn_00451F80 after a fight that involved its gangs. SCR-EVENT-001,
    // FND-EVENT-001: it opens the Last Turn Events panel through fn_0044F2FC when it has reports.
    // Each waits until its Exit (137, 293, 49, 22) is pressed.
    public const uint CombatResults = 0x00451F80;
    public const uint LastTurnEvents = 0x0044F2FC;
    public const int PanelExitX = 137 + 24;
    public const int PanelExitY = 293 + 11;

    public const int DoneX = 500 + 50;
    public const int DoneY = 282 + 24;

    // FND-UI-020: left_button_down, the byte the window procedure keeps from the mouse messages.
    public const uint LeftButtonDown = 0x004985A4;

    // FND-HIRE-001: the hire block's first check of one hire order, the count of the player's gangs
    // in the order's sector, and the arrays it reads: offers, orders and cash.
    public const uint HireOrderCheck = 0x0047592B;
    public const uint HireOffers = 0x004ABBC0;
    public const uint HireOrders = 0x004A27C8;

    // FMT-STATE-007: the computer players' planning records, 81 of 16 bytes per player, with the
    // family at offset 0; FND-AI-043: raider_mode, one byte per player.
    public const uint PlanningRecords = 0x0048A250;
    public const int PlanningPlayerStride = 0x510;
    public const int PlanningRecordSize = 0x10;
    public const uint RaiderMode = 0x00482158;
    public const uint Cash = 0x004A25E8;

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
