---
id: FND-STATE-010
title: The byte at 0x004ABC9C is set while no match is in play, from startup and again once a match has ended
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004ABC9C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004614AF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E89F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F728
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F9DD
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FF92
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470382
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470872
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470444
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047053A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00470929
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439C66
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439D67
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472198
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472288
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00471E05
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00414E5E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00410291
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. Every access to `0x004ABC9C` is a
byte move. It is 0 in the executable's data.

Writers:

| Address | Function | Value | Context |
|---|---|---|---|
| `0x004614AF` | title `fn_00460CCF` | 1 | Title initialization, just before the intro test, next to a store of 1 to `0x00498350` |
| `0x0046E89F` | match entry `fn_0046E766` | 0 | Match start, after `0x004ABBD4` (`match_over`) is cleared |
| `0x0046F728` | match entry `fn_0046E766` | 1 | After resolution, when `0x004ABBD4` is set: the network bytes `0x00482178` and `0x00487B58` are cleared and `0x004ABC60` is set in the same block (FND-OBJECTIVE-004) |
| `0x0046F9DD` | match entry `fn_0046E766` | 1 | On the way out of the match loop, after a store of 1 to `0x00498350` |

Readers (all skip the named work when the byte is set):

| Address | Function | Work skipped |
|---|---|---|
| `0x0046FF92` | planning `fn_0046FD80` | A number drawn on the console at `(562,15)` for scenarios 0 to 3 |
| `0x00470382` | planning `fn_0046FD80` | Starting the planning clock `fn_0041B8BC` (FND-TIMER-003) |
| `0x00470872` | planning `fn_0046FD80` | The idle-gang warning `fn_00448718` (FND-OPTIONS-003) |
| `0x00470444`, `0x0047053A`, `0x00470929` | planning `fn_0046FD80` | Message `0x81` with style 6 through `fn_00465CEC` and, on its answer, the save function `fn_00463CC5`; the same branches also skip when `0x00498350` is set |
| `0x00439C66`, `0x00439D67` | `fn_004396C0` | The same message and save |
| `0x00472198`, `0x00472288` | `fn_00471F06` | The same message and save |
| `0x00471E05` | console `fn_004718EE` | Playing slot 4 while `0x004ABC60` is set |
| `0x00414E5E` | gang panel `fn_00414D8C` | A whole branch of the handler, entered only when the byte is 0 |
| `0x00410291` | `fn_00410130` | A draw at `(8,5)-(0x45,0x11)` for the active player's own gang |

## Interpretation

The byte marks that no match is in play: it is set from startup until a match
starts, and set again when the end evaluation has finished the match, so the
last planning passes the end sequence gives each surviving local human
(FND-OBJECTIVE-004) run with it set. In those passes the planning clock is not
started, the idle-gang warning is not shown, and the commands that would offer
to save the match before leaving it do not ask. The same rule keeps the save
question away at the title before any match.

## Alternatives

- What the number at `(562,15)`, the branch of the gang panel and the draw in
  `fn_00410130` are has not been read here.

## How to reproduce

List the references to `0x004ABC9C` and read the instructions around each: the
writers store a constant, and each reader tests the byte and jumps over the
block that follows.
