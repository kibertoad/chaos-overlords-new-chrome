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

- All `.ksy` files of this task (fmt_data_001..003, fmt_gfx_001..003,
  fmt_audio_001..002, fmt_video_001, fmt_help_001, fmt_save_001..002): every definition
  now compiles with kaitai-struct-compiler 0.11; parse the shipped files with the
  generated readers and compare every field with the entry. `fmt_gfx_002` names a process routine
  `rule_gfx_001` that has to be supplied; `fmt_audio_002` and `fmt_video_001`
  use `_index` in repeated sizes. Changes the Coverage sections of every
  format.
- FND-PLATFORM-001..008, FND-SAVE-001, FND-ASSET-001: several functions are
  located by their entry address alone. Give the full address range of each.
- FMT-GFX-001, FMT-GFX-002, FND-GFX-003: collect the width and height the
  executable passes to the image loader for each image number, in particular
  `PX00202`, `PX00203` and `PX06008`, whose file data give 311, 311 and 241
  where the older notes say 312 and 242. Look at the callers of the loader
  and the constants they push.
- FMT-DATA-004, FND-PLATFORM-007: find which `CLT` number the palette loader
  opens (the template reads `CLT00000`; the only file is `CLT00002`), and
  whether the colour table inside each PX08 file is ever used or replaced by
  the CLT palette.
