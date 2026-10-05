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

    // FND-PLATFORM-009: the depth returned by display setup, used to choose the image set.
    public const uint DisplayDepth = 0x0048787C;

    // FND-PLATFORM-014: the SetBkColor call of the keyed mask compositor that passes the 16-bit
    // key RGB(255,252,255), which a 32-bit surface never holds.
    public const uint KeyColourCall = 0x00427C84;
    public const int SixteenBitWhiteKey = 0x00FFFCFF;
    public const int ThirtyTwoBitWhite = 0x00FFFFFF;

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

    // FND-EQUIP-008: the Equip panel's list builder, called as (category, tech_level, player,
    // roster slot), the sixteen INT32 entries it fills, the 16-bit Tech Level of the gang
    // definition records, 156 bytes apart, and the panel's own instruction that loads the Tech
    // Level for the call, which the probe uses as the return address of every call it makes.
    public const uint EquipListBuilder = 0x0043F136;
    public const uint EquipListEntries = 0x004948A8;
    public const int EquipListLength = 16;
    public const uint GangDefinitionTechLevel = 0x004A2882;
    public const int GangDefinitionSize = 156;
    public const uint InjectedCallReturn = 0x0043DE80;

    // FND-ATTACK-006: the Attack picker's roster builder, called as (opponent, sector), and the six
    // INT32 entries it fills.
    public const uint AttackTargetBuilder = 0x0043D132;
    public const uint AttackTargetEntries = 0x00494850;
    public const int AttackTargetLength = 6;

    // FND-UI-020: the PeekMessageA calls of the two message pumps, which every screen and panel
    // loop of the main thread goes through.
    public static readonly uint[] PumpPeeks = [0x0045C1DA, 0x0045C2E7];

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

    // FND-AWARDS-005: the renderer draws each listed player's name with fn_00413FD5, whose third
    // argument is the name at 0x004A2589 + 12 * player: the splash's at 0x0042D1A4, the ranked rows'
    // at 0x0042D2DA and the eliminated rows' at 0x0042DA64.
    public const uint TextDraw = 0x00413FD5;
    public const uint SplashNameDraw = 0x0042D1A4;
    public const uint RankedNameDraw = 0x0042D2DA;
    public const uint EliminatedNameDraw = 0x0042DA64;
    public const uint PlayerNames = 0x004A2589;
    public const int PlayerNameStride = 12;

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

    // FND-SEARCH-006: the city redraw fn_004123CC(viewer, ...) passes each site marker to
    // fn_00412AC4(definition, sector, ordinal, controlled). search_filters: one byte per player
    // and site definition, element player * 22 + definition, at 0x004A24E8 (FND-SEARCH-001).
    public const uint CityRedraw = 0x004123CC;
    public const uint SiteMarker = 0x00412AC4;
    public const uint SearchFilters = 0x004A24E8;
    public const int SiteDefinitionCount = 22;

    // FND-SEARCH-002: fn_00448E32, the Search panel's handler, which runs while the panel is open.
    public const uint SearchPanel = 0x00448E32;

    // FND-FINANCE-002: fn_0044D1BB(player, sector) builds and draws the Financial panel, -1 for the
    // City variant; the upper part of the console's Financial control, (552, 178, 48, 33), opens the
    // City variant and the lower part, (552, 211, 48, 15), the Sector variant of the selected sector
    // at 0x004ABC80 (SCR-UI-003). FND-FINANCE-003: the panel draws its nine numbers with
    // fn_00414187, whose third argument is the value, from the calls at 0x0044DFEB to 0x0044E2AB.
    // It closes on its control at (161, 293, 49, 22) (SCR-FINANCE-001).
    public const uint FinancePanel = 0x0044D1BB;
    public const uint NumberDraw = 0x00414187;
    public const uint FinanceFirstDraw = 0x0044DFEB;
    public const uint FinanceLastDraw = 0x0044E2AB;
    public const uint SelectedSector = 0x004ABC80;
    public const int FinanceCityX = 552 + 24;
    public const int FinanceCityY = 178 + 16;
    public const int FinanceSectorX = 552 + 24;
    public const int FinanceSectorY = 211 + 7;
    public const int FinanceCloseX = 161 + 24;
    public const int FinanceCloseY = 293 + 11;

    // FND-UI-040: fn_0046FD80, the planning-entry function, draws the active player's score from
    // 0x004A2790 and cash from 0x004A25E8, four bytes per player, as five number cells each.
    public const uint PlanningEntryDraw = 0x0046FD80;

    public const int DoneX = 500 + 50;
    public const int DoneY = 282 + 24;

    // FND-UI-020: the two pointer points the window procedure keeps on WM_MOUSEMOVE, the client
    // point from lParam and the point from GetCursorPos, each with x and y as 16-bit halves.
    public const uint PointerClientPoint = 0x0049859C;
    public const uint PointerScreenPoint = 0x004985A0;

    // FND-UI-020: left_button_down, the byte the window procedure keeps from the mouse messages.
    public const uint LeftButtonDown = 0x004985A4;

    // FND-HIRE-001: the hire block's first check of one hire order, the count of the player's gangs
    // in the order's sector, and the arrays it reads: offers, orders and cash.
    public const uint HireOrderCheck = 0x0047592B;
    public const uint HireOffers = 0x004ABBC0;
    public const uint HireOrders = 0x004A27C8;

    // SCR-HIRE-002, FND-HIRE-008: the centres of an offer portrait (440 + 66s, 373, 64, 64) and of its
    // Reject cross (472 + 66s, 437, 32, 13), and of a city map cell, 54 by 52 from (2, 42), eight to
    // a row, where a dropped offer names that cell's sector.
    public static int HireOfferX(int slot) => 472 + 66 * slot;
    public const int HireOfferY = 405;
    public static int HireRejectX(int slot) => 488 + 66 * slot;
    public const int HireRejectY = 443;
    public static (int X, int Y) MapSectorCentre(int sector) => (2 + 54 * (sector % 8) + 27, 42 + 52 * (sector / 8) + 26);

    // FND-UI-021, EXP-TURN-095: the popup helper fn_0042566D(menu, slot, point) and its
    // TrackPopupMenu call, whose seven stdcall arguments start with the menu handle and whose
    // result, the chosen command or 0, the helper stores at the next instruction.
    public const uint PopupMenuTrack = 0x00425715;
    public const uint PopupMenuTracked = 0x0042571B;
    public const int PopupMenuTrackArguments = 7;

    // FND-UI-015, FND-UI-018, FND-STATE-008: the view byte, 1 while the city is shown and 0 in the
    // sector view, and the sector view's six card slots, a roster slot or -1 each.
    public const uint CityViewShown = 0x00487B88;
    public const uint SectorCardSlots = 0x004ABC68;
    public const int SectorCards = 6;

    // FMT-STATE-007: the computer players' planning records, 81 of 16 bytes per player, with the
    // family at offset 0; FND-AI-043: raider_mode, one byte per player.
    public const uint PlanningRecords = 0x0048A250;
    public const int PlanningPlayerStride = 0x510;
    public const int PlanningRecordSize = 0x10;
    public const uint RaiderMode = 0x00482158;

    // FND-UI-038: the 16-bit counter of the viewed player's marker, 0 to 11.
    public const uint MarkerCounter = 0x00487B90;

    // FND-UI-017, FND-EVENT-006: the pump's dword counter, 0 to 7; the selected-sector frame is
    // the counter divided by 4, and bit 0 paces the control lights' blink.
    public const uint PumpCounter = 0x00487804;
    public const uint Cash = 0x004A25E8;

    // FND-OPTIONS-001: Slide Panels, read by the panel helpers that slide a panel in and out.
    public const uint PrefSlidePanels = 0x00487840;

    // FND-SETUP-016: the handoff card's presenter, which takes the next player's slot and returns
    // once Ready, (270, 241, 100, 48) on the screen, is released inside.
    public const uint HandoffCard = 0x004396C0;
    public const int ReadyX = 270 + 50;
    public const int ReadyY = 241 + 24;

    // FND-COMLINK-001, FND-COMLINK-004: 16 message records of 0xA6 bytes per player in
    // comlink_messages, with comlink_count and comlink_cursor, one INT32 per player each.
    // FND-COMLINK-006: comlink_pending and active_player. FND-COMLINK-007, FND-COMLINK-008: the
    // Send panel's draft buffer and its six selection bytes.
    public const uint ComlinkMessages = 0x0049CA90;
    public const int ComlinkPlayerStride = 0xA60;
    public const int ComlinkRecordSize = 0xA6;
    public const int ComlinkRecords = 16;
    public const uint ComlinkCount = 0x004981E0;
    public const uint ComlinkCursor = 0x004981C8;
    public const uint ComlinkPending = 0x0048781C;
    public const uint ComlinkDraft = 0x00498120;
    public const uint ComlinkSelected = 0x00498114;
    public const uint ActivePlayer = 0x004ABC84;

    // FND-COMLINK-002: the View handler fn_0045D61A; FND-COMLINK-003: the Send handler
    // fn_0045EAB1; FND-COMLINK-004: the helper fn_0045E04D(player, count) that marks and draws one
    // message; FND-COMLINK-006: fn_00460391(player), which drops the leading read messages when the
    // player's planning ends. FND-COMLINK-007 gives the ranges of all four.
    public const uint ComlinkView = 0x0045D61A;
    public const uint ComlinkSend = 0x0045EAB1;
    public const uint ComlinkShow = 0x0045E04D;
    public const uint ComlinkShowEnd = 0x0045E7CD;
    public const uint ComlinkDropRead = 0x00460391;

    // FND-UI-032: the console's Comlink control, (552, 126, 48, 48), gives its top 33 rows to View and
    // its bottom 15 rows to Send.
    public const int ComlinkViewX = 552 + 24;
    public const int ComlinkViewY = 126 + 16;
    public const int ComlinkSendX = 552 + 24;
    public const int ComlinkSendY = 159 + 7;

    // FND-COMLINK-002, FND-COMLINK-003, FND-COMLINK-007: the panels' controls on the screen, the
    // panel at (104, 124). View: Previous (135, 157, 26, 23), Next (163, 157, 26, 23) and Dismiss
    // (137, 293, 49, 22). Send: the card of slot p at (202 + 121 * (p / 3), 144 + 34 * (p % 3)),
    // 100 by 32, Cancel (137, 261, 49, 22) and Send (137, 293, 49, 22).
    public const int ViewPreviousX = 135 + 13;
    public const int ViewNextX = 163 + 13;
    public const int ViewArrowY = 157 + 11;
    public const int PanelButtonX = 137 + 24;
    public const int SendButtonY = 293 + 11;
    public const int CancelButtonY = 261 + 11;
    public static int CardX(int slot) => 202 + 121 * (slot / 3) + 50;
    public static int CardY(int slot) => 144 + 34 * (slot % 3) + 16;

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
