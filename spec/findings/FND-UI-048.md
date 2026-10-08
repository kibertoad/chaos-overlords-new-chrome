---
id: FND-UI-048
title: The pump draws the selection frame from its counter before it advances the counter, so the frame on screen is the one for the counter less one
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004633D8..0x004635B9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463476
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004635E5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463713
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463726
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. In the event pump `fn_00462579`,
the counter `0x00487804` (FND-UI-017) is read at six places and written at
two:

- Unless the bytes `0x004854C8` or `0x00487890` are set, the pump draws the
  54-by-52 selection frame, the rectangle from `(236 + 54f, 15)` to
  `(290 + 54f, 67)` of the sheet that `fn_00411DF5` also draws (FND-UI-017),
  where `f` is the counter divided by 4. While the city view byte
  `0x00487B88` is set it reads the counter at `0x00463476` and keys the frame
  over the selected sector `0x004ABC80` at `(6 + 53c, 45 + 51r)` (column `c`,
  row `r`). Otherwise, on the detailed sector screen, it reads the counter at
  `0x004635E5` and keys the frame over `(117,111)-(171,163)`, the centre cell
  of the nine-sector display (FND-UI-018).
- At `0x00463713`, after both copies, it increments the counter, and at
  `0x00463719` to `0x00463726` it sets it back to 0 when it reaches 8.

No other instruction of the pump writes the counter.

## Interpretation

Each pass that draws the selection frame draws it for the counter's value at
the start of the pass and leaves the counter one higher. Between passes, a
counter of `n` therefore shows frame `((n + 7) % 8) / 4`: frame 0 for counters
1 to 4 and frame 1 for counters 5 to 7 and 0. On the detailed sector screen
the pump's frame covers the outline the display's own frame draws around the
centre cell. EXP-UI-006's captures, which record the counter when they were
taken, show frame 1 with counters 6 and 7, on the city and on the sector
screen.

## Alternatives

None known.

## How to reproduce

List the references to `0x00487804` inside `0x00462579..0x004637B8`. At
`0x00463476` and `0x004635E5`, follow the arithmetic on the value read to the
constants `0xEC`, `0x0F`, `0x122` and `0x43` pushed before the call of
`0x00425EDF`, and find the test of `0x00487B88` at `0x004633F6` that chooses
between them and the destination `0x6F`, `0x75`, `0xA3`, `0xAB` at
`0x004635AB`. Find the increment at `0x00463713` and the comparison with 8 that
follows it.
