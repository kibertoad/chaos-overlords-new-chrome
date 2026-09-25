---
id: FND-UI-001
title: Detailed Combat advances one frame per tick of a 6 Hz multimedia timer and draws the frames in two 64-by-64 apertures
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E040
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00430C23
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

- `fn_0042E040` references the four combat strip bases: 7000 at `0x0042E812`,
  7100 at `0x0042E86D`, 7300 at `0x0042EB41` and 7200 at `0x0042EB9F`. It then
  enters the presentation loop `fn_00430C23`.
- The loop reads and clears timer slot 0 and advances a phase counter. Phases 3
  to 10 draw the eight 64-pixel frames of a strip in turn. Phases 13 and 15 draw
  the lost part of the two Force bars in white, phases 14 and 16 restore the bar
  areas, phases 17 to 21 keep the finished result on screen, and phase 22
  leaves the loop.
- During startup `fn_00460CCF` calls `fn_004327DC(0, 6)`. The timer helper sets
  up `timeSetEvent` with a period of `1000 / rate` milliseconds in integer
  arithmetic, so slot 0 fires every 166 ms.
- For phases 3 to 10, `fn_00430C23` copies each 64-by-64 source frame to the
  backing-buffer rectangles with half-open corners `(150,274)-(214,338)` and
  `(223,274)-(287,338)`. The panel occupies backing-buffer rows 144 to 353 and is
  copied to the screen rectangle with corners `(104,124)-(448,333)`, so the
  frames land at screen `(254,254,64,64)` and `(327,254,64,64)`. These sit
  inside the wider 67-by-64 action cells of the panel art and leave its green
  dividers alone.
- `fn_0042E040` copies the sector tile into backing-buffer corners
  `(31,155)-(85,207)` and draws the sector code at `(52,210)`: panel-local
  `(31,11,54,52)` and `(52,66)`.

## Interpretation

Combat animation runs at 6 frames per second: each frame stays 166 ms, and an
eight-frame attack clip lasts about 1.33 seconds. After the clip, the Force
each gang lost flashes white twice, and the result then stays for five more
ticks before the loop ends.

## Alternatives

- How the timer callback marks slot 0 (a flag or a counter) has not been read.
- The white flashes are read from the phase numbers; the colour of the missing
  Force after the flashes has not been checked against the palette.

## How to reproduce

Find the constants 7000, 7100, 7200 and 7300 in `0x0042E040` and follow its call
to `0x00430C23`, whose switch on the phase counter has cases 3 to 22. In
`0x00460CCF`, find the call to `0x004327DC` with the arguments 0 and 6;
`0x004327DC` divides 1000 by its second argument before calling
`timeSetEvent`.
