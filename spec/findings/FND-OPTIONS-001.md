---
id: FND-OPTIONS-001
title: Options are read from thirteen HKLM registry values into one shared buffer, and the writer opens the key read-only
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464783
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487840..0x00487874
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487884..0x00487888
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The loader `fn_0046439A` opens
  `HKLM\SOFTWARE\Stick Man Games\Chaos Overlords\1.0` once with access mask
  `0x20019` (`KEY_READ`) and queries thirteen four-byte values in this order,
  each into the global shown, whose initialized value is given:

  | Order | Registry value | Global | Initialized value |
  |---|---|---|---|
  | 1 | `prefsVidDeep` | `0x00487844` | 1 |
  | 2 | `prefsSlide` | `0x00487840` | 1 |
  | 3 | `prefsBaseStats` | `0x0048784C` | 0 |
  | 4 | `prefsCombat` | `0x0048785C` | 1 |
  | 5 | `prefsFreeGang` | `0x00487860` | 1 |
  | 6 | `commType` | `0x00487884` | 0 |
  | 7 | `prefsVolumeSFX` | `0x00487864` | 6 |
  | 8 | `prefsVolumeCD` | `0x00487868` | 5 |
  | 9 | `prefsDiff` | `0x00487850` | 1 |
  | 10 | `prefsTimeLimit` | `0x00487854` | 0 |
  | 11 | `prefsObjective` | `0x00487858` | 0 |
  | 12 | `prefsFullScreen` | `0x0048786C` | 1 |
  | 13 | `serialNum` | `0x00487870` | 0 |

- The loader ignores every query result and uses one DWORD buffer for all
  thirteen queries without resetting it between them.
- When the serial number the loader ends with is 0, it makes two bounded random
  draws, at `0x00464726` and `0x00464739`, and tries to write the value they
  make back to `serialNum` through the same read-only handle (FND-RNG-001).
- The writer `fn_00464783` writes all twelve preference values as `REG_DWORD`,
  but opens the same key with the same `KEY_READ` mask. It never asks for
  `KEY_SET_VALUE` and ignores every `RegSetValueExA` result. Its three callers
  are at `0x00460EF9`, `0x00460F40` and `0x0046224A`.
- The Slide Panels byte is read directly by the panel helpers `fn_0041953E` and
  `fn_004196F5`. The Base Statistics byte is read by the paths that draw gang
  statistics. The Detailed Combat byte is read on the Done path at
  `0x0046FD80`.

## Interpretation

Whatever the player changes in the Options menu lasts only until the game
exits: `RegSetValueExA` fails on a handle opened for reading, and the game does
not notice. Values an installer or another program put in the key are read at
start. When a value is missing or cannot be read, the query fails without
touching the buffer, so the option takes the value read for the option before
it, not its initialized value. The initialized values are Thousands of Colors
on, Slide Panels on, Base Statistics off (current statistics shown), Detailed
Combat on, Warn if Idle Gangs on, effects level 6, music level 5, Mentality 1,
no planning time limit, objective 0 and full screen on.

## Alternatives

- Whether a query into a four-byte buffer of a value stored with another type
  or size changes the buffer has not been checked against the Windows
  documentation.
- The query for the first value, `prefsVidDeep`, has no earlier value to
  inherit; what the buffer holds before it has not been read.

## How to reproduce

Find the string `Stick Man Games` and follow its reference into `0x0046439A`
and `0x00464783`. Each passes `0x20019` to `RegOpenKeyExA`. The thirteen value
names are string constants pushed in order before `RegQueryValueExA`.
