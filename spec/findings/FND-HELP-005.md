---
id: FND-HELP-005
title: The only call that opens the help file has no callers, so Help Topics does nothing in this build
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046508C..0x004650B0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487AF4..0x00487B04
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462579..0x004637B7
tool: Ghidra 12.1.3
environment: null
---

## Observation

- `fn_0046508C` calls `WinHelpA` with the main window, the path
  `.\Help\Chaos.hlp` at `0x00487AF4`, command `0xB` (`HELP_FINDER`) and data 0,
  and returns. The call at `0x004650A1` is the only reference to the
  `WinHelpA` import slot `0x004AE840`, and the path string's only reference is
  the push at `0x00465096`.
- `fn_0046508C` has no callers. The function before it ends with a return at
  `0x0046508B`, so it is not reached by falling through, and its entry address
  occurs nowhere in the file as a 32-bit value.
- The Help Topics item of menu 101 sends the command `0x8001`, which the
  window procedure turns into event 1 with `(0x80, 1)` (FND-UI-020). The event
  step `fn_00462579` handles `(0x80, 3)`, About, and has no branch for
  `(0x80, 1)`. None of the screen loops that call it compares the event with
  `(0x80, 1)`, and the value `0x8001` does not occur as an operand anywhere in
  the code.
- Accelerator table 102 has no entry for F1 or any other key that sends
  `0x8001` (FND-UI-021).

## Interpretation

Choosing Help Topics, or pressing F1, has no effect: the Windows help viewer is
never started and the game never reads `Chaos.hlp`. The function that would
open the viewer's contents page was written and left unconnected. This
answers the question FND-HELP-003 left open about the callers of `WinHelpA`.

## Alternatives

An indirect call through a computed address could reach `fn_0046508C`; no
table in the file holds its address, so none is known. The absence of any
effect has not been observed in a run.

## How to reproduce

List the references to `0x004AE840` and to `0x0046508C`. Search the file for
the bytes `8C 50 46 00`. List the comparisons with `0x80` in `fn_00462579` and
its callers.
