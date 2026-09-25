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

- Family 1 (RULE-AI-020; FND-AI-020): the branches for previous Attack, Bribe,
  Give, Hide, Influence, Move, Research, Sell and Terminate; the Snitch branch
  gated on cash above 50; the Mentality 2 branch; the selector `0x35` test for
  a neutral owner.
- Family 2 (RULE-AI-021; FND-AI-032): whether the attack step tests selector
  `0xAB` or the pool size; whether the late Control gates replace an Equip or
  Heal.
- Family 6 (RULE-AI-025; FND-AI-029): the thresholds of the unreachable Heal and
  Control branches; whether the equipment step is gated by selector `0x6C`;
  whether selector `0x5F` counts the planning gang itself.
- Family 7 (RULE-AI-026; FND-AI-035): whose owner the Attack hostility test reads
  (the drawn gang's or the compared gang's). The fixed Research list is Tech capped
  (FND-AI-055).
- Family 11 (RULE-AI-029; FND-AI-024): the Heal test. The armor and
  miscellaneous choices are selectors `0x64` and `0x74` (FND-AI-055).
- Families 13 and 14 (RULE-AI-031; FND-AI-039): instruction addresses of the
  owned-objective Heal branch; the contested pool (owner's gangs or every
  visible opponent); whose Force the Force-5 test reads.

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

## Found while integrating the spec

