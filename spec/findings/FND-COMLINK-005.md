---
id: FND-COMLINK-005
title: Comlink Send edits a fixed grid of four rows of 40 characters, with a caret that alternates every three ticks of a 6 Hz timer
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045EAB1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004600D2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046023C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327C0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432926
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494810..0x00494814
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328BE
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004328F8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460CCF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327DC
tool: Ghidra 12.1.3
environment: null
---

## Observation

- In the Send handler `fn_0045EAB1`, Enter (`0x0D`) moves the text cursor to
  column 0 of the next row, Backspace (`0x08`) deletes at the cursor, and
  Left, Up, Right and Down move the cursor within four rows and 40 columns,
  held at the edges.
- A printable character is turned to upper case and then accepted only when
  it lies in `0x20..0x5B`.
- The cell writer `fn_004600D2` draws each 6-by-7 character at
  `(199 + 6 * column, 256 + 8 * row)`.
- The caret helper `fn_0046023C` draws the cell under the cursor again from
  the plain character row of `PX00129` at y 0, or from the inverse row at
  y 441, with the same opaque copy it uses for the panel art.
- The window-message callback `fn_004327C0` and the signal helper
  `fn_00432926` each set one of four timer flags at `0x00494810`. The helpers
  `fn_004328BE` and `fn_004328F8` test and clear a flag, so each consumer sees
  a raised flag once.
- The Send loop starts by raising timer flag 0. It counts only the timer-0
  events it consumes, and every third one switches the caret between the plain
  and the inverse row. The caret starts plain.
- The start-up routine `fn_00460CCF` registers timer 0 through `fn_004327DC`
  with a frequency of 6. That helper calls `timeSetEvent(1000 / frequency, 20,
  ...)`, so the period is `1000 / 6` = 166 milliseconds in integer division,
  and each caret phase lasts three events, 498 milliseconds.

## Interpretation

The message is a fixed four-by-40 grid that the player types over, not a text
field that grows. Enter moves to the next row and never sends.

## Alternatives

- Whether Backspace moves the cursor back before deleting, and what it
  deletes at column 0, is not recorded.
- Whether typing a character moves the cursor on, and where it goes after
  column 39, is not recorded.

## How to reproduce

In `fn_0045EAB1`, read the virtual-key switch and the character branch with
its upper-case step and range test. Follow the calls to `fn_004600D2` and
`fn_0046023C` for the cell arithmetic and the `PX00129` source rows. For the
timer, open `fn_00460CCF` and follow the call to `fn_004327DC`, which imports
`timeSetEvent`.
