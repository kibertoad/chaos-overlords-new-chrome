---
id: FND-AWARDS-005
title: The endgame renderer draws each listed player's name with fn_00413FD5 from three calls, so the name pointer names the player of each row in drawing order
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042CE61..0x0042E02E
tool: Ghidra 12.1.3
environment: null
---

## Observation

`fn_0042CE61`, the endgame renderer of
FND-AWARDS-001 and FND-AWARDS-003, takes three arguments. When its first
argument is not 0 and its second is 0 (`0x0042CFEF`, `0x0042CFFC`), it draws
the victory splash of the player in its third argument and that player's name
with `fn_00413FD5` at `0x0042D1A4`. Otherwise it draws the table:

- The ranked rows (`0x0042D263..0x0042D9F1`) run a standing from 0 to 5 in
  `[EBP-0x8]` and, inside it, a player slot from 0 to 5 in `[EBP-0xC]`, and
  draw a row when the byte of `scenario_standing` at `0x004ABC08` for the slot
  equals the standing (`0x0042D289..0x0042D293`). The row's name is drawn by
  the call at `0x0042D2DA`.
- The rows of eliminated players (`0x0042DA00..0x0042E02E`) run a player
  slot from 0 to 5 and draw a row when its standing byte is -1, 0xFF read as
  a signed byte; the name is drawn by the call at `0x0042DA64`.

Each of the three calls pushes, from the first argument, 1, a position and
the player's name, the text at `0x004A2589 + 12 * player` (FND-UI-003). The
row counter that places each row goes up by one after every row drawn. The
only other call of `fn_00413FD5` in the function, at `0x0042D526`, draws a
fixed string at `0x00487704`.

## Interpretation

A debugger that stops at `fn_00413FD5` with the return address after one of
the calls at `0x0042D1A4`, `0x0042D2DA` or `0x0042DA64` reads the player of
the row from the third stack argument, `(pointer - 0x004A2589) / 12`, so a run
can record the order in which the endgame lists the players, and whether it
showed the splash.

## Alternatives

None known.

## How to reproduce

Open `fn_0042CE61`. Follow the branch on the first two arguments, the nested
loops over `[EBP-0x8]` and `[EBP-0xC]` that compare the byte at
`0x004ABC08 + slot`, the loop that compares it with -1, and the three calls of
`fn_00413FD5` whose third argument is `0x004A2589` plus twelve times a slot.