- FMT-DATA-001..003: list the field offsets the executable reads from the
  `SITES`, `Gangs` and `ITEMS` records (starting from `0x004782C5`, the
  equipment resolver, and `0x0042E040`, which reads an item's sound byte) and
  the in-memory addresses the tables are copied to. Rows still resting on the
  source alone would become supported.
- FMT-DATA-005: confirm that no code path, including the installer
  components, opens `DATA.Z`.
- FND-PLATFORM-004: map the WinSock and `smackw32.dll` imports taken by
  ordinal to names, and find the callers of the TAPI and serial functions.
- FND-ASSET-001: explain the strings with a leading space
  (`" data\PX08\px00128"`, `" data\PX16\px00128"`, `" A:\CHAOS\CDTrack"`):
  whether the space is skipped by the code that uses them.
- FND-PLATFORM-002: give the exact test that chooses the 8-bit or 16-bit image
  set (display depth query and threshold).
- FMT-SAVE-002: find what writes an `M10W` file, if anything, and what the
  caller does with the load result 3; identify the 12-byte global.
- FMT-SAVE-001: identify blocks 16, 17, 18, 21, 36 and 37, confirm blocks 3,
  4, 8, 13, 27, 33 and 39 (now resting on the source), and name which
  preference each of the three preference bytes is.

## Shared in-memory structures


## Movement, Control, gangs, equipment and money

- SCR-MOVE-001, SCR-EQUIP-001, SCR-GIVE-001, SCR-SELL-001, SCR-GANG-001,
  SCR-GANG-002: positions of the Confirm, OK and Cancel controls, the keys each
  panel handler accepts, and the item picture and portrait resources.

## Attack, combat, detection, Chaos and police

- SCR-ATTACK-001, SCR-COMBAT-001, SCR-COMBAT-002: which resources surfaces 3
  and 5 hold while the picker, Combat Results and Detailed Combat are open,
  which gives the files of the portraits and equipment icons; the picker's
  event 5 branch (`0x0043C4BC`), which tests the portrait and equipment
  rectangles; the argument order of the colour helper behind the Combat
  Results outlines (FND-ATTACK-003, FND-COMBAT-010, FND-COMBAT-012).
- SCR-COMBAT-002: the Force tracks are drawn at panel-local y 116 and 123
  (FND-COMBAT-010) and the capture of FND-UI-010 measured 114 and 121. A new
  capture with its settings recorded settles it; the executable cannot.
- RULE-COMBAT-004: the fight list's record area holds four entries
  (FND-COMBAT-011). Check whether one sector can list more than four gangs
  against one focal gang.

## Setup, city generation, objectives and awards

- SCR-SETUP-001, RULE-SETUP-002, RULE-OBJECTIVE-004: find the local setup
  handler's rectangles for the scenario buttons, time limit, AI Mentality and
  turn time controls, and the globals they write (the time limit gives
  `turn_limit`; its address is unknown). Also the positions of the top
  portrait strip, the names and the selection lights.
- RULE-OBJECTIVE-004: write out the Dominance numerator in `0x0047712A` and
  the end tests of each scenario in `0x00476857` (thresholds 40 and 64, Siege
  test, timed end test), which the rule takes from the manual.
- RULE-SETUP-001, RULE-SETUP-005, RULE-SETUP-006, RULE-SETUP-007: record the
  addresses of the five `modifier_name_*` strings and of the name-compare
  helper; confirm whether the scan is one pass or several.
- RULE-SETUP-003, RULE-SETUP-009, RULE-SETUP-010: find the `portrait` table's
  address and the stepping rules of `fn_00468D87` and `fn_00468CFC` (skipping
  used portraits, wrapping at 0 and 14).
- RULE-SETUP-004: find the address of `reaction`, and whether computer and
  human players both get a draw (coordinate with the AI task, FND-AI-006).
- RULE-SETUP-008: find where `new_local_game` lives and is cleared, and what
  `fn_0045519D` and `fn_00451F80` show.
- RULE-OBJECTIVE-001, RULE-OBJECTIVE-005: addresses of `match_over`,
  `player_retired` and `player_active`, and where the elimination walk sits in
  the turn relative to resolution and the next planning scan.
- RULE-AWARDS-001, SCR-AWARDS-001: address and layout of `player_awards`, the
  icon cells in `PX00201`, the Awards and Stats rectangles in `0x0042CE61`,
  which statistic each of the five fields holds, and which tab shows first.
- SCR-AWARDS-002, RULE-AWARDS-002: where `PX00202` is drawn, how it is
  dismissed, whether the shared results follow it, and whether the human count
  includes controller 3.
- SCR-OBJECTIVE-001: the vertical position formula of the rankings portraits
  in `0x004518D9`, the portrait source, and how the panel closes.
- SCR-SETUP-002: position of `PX00132`, the portrait drawn on it and the Ready
  rectangle in `0x004396C0` and `0x00439F7A`.
- SCR-NET-001, SCR-NET-002, SCR-NET-003: settle the order of the four numbers
  in the held-button rectangles (read the helper's comparisons), and name each
  action.
- SCR-NET-004, SCR-NET-005: the progress-to-width mapping in `0x0040D3C0` and
  `0x0046D77B`, the spinner cell arithmetic and timer period, and the
  `PX00139` destination.
- All screens: where the 640-by-460 drawing surface sits on the 640x480
  window (menu bar offset).

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
### Addresses and ranges
- Give full function ranges for every finding location written as a single
  entry address in FND-UI-*, FND-AUDIO-001..003, FND-AUDIO-010..013,
  FND-OPTIONS-*, FND-TIMER-001 (the extents were not in the old notes).
- Find the addresses of `music_enabled`, `effect_slots`,
  `sound_output_available` (and its writer), `pointer_shape`,
  `presentation_tick_pending` (timer slot 0), `comlink_blink_step`,
  `planning_limit_ms`, `planning_start_ms` and `blit_benchmark_count`, and add
  them as locations and glossary addresses. Rules: RULE-AUDIO-003, 004, 005,
  008; RULE-UI-003, 007, 008; RULE-TIMER-001, 002, 003.
- Record the widths of `local_game` (`0x00482178`), `network_game`
  (`0x00487B58`) and `comlink_alert_repeat` (`0x00487808`), and read the
  writers of the two game-type flags to confirm their meaning (RULE-AUDIO-006).
### Behaviour
- RULE-OPTIONS-001: what the loader does when `RegOpenKeyExA` fails; the
  shared buffer's value before the first query; how the two serial draws are
  combined and whether the result is kept in `serial_number`; the effect of a
  value of another type or size.
- RULE-OPTIONS-002: which player actions reach the writer's three callers
  (`0x00460EF9`, `0x00460F40`, `0x0046224A`).
- RULE-OPTIONS-003: confirm the idle scan covers only the active player's 81
  slots.
- RULE-TIMER-001/002/003: whether the limit mapping is a table or compares;
  where the -1 "no limit" case is tested (a signed compare would expire at
  once); the types in the expiry compare; what happens to an open modal panel
  on expiry; the countdown's value at planning start; whether the bar is drawn
  with no limit; whether a loaded game recomputes the limit from the saved
  choice byte.
- RULE-AUDIO-001..003: the effect of a nonzero music level after 0 on
  `music_enabled`; the order of music and effects application; which auxiliary
  device the volume call addresses; which callers run the level helper.
- RULE-AUDIO-004/005: behaviour on a missing sound file; the lower helper's
  slot bounds check; what the priority and channel record do.
- RULE-AUDIO-009: what the evasion branch leaves in slot 5; whether "base"
  Martial Arts is the definition's or the effective value.
- RULE-UI-003: the last partial slide step; the divisor when
  `blit_benchmark_count` is below 4; whether the slide-out redraws what is
  underneath.
- RULE-UI-004: cell walk direction and overflow; the red digits' source x.
- RULE-UI-006: whether incoming hires come from `hire_orders`.
- RULE-UI-007: which callers pass `force`.
- RULE-UI-008: whether timer slot 0 is a flag or a counter; what else is paced
  by it (Item Information rotation, the Comlink Send cursor).
- RULE-UI-009: the string resource IDs of the scenario names; whether the
  eliminated test reads `player_active`; the label for `controller` 3.
- RULE-UI-010: whose roster Gangs in Sector scans; more than six matches.
- RULE-UI-011: whether Income and Tolerance are hidden from non-owners; which
  number helper draws the values.
- SCR-UI-005: the close face rectangle and the keys the panel takes; which
  gang field each statistic row reads and which rows Base Statistics switches.
- SCR-UI-006: the screen row of Cost and Tech Level (backing y 236); the timer
  behind the item rotation; how Equip and Research open the panel.
- SCR-UI-007, SCR-UI-008: the keys each panel takes; whether Site Information
  shows remaining or base Resistance.
- SCR-UI-008: the code that opens Game Information at the start of a
  multi-player game and after loading (RULE-SETUP-008 calls `fn_0045519D` for a
  new local game).
- SCR-OPTIONS-001: the sounds Cancel and OK play; a click outside both faces.
- FND-UI-031: whether the scaled copy path ignores the requested copy mode
  (possible GFX bug).

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

- `fn_00460CCF` is WinMain (5,565 bytes, called from the runtime entry at `0x00478E4B`). Eight
  findings cite it for single details. Record its whole order as a rule: any single-instance
  check (shutdown releases a mutex), the preference load (RULE-OPTIONS-001), display setup
  `fn_00425850`, sound setup `fn_00458290`, the CD drive search `fn_004667DC`, the image set
  choice (FND-PLATFORM-002), the intro `fn_004329C0`, the title loop, and shutdown `fn_00465A95`
  (menu destroyed, mutex released, timer period ended).
- The window procedure `fn_0045C33B` (2,398 bytes, no direct callers; registered by
  `fn_00425850`) is described nowhere. List every message it handles (it compares with `0x100`,
  `0x102`, `0x111` and `0x3B9`: key down, character, menu command and the MCI notification) and
  what each does, including its calls into `fn_00423C81`, `fn_0042B60F`, save and load
  `fn_0046381A`, and `fn_00465BC8`. Record how keys are read (`MapVirtualKeyA`,
  `GetAsyncKeyState`) and how the mouse position is read (`GetCursorPos`). This becomes the input
  dispatch rule (UI area) that every screen's keyboard and mouse questions above depend on.
- The two message pumps: `fn_0045C180` (4 callers; `GetMessageA`, `IsDialogMessageA`,
  `TranslateAcceleratorA` with accelerator table 102, `timeGetTime`) and `fn_0045C2CD` (22 callers;
  `PeekMessageA` without waiting). Record which modal loops use which, and what a panel's loop
  does while the pump runs.
- The block `0x004980A0..0x00498125` is written by every panel handler (Attack, Equip,
  Influence, Move, Research, Sell, Give, Search, gang panels) and read by the pumps, the window
  procedure and the Comlink panels. Identify its fields (the open panel, the last click, the last
  key are expected) and record it as a FMT-STATE entry for the input state.
- `0x00498570..0x004985DA` is written by `fn_00425850`, the window procedure, `fn_00465620` and
  `fn_00465B64`, and read by 25 functions: give each field (window handle, instance and so on) a
  glossary name.
- `fn_0045CD70` and `fn_0045CDA4` wrap `BeginPaint` and `EndPaint` for 39 callers each. Record
  what a repaint of the window redraws and from where (see the presentation model below).

## Display, drawing primitives and palette

- Display setup `fn_00425850` (1,351 bytes: `CreateWindowExA`, `AdjustWindowRectEx`,
  `DirectDrawCreate`, `SetSysColors`, `SystemParametersInfoA`, the `MS Sans Serif` font) and its
  undo `fn_00425D97` (`ChangeDisplaySettingsA`, `SelectPalette`, `RealizePalette`,
  `SetSysColors`). Record the window style and client size, whether and to what the display mode
  is changed, what DirectDraw is used for, what the font is used for, and which system colours are
  replaced and restored. This also settles "where the 640-by-460 drawing surface sits on the
  window" in the Screens group.
- The drawing primitives every screen calls are not described: `fn_00425E99` (44 callers),
  `fn_00425F4D` (57), `fn_00425F8C` (60), `fn_0042639F` (48), `fn_00426405` (48), `fn_00426575`
  (42, a GDI rectangle with pen and brush), the off-screen bitmap setup `fn_00425FB0` and its
  release `fn_00426202`. With the cited `fn_00425EDF`, `fn_0042773E`, `fn_00427864`,
  `fn_004266A6` and `fn_00449B20` they form the drawing layer. For each, record the arguments and
  the effect (surface, rectangle, colour, copy mode). Then record the presentation model as a
  GFX rule: what is drawn off screen, when and how it reaches the window, and whether only changed
  rectangles are copied. Pixel parity of every SCR entry rests on it.
- Name the drawing layer's state: `0x00493830..0x00493878` (written and read by the primitives;
  current pen, brush or colour is expected), `0x00493598..0x004935F8` and
  `0x00493658..0x00493680` (written once by `fn_00425850`, read by 24 functions; device context
  and bitmap handles are expected).
- GDI shape helpers with no direct callers: lines `fn_00426427`, `fn_004264D4`; rectangle
  `fn_00426909`; ellipses `fn_00426A37`, `fn_00426B7D`, `fn_00426CAB`, `fn_00426E1D`; and
  `fn_00425F14`, `fn_00428E6A`, `fn_0042B7E3`. Search for their entry addresses stored as data
  (pointer tables) and record each as reachable, with its caller, or as dead code.
- Palette: `fn_0042885B` opens `data\CLT00000`, reads 1,024 or 1,032 bytes and creates a palette,
  and has no direct callers. `fn_00428BAB` builds a palette from the device capabilities;
  `fn_004289D3` and `fn_00428ADC` select and realize one. Find how `fn_0042885B` is reached, if at
  all, and what happens when `CLT00000` is missing (the build ships only `CLT00002`). This
  answers part of the FMT-DATA-004 item in the first group.

## Movies

- The intro `fn_004329C0` (992 bytes) opens `Data\mvIntro` and `Data\mvLogos` and raises the
  thread priority. Its Smacker wrappers are `fn_0040DBC0` (open), `fn_0040DDFF` (frame loop:
  decode, copy to a rectangle, colour remap, new palette, wait), `fn_0040DD7B` (close),
  `fn_0040E049` (volume and pan), `fn_0040DAB9`, `fn_0040DAE0` and `fn_0040E00E`; `fn_0040DFFE`
  and `fn_0040E02B` have no callers. The VIDEO area has two entries and no rule for playback.
  Record the order of the two movies, which keys or clicks end each, where a frame lands on the
  screen, how the palette is handled in the 8-bit set, the sound setting, and what happens when a
  file is missing or fails to open. DEV-VIDEO-001 then cites the rule.
- `0x00490598..0x004905E0` is read by the Smacker wrappers and by the network screens
  (`fn_0040B9C0`, `fn_0040C4C5`, `fn_0046913D`). Identify what they share.

## Sound, music and the CD drive

- Sound setup `fn_00458290` builds paths from `data\snd00000`, counts auxiliary devices and reads
  the registry through `fn_0042B8EC`. `fn_004589B8` uses the same path and `PlaySoundA` and has no
  direct callers. `fn_00458895` is the play primitive; `fn_00458858` and `fn_004584BA` sit above
  it. FND-AUDIO-002 covers the slots. Record the path construction and file numbering, how a
  missing file is handled, and whether `fn_004589B8` is dead.
- CD music through MCI: `fn_00458EA6` opens the device (commands `0x803` and `0x80D`),
  `fn_00458CA0` plays (`0x808`), `fn_00458CEF` queries status (`0x814`), `fn_00458D54` waits, and
  `fn_00458ACC` and `fn_00458E2F` read the auxiliary volume. The window procedure receives the MCI
  notification (`0x3B9`). FND-AUDIO-001 says which track programs play; record the command
  sequence, the time format, the track numbers per program, what the notification restarts, and
  what happens with no disc.
- CD drive search: `fn_004667DC` (`GetLogicalDrives`, the string `" A:\CHAOS\CDTrack"`),
  `fn_0046678D` (`GetDriveTypeA`) and `fn_0046638E` (`GetDriveTypeA`, `GetVolumeInformationA`;
  called from WinMain and the main console). Record which drive letters are tried and in what
  order, the drive type and volume label accepted, what the path string is used for, and what the
  player sees without a disc. This ties to the leading-space item of FND-ASSET-001. The rebuild
  does not reimplement any of it: it plays the music tracks from the files of the GOG release
  (`MUSIC/TrackNN.ogg`, docs/AUDIO-VIDEO.md) and never looks for a drive. Document the original's
  behaviour in the spec (a PLATFORM finding and an AUDIO rule) and add a DEV-AUDIO entry that
  names it as not reproduced, so its PARITY rows are accounted for. No setting is needed, since it
  changes no game state.
- Name the audio state: `0x00494BF0..0x00494C32` (written by `fn_00451F80`, `fn_00458290`,
  `fn_00458B43`, `fn_00458CEF`, `fn_00458EA6`) and `0x00497FE8..0x00498012` (written by
  `fn_00458290` and `fn_00458EA6`). Several of the glossary addresses the Screens group asks for
  (`music_enabled`, `effect_slots`, `sound_output_available`) are expected here.

## Menus, dialogs, help and other resources

- Record the resource section as an EXE finding: every menu (1, 2, 3, 5, 101), dialog
  (`DIALDIALOG`, `DIRECTDIALOG`, 128 to 141, 143 to 145, 201, 20000, 20002 to 20007),
  string-table block (1 to 7), the accelerator table (102), bitmap (143, 146, 147, 148), icon
  group (152, 153, 158, 159, 160, 164, 166, 167) and the version record, with its size and the
  function that loads it. Find the loaders by the ID pushed before `LoadMenuA`,
  `DialogBoxParamA`, `CreateDialogParamA`, `LoadStringA`, `LoadAcceleratorsA`, `LoadBitmapA` and
  `LoadIconA`. Record IDs and roles only; the spec never reproduces the texts.
- Accelerator table 102: record each key and the command it sends, and whether the window
  procedure handles those commands the same way as the menu items. The keyboard items of every
  SCR entry depend on it.
- Menu state helpers: `fn_0042533F` and `fn_0042548A` (13 and 16 callers, pushing 1024 and
  1025), `fn_004255D5` (redraws the menu bar, 13 callers), `fn_00425601` (check marks),
  `fn_0042572C` (switches the whole menu), `fn_004257D7` (no callers), and `fn_004120A7` and
  `fn_004120CB` (27 callers each, both pass 129). Record which items each greys out and when (open
  panels, planning, resolution), which menu resource each game state uses, and add it to
  SCR-UI-009.
- `fn_0042566D` opens a popup menu (`TrackPopupMenu`) from the city screen handlers
  `fn_00414D8C` and `fn_0041462F`. Record which menu resource it shows, where, and what each item
  does. No entry mentions a popup menu.
- Dialogs: `fn_00465EC6` (1,219 bytes, no direct callers, controls 1003 to 1015) is a dialog
  procedure, reached through `fn_00465CEC` (`DialogBoxParamA`, 15 callers, compares with 20000
  and 20002) or `fn_00465DD5` (`CreateDialogParamA`). Record which dialog each caller opens, the
  controls and their effects, and which dialogs a local game can reach. `DIALDIALOG`, `DIRECTDIALOG`
  and the TAPI dialogs belong to network play and need only be listed.
- Help: `fn_0046508C` calls `WinHelpA` with `.\Help\Chaos.hlp` and has no direct callers. Record
  how it is reached (a menu or accelerator command in the window procedure is expected) and the
  command and context it passes. DEV-HELP-001 then has a rule to depart from.

## Files, registry and the save dialogs

- File layer: `fn_0042B60F` (`CreateFileA`, called from WinMain, the sound loader and planning
  entry `fn_0046E766`), `fn_0042AB80`, `fn_0042ABFB`, `fn_0042B7F3` and `fn_0042B87B` sit around
  the cited `fn_0042AC7A..fn_0042B27A` (FND-PLATFORM-003) and share `0x00493F88..0x00493FE7`.
  Record path building, open modes, error handling (what the player sees on a missing or short
  file) and which file `fn_0042B60F` opens at planning entry.
- `fn_0042B8EC` reads a value under `SOFTWARE\Microsoft\Windows\CurrentVersion` (buffer of 260
  bytes). Record which value and where the result goes. RULE-OPTIONS-001 covers a different key.
- Save and load dialogs: the thunks for `GetOpenFileNameA` and `GetSaveFileNameA` (`0x00478630`,
  `0x00478636`) and `fn_00458155`, which the save and load path `fn_00463CC5` calls. Record the
  default directory and extension, the filter's roles, the overwrite prompt and what a failed load
  does. DEV-SAVE-001 departs from the files, not from this flow.

## Timing, delays and arithmetic helpers

- `fn_0043287C` and `fn_00432897` (`timeGetTime`), `fn_00432847` (`timeKillEvent`),
  `fn_00464B43` (no callers; draws and waits), `fn_00464CD9` (7 callers; waits through
  `fn_00462579`) and `fn_00418F16` (called from planning entry; constants 1,280, 1,408, 2,000,
  3,000 and 4,999). Record every presentation delay with its length and whether it depends on
  processor speed, as a TIMER or UI rule. Speed tied to the processor may be fixed under the
  fidelity rules, so each delay needs its source known.
- Floating-point helpers `fn_0045CDE0`, `fn_0045CE26`, `fn_0045CE61`, `fn_0045CEBE` (compares
  with 360), `fn_0045CF05`, `fn_0045CF99` (pushes 180; no direct callers) and `fn_0045D17E` do
  angle arithmetic through the runtime's `__ftol`. Find what uses them (the rotating item picture
  of SCR-UI-006 or the network spinner are candidates) and record the rounding, since `__ftol`
  truncates.

