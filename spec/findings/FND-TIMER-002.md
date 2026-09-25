---
id: FND-TIMER-002
title: Four multimedia timer slots set flags that the event step polls; waits are counted in ticks of the six-per-second slot, and the floating-point helpers are reachable only from dead code
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004327C0..0x00432925
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432954..0x004329B0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464CD9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464B43
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045CDE0..0x0045D226
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00418F16
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494800..0x00494817
tool: Ghidra 12.1.3
environment: null
---

## Observation

Timer slots:

- `fn_004327DC(slot, rate)` accepts slots 0 to 3. It kills a timer already in
  the slot and starts `timeSetEvent` with a period of `1000 / rate` ms when
  `rate` is positive and `-rate` ms otherwise, a resolution of 20 ms and
  `TIME_PERIODIC`, passing the slot as the user value. The timer IDs are kept at
  `0x00494800` (four 32-bit entries).
- The callback `fn_004327C0` sets the byte `0x00494810 + slot` to 1 and does
  nothing else.
- `fn_00432847(slot)` kills the slot's timer, `fn_004328BE(slot)` returns its
  byte and `fn_004328F8(slot)` clears it.
- `WinMain` starts slot 0 at rate 6 (a period of 166 ms) and slot 1 at rate 10
  (100 ms) and kills both on the way out (FND-PLATFORM-009). Slot 3 is started
  at rate 46 only by the wipe `fn_00464B43`, which draws 46 strips of 10 rows
  with a green line; `fn_00464B43` has no callers and its address occurs nowhere
  in the file. Nothing starts slot 2.
- `fn_0043287C` stores `timeGetTime` at `0x00494814` and `fn_00432897` returns
  the milliseconds since then. The elimination panel `fn_0042C3F5` uses them to
  close itself after 15000 ms in a network game, and the network wait
  `fn_0040D9E2` to wait 2000 ms.

Waits:

- `fn_00464CD9(n)` clears slot 0's byte and calls the event step `fn_00462579`
  until it has seen the byte set `n` times. Its 13 call sites, in 7 functions, all pass 1, so each
  waits until the next tick of slot 0: up to 166 ms, and less when the tick is
  near.
- The event step itself waits at most `1 * 17` ms for a message
  (`fn_0045C180`, FND-UI-020), and `timeBeginPeriod(17)` is in force for the whole
  session (FND-PLATFORM-009).
- `fn_00432954(src, dst, rect)` copies the rectangle from `src` to `dst` in a
  loop until `timeGetTime` has advanced more than 1000 ms and returns the number
  of copies. `WinMain` stores it at `0x004981F8`.
- `GetTickCount` is called only from the modem code (`fn_0041BE20`, which waits
  5 seconds, and `fn_0041CFCA`).

`fn_00418F16` does not wait. At planning entry it loads `PX00128` into surface
1, `PX03000` into surface 3 at 640 by 576, `PX02000` into surface 5 at 120 by
1408 and `PX04999` into surface 5 at x 120, 20 by 1280.

Floating-point helpers:

- `fn_0045CF05` computes a point on a circle from a centre, a radius and an
  angle in degrees, and `fn_0045CF99` the angle in degrees, 0 to 359, from one
  point to another, using the table at `0x00483800`. Neither has callers, and
  neither address occurs in the file as a 32-bit value.
- The helpers they call, `fn_0045CDE0` (distance), `fn_0045CE26` (square root
  through the runtime `fn_00478CA0`), `fn_0045CE61` and `fn_0045CEBE` (an angle
  reduced to 0 to 359; the second returns an uninitialized local when its input
  is 360 or less) and `fn_0045D17E`, have no callers outside those two.

## Interpretation

The game's pacing comes from two periodic flags polled by the event step
(RULE-UI-008): nothing runs inside the timer callbacks. A delay of one tick
lasts between 0 and 166 ms depending on where the tick falls, so the same wait
gives different lengths from one call to the next. The benchmark count grows
with the speed of the machine and the display. No game rule uses floating-point
angles or distances, so the rounding of those helpers never reaches play. The
plan's guess that `fn_00418F16` is a delay came from its constants, which are
image numbers.

## Alternatives

The actual interval of a 20 ms-resolution multimedia timer at 166 ms depends on
the system and has not been measured. What the game does with the benchmark
count, a quarter of it at least 1 as the panel slide step, is described with
the panel helpers `fn_0041953E` and `fn_004196F5` (FND-UI-011, RULE-UI-003).

## How to reproduce

The `timeSetEvent` call is at `0x0043282D` and the callback's store at
`0x004327C9`. `WinMain` starts the timers at `0x0046118F` and `0x0046119B`. List the callers of
`0x00464CD9` and read the value each pushes. Search the file for the
little-endian bytes of `0x00464B43`, `0x0045CF05` and `0x0045CF99`.
