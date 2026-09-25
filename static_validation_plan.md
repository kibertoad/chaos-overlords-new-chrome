# Static validation plan

Work that needs no one to play the original: reading the executable and the data files of
BLD-GOG-EN-1.1 in Ghidra or a hex editor, and checking the spec against what they hold. Each item
names the spec entries it concerns and what to record. The procedure for the tools is in
[docs/GHIDRA.md](docs/GHIDRA.md).

An item is closed by recording what was read as a new static finding (`FND-*`, `method: static`,
with addresses in the build's notation), citing it in the entries it concerns, raising or
correcting their statuses, and deleting the item from this file. A reading that contradicts an
entry makes the entry `disputed` until it is settled. Never paste decompiler output or listings
into the repository.

Items are grouped by the part of the game they concern. Within a group they are in no particular
order of priority, unless a group says otherwise.

## Executable, platform and file formats

- Needs a run of the original, not the executable alone: what the extra top
  row of `PX06008` shows (FND-GFX-005); whether `IDirectDraw::CreatePalette`
  with flags 8 fails in full-screen 8-bit play (FND-PLATFORM-011); which
  extension the save dialog appends given `lpstrDefExt` `*.SAV`
  (FND-SAVE-002).

## Shared in-memory structures


## Movement, Control, gangs, equipment and money

- SCR-MOVE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-GANG-001,
  SCR-GANG-002: positions of the Confirm, OK and Cancel controls, the keys each
  panel handler accepts, and the item picture and portrait resources.

## Attack, combat, detection, Chaos and police

- SCR-COMBAT-002: the Force tracks are drawn at panel-local y 116 and 123
  (FND-COMBAT-010) and the capture of FND-UI-010 measured 114 and 121. A new
  capture with its settings recorded settles it; the executable cannot.

## Setup, city generation, objectives and awards


## Computer players

- Scenario numbering (RULE-AI-002, RULE-AI-010, RULE-AI-011, RULE-AI-013,
  RULE-AI-019 to RULE-AI-031, BUG-AI-001; FND-AI-002, FND-AI-005): FND-AI-002
  and FND-AI-005 read scenario 0 as Greed, 6 as Eliminate and 7 as Siege;
  FND-UI-033 and FND-TURN-003 read 6 as Siege and 7 as Eliminate; FND-SETUP-009
  and FND-SETUP-012 read 0 as Kill 'Em All. The AI rules use numeric
  literals and were not renumbered. Settle the numbering from the scenario
  global `0x004ABBE8`'s writers in the setup panel and from the name resource
  the setup presenter loads for each value.
- Selector `0x5B` (RULE-AI-019, RULE-AI-023, RULE-AI-028; FND-AI-019,
  FND-AI-030, FND-AI-031, FND-AI-037): read the call sites in `0x00428EF0` and
  `0x00401000` and check whether the selector takes the counted action as an
  argument, or always counts previous Chaos at `0x004048B8`.
- Full function ranges for every finding that gives only an entry address:
  the family handlers (`0x00428EF0`, `0x00434080`, `0x0041FEF0`, `0x00435BD0`,
  `0x00401000`, `0x0043A1D0`, `0x00431C60`, `0x00436C70`, `0x004605E0`,
  `0x0042A6E0`, `0x00420950`, `0x004353A0`, `0x0040ABC0`, `0x00466910`), the
  dispatcher, `0x00458FA0`, `0x0040A1A7`, `0x00402D70`, `0x00408642`,
  `0x0046DC10`, `0x00472775` (FND-AI-001 to FND-AI-040).
- Family-9 seeding in the outer pass (RULE-AI-001, RULE-AI-027; FND-AI-003):
  the condition under which `0x00458FA0` writes family 9 before the dispatch.
- Meaning of planning record byte +1 (`unk_01`) to the planner (RULE-AI-002, RULE-AI-001;
  FND-AI-002, FND-AI-003). Its writers and its one reader are in FND-STATE-006; +11 is never
  addressed.
- Dispatcher tail (RULE-AI-002; FND-AI-001): the scenario 6, 7, 8 comparisons
  after the handler and the mode 9 Move for roster slot 0.
- Reuse of a roster slot's planning record (RULE-AI-001): where a hire resets
  the record so the new gang does not inherit family and history.
- Hire-role schedule adjustments for every scenario, and what a family-6 guard
  does when it fires (RULE-AI-010, BUG-AI-001; FND-AI-009, FND-AI-014).
- Selector `0x62` (`local_tech_cap`, RULE-AI-005, RULE-AI-026; FND-AI-024): how
  the Tech ceiling is computed.
- Selectors `0x64`, `0x74` and `0x75` (RULE-AI-005): which candidate wins when
  several qualify; the weapon class `type` numbers of the item table.
- Mode 4 of the sector selector (RULE-AI-006), and mode 0 (RULE-AI-007; conflict with
  FND-MOVE-001). The table selector `0x2D` reads is the standings table `0x004ABC08`, searched as
  if it listed player slots (FND-STATE-004); RULE-AI-006 should cite it.
- Family 1 (RULE-AI-020; FND-AI-020): the branches for previous Attack, Bribe,
  Give, Hide, Influence, Move, Research, Sell and Terminate; the Snitch branch
  gated on cash above 50; the Mentality 2 branch; the selector `0x35` test for
  a neutral owner.
- Family 0 and family 4 target pools (RULE-AI-019, RULE-AI-023): whether the
  single and five-draw loops choose the human pool by the owner's hostility, as
  family 3 does.
- Family 2 (RULE-AI-021; FND-AI-032): whether the attack step tests selector
  `0xAB` or the pool size; whether the late Control gates replace an Equip or
  Heal.
- Family 6 (RULE-AI-025; FND-AI-029): the thresholds of the unreachable Heal and
  Control branches; whether the equipment step is gated by selector `0x6C`;
  whether selector `0x5F` counts the planning gang itself.
- Family 7 (RULE-AI-026; FND-AI-035): whether the fixed item list is Tech
  capped; whose owner the Attack hostility test reads.
- Family 11 (RULE-AI-029; FND-AI-024): the armor, miscellaneous and Heal tests.
- Families 13 and 14 (RULE-AI-031; FND-AI-039): instruction addresses of the
  owned-objective Heal branch; the contested pool (owner's gangs or every
  visible opponent); whose Force the Force-5 test reads.
- Attitude updates (RULE-AI-015, RULE-AI-016, RULE-AI-017; FND-AI-006):
  instruction addresses in `0x00472775` of the recovery loop, the combat
  decrement and the takeover decrement; whose reaction the combat decrement
  uses; which ownership changes call the takeover decrement; the address of
  the reaction values.
- Difficulty band reads (RULE-AI-018; FND-AI-007): instruction addresses of the
  nine reads in `0x00472775`; which controller values count as computer.
- Addresses of `sector_weight` and of the player-pair records holding
  `combat_advantage` (RULE-AI-003; FND-AI-018, FND-AI-039), and the order of
  the three parts of `0x0040A1A7`.
- `solo_control_ok` (RULE-AI-004; FND-AI-004): what makes a sector disabled
  (owner -2); whether the unavailable test is the Crackdown byte; the offsets of
  the Income and Support bytes selector `0x2C` reads.
- The auxiliary record block's base and stride (FND-AI-015) and whether the
  planning and auxiliary records are saved.
- Placement anchor (RULE-AI-013; FND-AI-010): the element type at
  `0x0048E2F8`; the order of the keep tests; what the byte at `0x004A08C4`
  holds; what the hire resolver does with sector -1; where `seed_anchor` runs.

## Screens, options, planning timer and sound

Gaps more reading of `Chaos Overlords.exe` or the data files could close.
- RULE-OPTIONS-003: the byte `0x004ABC9C` also suppresses the idle-gang
  warning (FND-OPTIONS-003). Read its writers (`0x004614AF`, `fn_0046E766`) and
  say what it marks.
- FND-UI-031: the scaled copy ignores the copy mode (FND-UI-023). List which of
  the 32 callers of `fn_00427864` pass rectangles of different sizes with a
  keyed or patterned mode, to say whether any visible draw loses its key.
- Needs a run of the original, not static reading: what `RegQueryValueExA`
  writes when an option value is longer than four bytes (RULE-OPTIONS-001), and
  whether the incoming-hire marker of RULE-UI-006 really disappears from all
  but the last such sector after a full map draw (a capture with two sectors
  holding only incoming hires).

## Coverage of the executable

The groups above close questions about entries that already exist. The groups from here on list
the parts of the executable that no entry describes yet, found by comparing a function inventory
of the build with every address the spec cites. The inventory was taken on 2026-09-25 from a
Ghidra 12.1.3 project with default auto-analysis. FND-EXE-004 records the layout and every game
function's range; `tools/ghidra/ReportFunctionInventory.java` and
`node tools/spec-coverage.mjs --inventory <file>` reproduce the counts (see `docs/GHIDRA.md`), and
`spec/index/functions.md` lists the entries that cite each function:

- Ghidra finds 694 functions. Game code runs from `0x00401000` to `0x0047862F`: 464 functions,
  480,397 bytes. The import thunks (Smacker, DirectDraw, WinSock, TAPI, common dialogs) start at
  `0x00478630` and the statically linked C runtime at `0x004787E0`. Inside the game code only
  309 bytes lie outside a function body, in gaps of at most 15 bytes, so auto-analysis missed no
  game code.
- 190 game functions (376,915 bytes) are cited by at least one spec entry. 274 (103,482 bytes)
  are cited by none. 69 of those (27,868 bytes) import WinSock, TAPI or serial functions or are
  called only by functions that do; they are the network play DEV-NET-001 leaves out. That leaves
  205 functions (75,614 bytes) that the groups below assign to subsystems.
- A citation does not mean a function is mapped. 44 functions over 1,000 bytes are cited by one
  or two entries, often a finding about another subject: the main console `fn_0046FD80` appears
  only in FND-AUDIO-012, and the Give handler `fn_00445A4F` (8,290 bytes) only in FND-EQUIP-003.
- Of the 1,108 `.data` addresses game code reads or writes, 362 lie within 16 bytes of an address
  the spec cites. Of the 51 `.rdata` addresses it reads, 3 are cited.
- The bounded random wrapper `fn_0045D227` has 61 call sites in 24 functions. The spec cites 5 of
  them by instruction address.
- The resource section holds 5 menus, 27 dialogs, 7 string-table blocks, 1 accelerator table,
  4 bitmaps, 11 icons in 8 groups and a version record. The spec cites menu 101, dialog `0x8B`
  and a few string IDs.

## Program shell: startup, window, input and shutdown

## Display, drawing primitives and palette

## Movies

- Needs a run of the original: how `smackw32.dll` fits a movie's palette in the
  8-bit display set (blit type 3 and `SmackColorRemap`, FND-VIDEO-002) and how
  it treats the volume `effects_level * 25 * 256`, above its normal level from
  level 6 up. The executable's side is recorded in RULE-VIDEO-001.

## Sound, music and the CD drive

- Needs a run of the original: how the GOG build's `winmm.dll` answers the MCI
  status query and the resume command, and whether the resume plays past the
  last track of the program (FND-AUDIO-007, RULE-AUDIO-002); and how
  `GetDriveTypeA` answered the strings `.\` to `Y\` on the Windows versions of
  the time (FND-PLATFORM-012, RULE-AUDIO-010).

## Menus, dialogs, help and other resources

- Needs a run of the original, since Windows decides it: whether
  `TranslateAcceleratorA` sends the command of a greyed menu item, so whether
  Ctrl+S saves during resolution or Backspace acts as Host on the title when
  Host is greyed (SCR-UI-009, RULE-UI-014). The static reading of the menus,
  dialogs, popups, accelerators and help is closed by FND-EXE-005, FND-UI-021,
  FND-UI-022, FND-HELP-005 and RULE-HELP-001.

## Timing, delays and arithmetic helpers

## Screen and panel code no entry describes

- City screen: whether FND-UI-033's map origin `(2,44)` or FND-UI-017's `(2,42)` is right
  (SCR-UI-003). The copy, the selection frame and the cell restore all use `(2,42)`; a screenshot
  of the original settles it.

## Game-side code and data no entry describes

- `fn_00409F47` (608 bytes), `fn_0040AA65` and `fn_0040AAE3` are called from planning entry
  `fn_0046E766` and call the AI selector `fn_00402D70` and `fn_0040A1A7`. Record their role in
  the AI planning pass (the per-player part of RULE-AI-003 is a candidate).

## Found while integrating the spec

- RULE-AI-016, RULE-AI-017, RULE-CONTROL-001: FND-AI-047 records the instructions in
  `fn_00472775` that lower the attitude after an attack (every Attack order, evaded ones included,
  by the larger of the reaction and the opening damage; the retaliation writes no cell) and at a
  Control takeover (twice the previous owner's reaction, only when there was an owner). The three
  rules still have to cite it and match it (AI agent). RULE-ATTACK-001 already does.
