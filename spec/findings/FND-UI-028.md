---
id: FND-UI-028
title: A static initializer copies the Full Screen default before WinMain, five 16-byte functions do nothing, and a list-box helper has no caller
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CA0..0x00460CCE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482000..0x0048200B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004255F1..0x00425600
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464AD6..0x00464AE5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004653AE..0x004653DD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00465E9B..0x00465EC5
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges are in FND-EXE-004.

Static initializer. `fn_00460CA0` (21 bytes) has no caller; its address is
the dword at `0x00482004`, between the zero at `0x00482000` and the end
`0x00482008` of the pointer list that the C runtime start-up `_cinit`
(`0x0047A8A0`) walks, pushing `0x00482000` and `0x00482008` at `0x0047A8C2`
and `0x0047A8BD`. It is the only entry. It calls `fn_00460CB5`, which copies
the byte at `0x0048786C` (`pref_full_screen`, 1 in the image) to `0x00498354`
(`full_screen_active`). The options reader `fn_0046439A` later writes both
bytes again (`0x004646BD`, `0x004646C5`).

Empty functions. `fn_004255F1`, `fn_00464AD6`, `fn_004653AE`, `fn_004653BE`
and `fn_004653CE` are 16 bytes each: they save `EBP`, `EBX`, `ESI` and `EDI`,
jump over nothing to the epilogue and return, reading and writing nothing.
Their call sites:

| Function | Call sites | Next to |
|---|---|---|
| `fn_004255F1` | `0x00470A20` in the main console `fn_0046FD80`, `0x004726A1` in the network end-of-turn screen `fn_00471F06` | after the stores of 0 to `0x00487814`, `0x0048781C` and `0x00487B94`, before the menu-bar redraw `fn_004255D5` |
| `fn_00464AD6` | `0x00460D97`, `0x00460DBF`, `0x00460DFD`, `0x00460E25` in `WinMain` `fn_00460CCF`; `0x00462A00` in the event reader `fn_00462579` | in `WinMain` right after the depth at `0x0048787C` is set to 16 or 8; in the event reader after the window is invalidated and updated, before `fn_004255D5` |
| `fn_004653AE` | `0x004621E2` in `WinMain` | after `network_join` (`0x00482178`) is set to 1, before the join path `fn_0040DAB9` |
| `fn_004653BE` | `0x00462221` in `WinMain` | after the outer match loop `fn_0046E766` returns on that path |
| `fn_004653CE` | none | none |

No four-byte value in the file equals the address of `fn_004653CE`.

Unused helper. `fn_00465E9B(control, text)` sends `LB_ADDSTRING` (`0x180`)
with the text to the control of the window whose handle is at `0x00493664`,
the window handle field of surface slot 0 (FND-GFX-004). No instruction calls
it and no four-byte value in the file equals its address.

## Interpretation

`full_screen_active` holds the Full Screen default from before `WinMain`
starts until the registry is read. `WinMain` calls the options reader at
`0x00460D14`, before its own read of the byte at `0x004610B3`, so the value
the initializer copies is replaced before `WinMain` uses it.

The five empty functions are hooks whose bodies were removed or compiled out;
their call sites have no effect. The list-box helper is dead code next to the
dialog functions of FND-UI-022.

## Alternatives

- The empty bodies may be debug or logging calls compiled out of the release
  build; which calls they were cannot be read from this build.

## How to reproduce

Read the dwords at `0x00482000..0x0048200B` and the pushes in `_cinit` before
its `_initterm` call. Disassemble each empty function and list its callers.
Search the file for the little-endian addresses of `fn_004653CE` and
`fn_00465E9B`.
