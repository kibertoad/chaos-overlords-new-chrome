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

## Attack, combat, detection, Chaos and police

- SCR-COMBAT-002: the Force tracks are drawn at panel-local y 116 and 123
  (FND-COMBAT-010) and the capture of FND-UI-010 measured 114 and 121. A new
  capture with its settings recorded settles it; the executable cannot.

## Computer players

Nothing is left to read statically. The starting threshold of the family-13
and family-14 Support scan (BUG-AI-006) needs a run of the original.

## Screens, options, planning timer and sound

Gaps more reading of `Chaos Overlords.exe` or the data files could close.
- Needs a run of the original, not static reading: what `RegQueryValueExA`
  writes when an option value is longer than four bytes (RULE-OPTIONS-001), and
  whether the incoming-hire marker of RULE-UI-006 really disappears from all
  but the last such sector after a full map draw (a capture with two sectors
  holding only incoming hires).

## Coverage of the executable

The groups above close questions about entries that already exist. The groups from here on
listed the parts of the executable that no entry described, found by comparing a function
inventory of the build with every address the spec cites. The inventory was taken from a Ghidra
12.1.3 project with default auto-analysis. FND-EXE-004 records the layout and every game
function's range; `tools/ghidra/ReportFunctionInventory.java` and
`node tools/spec-coverage.mjs --inventory <file>` reproduce the counts (see `docs/GHIDRA.md`), and
`spec/index/functions.md` lists the entries that cite each function. Measured on 2026-09-25:

- Ghidra finds 694 functions. Game code runs from `0x00401000` to `0x0047862F`: 464 functions,
  480,397 bytes. The import thunks (Smacker, DirectDraw, WinSock, TAPI, common dialogs) start at
  `0x00478630` and the statically linked C runtime at `0x004787E0`. Inside the game code only
  309 bytes lie outside a function body, in gaps of at most 15 bytes, so auto-analysis missed no
  game code.
- All 464 game functions (480,397 bytes) are cited by at least one spec entry. The network code
  DEV-NET-001 leaves out is 124 functions, 73,353 bytes: the 118 of FND-NET-004 and six more of
  FND-NET-005, which also names the serial and modem helpers inside FND-NET-004's ranges and what
  keeps each out of a local game. Dead code and empty functions are recorded as such (FND-NET-005,
  FND-UI-028, FND-DATA-008).
- A citation does not mean a function is fully described. 4 functions over 1,000 bytes are cited
  by only one or two entries: `fn_00419AA8` (1,580 bytes; FND-UI-018, FND-UI-026), the Telephony
  callback `fn_0041D51B` (1,327; FND-PLATFORM-013, FND-STATE-011), `fn_00419022` (1,271;
  FND-AUDIO-010, FND-UI-032) and `fn_00427A09` (1,111; FND-PLATFORM-008, FND-UI-026).
- Of the 1,157 `.data` addresses game code reads or writes, 880 lie within 16 bytes of an address
  the spec cites; the regions around the rest are mapped by FND-STATE-007, FND-STATE-008 and
  FND-STATE-011, which cite region bounds and record sizes rather than every field. All 9 `.rdata`
  addresses the code reads are cited. Of the 48 `.rsrc` addresses Ghidra links to code, 5 are
  cited by address; the spec names resources by ID (FND-EXE-005).
- All 61 call sites of the bounded random wrapper `fn_0045D227` are cited by instruction address.

Every group below that listed code no entry described is closed, and its heading has been
removed; the groups left hold only questions that need a run of the original.

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

## Screen and panel code no entry describes

- City screen: whether FND-UI-033's map origin `(2,44)` or FND-UI-017's `(2,42)` is right
  (SCR-UI-003). The copy, the selection frame and the cell restore all use `(2,42)`; a screenshot
  of the original settles it.