## Screen and panel code no entry describes

- City screen and sector view, what is left after FND-UI-015, FND-UI-017, FND-UI-018, FND-UI-019
  and FND-SETUP-017: read the code that fills surface 2 to say what its second copy of the city
  map from y 420 holds (FND-UI-018 enlarges the sector view's background from it), and whether
  FND-UI-033's map origin `(2,44)` or FND-UI-017's `(2,42)` is right (SCR-UI-003).
- The rectangle test `fn_00449B78` (`PtInRect`, 49 callers) decides every click. Record its
  argument order and whether the right and bottom edges are inside; that settles "the order of
  the four numbers in the held-button rectangles" in the Setup group for every screen at once.
  Also identify `fn_00449BFC` (33 callers, no callees), `fn_00449BD2`, `fn_00449C41`,
  `fn_00465B64` (16 callers), the cursor helpers `fn_00449CAE` and `fn_00449CC6`
  (`ShowCursor`) and `fn_00465B27` (`GetKeyState`).

## Game-side code and data no entry describes

- `fn_00409F47` (608 bytes), `fn_0040AA65` and `fn_0040AAE3` are called from planning entry
  `fn_0046E766` and call the AI selector `fn_00402D70` and `fn_0040A1A7`. Record their role in
  the AI planning pass (the per-player part of RULE-AI-003 is a candidate).
- `fn_00449CDE` is called from `fn_0046DC10` during city creation. Record what it does and cite it
  from the CITY rules.

## Boundary of the network code

- 69 functions use WinSock, TAPI or serial imports or are called only from such functions. More
  are reached only from the network screens: `fn_0046981D` and `fn_0046A115` (constants 1,200,
  1,920, 2,304 and 2,592), `fn_0042202D` (the handshake strings), `fn_004211E0`, `fn_00423C81`,
  `fn_0046D00D`, `fn_0046913D`, `fn_0040C4C5`, `fn_004217C0`, and `fn_004688CA..fn_00468C0E`.
  DEV-NET-001 leaves all of it out, but the boundary is not recorded. Some of it is called from
  local code: `fn_00421A2D` has 12 callers, and the send routine `fn_00423749` is called from the
  Comlink recorder `fn_0045D2F0` and the hot-seat handoff `fn_004396C0`. Record a NET finding that
  lists the network functions and every call into them from local code with the test that skips
  it in a local game (`network_game`, `0x00487B58`, is expected). That shows the local game never
  depends on them, and closes the 27,868 bytes as out of scope rather than unread.

## Found while integrating the spec

- RULE-AI-016, RULE-AI-017, RULE-CONTROL-001: FND-AI-047 records the instructions in
  `fn_00472775` that lower the attitude after an attack (every Attack order, evaded ones included,
  by the larger of the reaction and the opening damage; the retaliation writes no cell) and at a
  Control takeover (twice the previous owner's reaction, only when there was an owner). The three
  rules still have to cite it and match it (AI agent). RULE-ATTACK-001 already does.
