---
id: FND-TIMER-003
title: The planning limit is a table of four values applied at every match entry, the expiry test skips an unlimited turn, and the bar is redrawn every sixth presentation tick
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046ECA2..0x0046ED1E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B8BC..0x0041B8FB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041B8FC..0x0041BCBB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041BCBC..0x0041BCD7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041BDD5..0x0041BE19
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00462EE2..0x00462F3B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463BD1
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00463BFB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047038F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470898
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004708E2
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487898..0x0048789B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00490698..0x004906A3
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. `timer_ms` below is the value of
`timeGetTime`.

- Mapping. The match entry `fn_0046E766` reads the byte at `0x00487854` with
  sign extension at `0x0046ECA2` and, when it is 0 to 3 as an unsigned value,
  jumps through a four-entry table at `0x0046ED0F` that stores -1, 30000, 120000
  or 300000 in the dword at `0x0049069C`. Any other value skips the store and
  leaves the previous limit. The code runs on every call of `fn_0046E766`,
  whatever its argument, before the pointer is set at `0x0046ED23`.
- The byte at `0x00487854` is written by the preferences loader (`0x0046464F`),
  by the setup panel (`0x0043954A`), by the title's load path (`0x00461C00`) and
  by the save loader `fn_0046381A` at `0x00463BD1` and `0x00463BFB`.
- Start. `fn_0041B8BC`, called once at `0x0047038F` in the planning function
  `fn_0046FD80`, stores `timer_ms` at `0x004906A0`, sets the byte at `0x00490698`
  to 1 when the limit is not -1 and to 0 when it is, and calls the draw function
  at once.
- Expiry. `fn_0041BDD5`, called once at `0x00470898` in `fn_0046FD80`, returns 0
  when `0x00490698` is 0. Otherwise it computes `timer_ms - start` as a 32-bit
  value and returns 1 when that is greater than the limit in a signed compare.
  In `fn_0046FD80` the call comes after the idle-gang warning's test.
- Stop. `fn_0041BCBC`, called once at `0x004708E2` in `fn_0046FD80` after the
  planning loop ends, writes 0 to `0x00490698` and calls the draw function.
- Draw. `fn_0041B8FC` does nothing when `0x00490698` is 0. Otherwise, with the
  limit not -1, it takes `elapsed = timer_ms - start`, multiplies it by 100,
  divides by the limit with a signed divide, multiplies the quotient by 60 and
  divides by 100, and subtracts the result from 60. That is the bar width
  (`0x3C` when the limit is -1, a path the flag test makes unreachable). A width
  below 1 copies the empty bar, rows 3 to 6 of buffer 6 at x 354 to 414, to the
  bar's place at `(520,336)-(580,339)` of buffer 1. A width above 59 copies the
  full bar, rows 0 to 3 of buffer 6 at the same x. Any other width copies
  `width` columns of the full bar and the remaining columns of the empty bar.
  Then it copies the bar area from buffer 1 to the screen.
- After drawing it computes `remaining = limit - (elapsed * 100) / 100` with a
  signed divide. When `1000 < remaining < 10000` it calls `fn_00464290(7)`;
  when `0 < remaining <= 1000` it calls `fn_00464290(8)`. `fn_00464290` plays
  only while sound effects are on (FND-AUDIO-006).
- Redraw. In the event pump `fn_00462579`, at `0x00462EE2`, while the byte at
  `0x00487830` is set and timer slot 0 has ticked (FND-UI-023), the dword at
  `0x00487898` is decremented when above 0 and set to 0 otherwise; when it is 0
  the pump calls the draw function and sets it to 6. The dword is 6 in the image
  and has no other writer.

## Interpretation

The planning limit is a table lookup on the stored choice: none, 30 seconds, 2
minutes, 5 minutes. It is taken again at each entry into a match, new or loaded,
so a loaded game uses the choice its save carries. A choice byte above 3 keeps
whatever limit was set last, and the limit starts at 0 in the image, so such a
choice in a fresh session gives a limit of 0 and every planning turn ends on the
first test.

An unlimited turn clears the active flag, so the expiry test returns 0 and the
bar is not drawn at all; the -1 never reaches the compare. The compare is signed
on a 32-bit difference, so `timeGetTime` wrapping during a turn does not break
it: the difference stays correct across the wrap.

The pump redraws the bar on every sixth tick of the 6 Hz presentation timer, so
about once a second, while the main console is up. The countdown is not reset
when planning starts, so the first redraw after the one at the start falls
anywhere from one to six ticks later. The warning sounds follow the redraws, so
there are about nine short ticks in the last ten seconds and one longer sound in
the last second, each started by one redraw. The start helper draws once at the
start of each timed human turn.

The expiry is tested only in the planning loop itself. A modal panel opened from
the console runs its own loop, which calls the pump and so keeps drawing the bar
and playing the sounds, but the turn ends only after the panel closes and the
planning loop runs its next pass. The stop helper hides nothing: it clears the
flag and the draw it calls then does nothing, so the last drawn bar stays on the
screen until something draws over it.

## Alternatives

- Whether the setup screen or the registry can produce a choice above 3 is
  outside this reading; the setup panel writes values from its own controls.

## How to reproduce

In `0x0046E766`, find the byte read of `0x00487854` at `0x0046ECA2`, the
compare with 3 and the table at `0x0046ED0F`. List the references to
`0x00490698`, `0x0049069C`, `0x004906A0` and `0x00487898`. In `0x0041B8FC`, find
the constants `0x3C`, `0x64`, `0x3E8` and `0x2710` and the calls to
`0x00464290` with 7 and 8.
