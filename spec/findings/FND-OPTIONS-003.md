---
id: FND-OPTIONS-003
title: The options loader stops when the key does not open, stores one byte of most values, builds the serial number from two draws, and the idle-gang scan reads only the active player's gangs
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046439A..0x00464782
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464783..0x00464AD5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00460EB9..0x00460F49
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046224A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004707CD..0x00470893
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487840..0x00487873
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487884..0x00487885
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498354
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004. The option names are those of
FND-OPTIONS-001.

- Loader. `fn_0046439A`, called once at `0x00460D14`, sets three locals before
  anything else: the key handle to 0, the value type to 0, the 4-byte data
  buffer to 0 and the data size to 4. It then calls `RegOpenKeyExA` with
  `HKEY_LOCAL_MACHINE` and `KEY_READ` (`0x20019`). When that returns anything
  but 0 it jumps to the end of the function at `0x00464779`: no value is
  queried, `RegCloseKey` is not called, and no random draw is made.
- On success it copies each value name into a 20-byte local and calls
  `RegQueryValueExA` with the same type, data and size locals every time. The
  size is never set back to 4 between queries, and no result is tested.
- After each query it stores from the data buffer: the low byte for every
  option except `commType`, whose low 16 bits go to `0x00487884`, and
  `serialNum`, whose four bytes go to `0x00487870`. The byte stores are
  `prefsVidDeep` to `0x00487844`, `prefsSlide` to `0x00487840`,
  `prefsBaseStats` to `0x0048784C`, `prefsCombat` to `0x0048785C`,
  `prefsFreeGang` to `0x00487860`, `prefsVolumeSFX` to `0x00487864`,
  `prefsVolumeCD` to `0x00487868`, `prefsDiff` to `0x00487850`,
  `prefsTimeLimit` to `0x00487854`, `prefsObjective` to `0x00487858` and
  `prefsFullScreen` to `0x0048786C`, which is also copied to `0x00498354`.
- When the dword at `0x00487870` is 0 after the last query, the loader copies
  the name `serialNum` again, calls `fn_0045D227(0x4000)` at `0x00464726` and
  again at `0x00464739`, and stores `((first - 1) << 16) + (second - 1)` at
  `0x00487870` and in the data buffer. It calls `RegSetValueExA` for
  `serialNum` with the type and size left by the last query, and closes the key.
  `fn_0045D227(n)` returns a value from 1 to `n` (RULE-RNG-002).
- Writer. `fn_00464783` has three call sites, all in the title function
  `fn_00460CCF`. At `0x00460EF9` and `0x00460F40` it runs on the failure
  branches of the display-depth check for 8-bit and 16-bit colour: the title
  shows message `0x4E27` through `fn_00465CEC`, sets `0x0048786C` to 1 (and, at
  `0x00460EF2`, `0x00487844` to 1), calls the writer and marks startup as
  failed. At `0x0046224A` it runs on the exit path, before the music fade and
  the volume restore (FND-AUDIO-007).
- Idle scan. In the planning function `fn_0046FD80`, when the done flag
  `0x004ABCA0` is set, the loop at `0x004707DC` visits the 81 gang records of
  the player in `0x004ABC84`, stride `0x20` from `0x00498DA0`, and sets a local
  flag for each record whose sector byte (offset `0xA`) is not 100 and whose
  action byte (offset `0xF`) is 0. It does not stop at the first match. When the
  flag is set, the byte at `0x004ABC9C` is 0 and `prefsFreeGang` is nonzero, it
  calls the warning `fn_00448718` at `0x0047088E` and stores its answer in the
  done flag.

## Interpretation

Without the registry key every option keeps its initialized value and the
serial number stays 0 for the session: the draws are made only when the key
opens. With the key, the shared data buffer starts at 0, so a key with no values
at all gives 0 for every option.

Every option but two is kept as one byte, including `prefsDiff`, so a registry
value above 255 is cut to its low byte. The serial number's high half comes
from the first draw and its low half from the second, each from 0 to 16383. The
new serial number is kept in memory for the session even though writing it back
fails (BUG-OPTIONS-001).

The writer runs when startup gives up because the images for the chosen colour
depth are missing, after it has turned full screen on, and when the player exits
from the title. Neither save reaches the registry (RULE-OPTIONS-002).

The idle-gang warning looks only at the active player's own gangs.

## Alternatives

- A registry value longer than four bytes makes `RegQueryValueExA` report the
  needed size in the size local, and the next query then passes that larger size
  with the 4-byte buffer. What Windows writes in that case, and whether it
  reaches the name local next to the buffer, has not been checked by running
  the original.
- The byte at `0x004ABC9C` suppresses the warning; what it marks is not settled
  here. It is set at `0x004614AF` and at two places in `fn_0046E766` and cleared
  at `0x0046E89F`.

## How to reproduce

In `0x0046439A`, find the constant `0x20019`, the jump to `0x00464779` after
`RegOpenKeyExA`, the word store at `0x00464572` and the two pushes of `0x4000`
before calls to `0x0045D227`. List the callers of `0x00464783` and read the
blocks before `0x00460EF9` and `0x00460F40`. In `0x0046FD80`, find the compare
with `0x51` at `0x004707F8` and the call to `0x00448718`.
