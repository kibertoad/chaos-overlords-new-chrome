---
id: FND-EXE-005
title: The resource section holds five menus, one accelerator table, 27 dialogs, 104 strings, four bitmaps, eight icon groups and a version record, and the code loads each kind through one place
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004B0000..0x004C1BEB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00466673..0x0046678C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465620..0x00465A94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046508C..0x004650B0
tool: Ghidra 12.1.3 and a resource listing of the executable
environment: null
---

## Observation

The `.rsrc` section (FND-EXE-002) holds these resources, all with language
1033. Addresses are virtual addresses of the data.

| Kind | IDs | Data |
|---|---|---|
| Menu | 1, 2, 3, 5 | `0x004B11E4` (322 bytes), `0x004B1328` (182), `0x004B13E0` (258), `0x004B14E4` (166) |
| Menu | 101 | `0x004B0D00`, 1252 bytes |
| Accelerators | 102 | `0x004B158C`, 48 bytes, six entries |
| Dialog | 128 to 141, 143, 144, 145, 201, 20000, 20002 to 20007 | `0x004B15BC..0x004B39E3`, 190 to 506 bytes each |
| Dialog | `DIALDIALOG`, `DIRECTDIALOG` (named) | `0x004B29CC` (1178 bytes), `0x004B3598` (390) |
| String table | blocks 1 to 7, strings 1 to 104 | `0x004C0778..0x004C1BEB` |
| Bitmap | 143, 146, 147 | 80 bytes each: 8 by 8, one bit per pixel |
| Bitmap | 148 | `0x004B3AD4`, 41,072 bytes: 512 by 340, eight bits per pixel |
| Icon group | 152, 153, 158, 159, 160, 164, 166, 167 | eleven icon images; 152 holds 32, 16 and 48 pixel images |
| Version | 1 | `0x004C0448`, 816 bytes |

There is no cursor, font or custom resource.

Strings. The 104 strings fall into groups by ID; their texts are not reproduced
here:

| IDs | Role |
|---|---|
| 1 to 10 | scenario names |
| 11, 12 | the game's name for a local and a network game |
| 13 to 18 | player colour names |
| 19 to 24 | progress labels |
| 25 to 29 | item category labels |
| 30 to 32 | site bonus lines |
| 33 to 45 | event report lines |
| 46 to 49 | difficulty levels |
| 50 to 53 | planning time limits |
| 54 to 57 | game lengths |
| 58 to 60 | controller labels |
| 61 | the prefix of default player names |
| 62 to 76 | fifteen Overlord names |
| 77 to 94 | network status lines |
| 95 to 104 | scenario descriptions |

Loaders. Each kind of resource is loaded in one place:

- Strings: `LoadStringA` (import slot `0x004AE7F4`) is called once, at
  `0x0046669C` in `fn_00466673(dest, id, width, pascal)`, which pads or cuts
  the string to `width` and stores it as a Pascal or a zero-terminated string.
  `fn_00466673` has 13 callers.
- Menus and accelerators: `LoadMenuA` (`0x004AE83C`) at `0x00465A11` and
  `LoadAcceleratorsA` (`0x004AE838`) at `0x004659D2`, both in `fn_00465620`
  (FND-PLATFORM-009). Menu 101 reaches the window through the window class.
  `TrackPopupMenu` (`0x004AE868`) is called only at `0x00425715` (FND-UI-021).
- Icons: `LoadIconA` (`0x004AE82C`) with 152 at `0x00465956` and `0x0046596A`.
  Icon groups 164, 166 and 167 are named by dialog templates; 153, 158, 159
  and 160 are named nowhere.
- Dialogs: `DialogBoxParamA` (`0x004AE810`) and `CreateDialogParamA`
  (`0x004AE8BC`) as listed in FND-UI-022.
- Bitmaps: `LoadBitmapA` (`0x004AE8E0`) at `0x00428071..0x004280BC` in the
  pattern compositor `fn_00427E60` for 143, 146 and 147 (FND-GFX-004). The only
  other request is for bitmap 107, which does not exist, in `fn_00426F77`,
  which has no callers. Bitmap 148 is never loaded.
- Help: `WinHelpA` (`0x004AE840`) is called only at `0x004650A1` (FND-HELP-005).
- `PtInRect` (`0x004AE8DC`) is called only at `0x00449BC2` (FND-UI-020).

## Interpretation

The resources are the Windows shell of the game: menus, dialogs, icons and the
strings the game formats itself. Every screen image comes from the data files.
The 512-by-340 bitmap 148 and four icon groups are left over from development.

## Alternatives

None. The resource directory and the import slots can be read directly.

## How to reproduce

Walk the resource directory of the `.rsrc` section with any PE reader and list
type, ID, data address and size. List the references to each import slot named
above.
