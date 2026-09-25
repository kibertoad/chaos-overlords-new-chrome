---
id: FND-COMLINK-008
title: The Send panel builds a message in a 166-byte buffer, with 160 characters from space to Z filled with spaces and no terminator, a signed turn, and a last byte nothing writes
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045F10B..0x0045F14F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045F3B7..0x0045F46D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004600D2..0x0046023B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045D2F0..0x0045D619
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045E485
tool: Ghidra 12.1.3
environment: null
---

## Observation

The message being written is kept in the 166-byte buffer at `0x00498120`.
FND-EXE-004 gives the ranges of the functions named here.

- When the Send panel `fn_0045EAB1` opens, it stores 1 at byte 0
  (`0x0045F10B`), 0 at byte 1 (`0x0045F112`), the low 16 bits of the elapsed
  turn counter `0x0049CA68` at bytes 2 and 3 (`0x0045F11E`) and the active
  player slot at byte 4 (`0x0045F129`). It then copies 40 bytes from
  `0x004877D4` into each of the four rows at `0x00498125 + row * 0x28` with the
  byte copy `fn_00449BFC` (`0x0045F14F`); the 40 bytes at `0x004877D4` are all
  `0x20`.
- A typed key goes through `0x0045F3B7..0x0045F3F6`: a code from `0x61` to
  `0x7A` is lowered by `0x20`, and only a code from `0x20` to `0x5A` is stored.
  Backspace stores a space (`0x0045F46D`). The column wraps at 40 and the row
  is held to 0..3.
- `fn_004600D2` stores the character at `0x00498125 + column + row * 0x28`
  and draws glyph number `character - 0x20` from a strip of 6-pixel glyphs.
- The reference search finds no instruction that addresses byte `0xA5`
  (`0x004981C5`) of the buffer, and the text bytes are written only by the
  two routines above.
- The recorder `fn_0045D2F0` copies the whole buffer (41 DWORDs and one WORD)
  into the next free message slot of each recipient at
  `0x0049CA90 + player * 0xA60 + index * 0xA6`. On the network paths it sends
  and receives the 166 bytes as they are, converting only the 16-bit turn
  field between host and network byte order (`fn_00449D3B`, `fn_00449D87`).
- The View panel `fn_0045E04D` loads the turn with `MOVSX` (`0x0045E485`) and
  divides it by 52 (`IDIV`).

## Interpretation

The message text is 160 single-byte characters in four rows of 40, each one
of the 59 codes from space to `Z`; lower-case letters become capitals, unused
characters are spaces, and there is no terminator. The turn is a signed 16-bit
field. Byte `0xA5` is a spare byte: the game never writes it on its own, so
it carries the buffer's initial value, 0 in the executable image, and a
received message carries whatever the sender's buffer held there.

## Alternatives

- A write to byte `0xA5` through a pointer that the reference search does not
  resolve cannot be ruled out; no such pointer into the buffer other than the
  whole-record copies was found.

## How to reproduce

List the references to `0x00498120..0x004981C5`. Read
`0x0045F10B..0x0045F14F`, the key handling at `0x0045F3B7..0x0045F46D`,
`0x004600D2`, and the copies in `0x0045D2F0`. Dump 48 bytes at `0x004877D4`.
