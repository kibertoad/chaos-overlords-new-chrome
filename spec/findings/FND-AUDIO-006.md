---
id: FND-AUDIO-006
title: The effect slots are 48 records of 276 bytes holding a path and a loaded copy, one play channel serves every effect, and the turn-start flags belong to network sessions
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458290..0x004584B9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004584BA..0x00458519
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045851A..0x0045867B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045867C..0x00458857
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458858..0x00458894
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458895..0x004589B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004589B8..0x00458ACB
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464290..0x004642BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042B8EC..0x0042B9D8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449C41
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048735C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048779C..0x004877C3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494C18..0x00494C1F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00494C28..0x00497FE7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00498000..0x0049809F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040BB78
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046849E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004578B4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0042E7A3..0x0042E883
tool: Ghidra 12.1.3
environment: null
---

## Observation

Function extents are those of FND-EXE-004.

- The sound setup `fn_00458290` is called once, at `0x00460F91` in the title
  initialization, with the argument 1. It caps the argument at 8, stores it at
  `0x00494C1C` as the number of play channels and one less at `0x00494C18`,
  which nothing reads.
- The setup calls `fn_0042B8EC` when the install-path length at `0x0048763C` is
  still 0. That helper queries the value `Path` of the App Paths key at
  `0x004876B4` (FND-PLATFORM-005) into `0x00487538`, counts its length into
  `0x0048763C`, stopping at 260, and appends a backslash when the last character
  is not one. When the key cannot be opened it changes nothing and the length
  stays 0.
- The setup builds one template in a local buffer: the two bytes at
  `0x0048779C` (a space and a NUL), then the install path from byte 1, then the
  14 bytes of `data\snd00000` from `0x004877A0`. It copies the template into
  each of 48 records of `0x114` (276) bytes from `0x00494C28`. A record holds a
  loaded byte at offset 0, the template from offset 1, a `GlobalAlloc` handle at
  offset `0x108`, the locked pointer at `0x10C` and the file size at `0x110`.
  The setup zeroes the loaded byte and the three words. The records end at
  `0x00497FE7`.
- The setup zeroes eight channel records of `0x14` bytes from `0x00498000`:
  byte 0, byte 1 and the 16-bit priority at offset `0x10`, and sets the 16-bit
  slot at offset `0x12` to -1. It then scans the auxiliary devices
  (FND-AUDIO-007) and opens the CD device through `fn_00458EA6`.
- The loader `fn_0045867C(slot, number)` does nothing for a slot outside 0 to
  47. Otherwise it first unloads the slot through `fn_00458895`, writes the five
  decimal digits of `number` over the zeros of the record's path, and calls
  `fn_00449C41`, which overwrites byte 0 of the string (the space) with the
  string's length. The file helper `fn_0042B60F` then copies that many bytes
  after the length byte into a name buffer and opens the file with
  `CreateFileA` for reading. When the file does not open, the loader stops and
  the slot stays empty. When it opens, the loader allocates the file's size with
  `GlobalAlloc(0x42)`, locks it, reads the whole file into it, sets the loaded
  byte to 1 and closes the file. A failed allocation leaves the slot empty.
- The unload helper `fn_00458895(slot)` acts on a loaded slot from 0 to 47. For
  each channel from 0 to the channel count whose slot field equals the slot, it
  calls `PlaySoundA(pointer, 0, 0x40)` (`SND_PURGE`). It then unlocks and frees
  the memory and clears the loaded byte, the handle and the pointer.
  `fn_00458858` unloads all 48 slots. `fn_004584BA` clears byte 0 of the eight
  channel records, calls `fn_00458858` and closes the CD device through
  `fn_00458F3E`; the shutdown path calls it at `0x004622B7`.
- The play helper `fn_0045851A(slot, priority)` reads the slot's pointer and
  returns when the slot is outside 0 to 47 or the pointer is null. It scans the
  channels from 0 to the channel count. For a channel whose byte 0 is 0 it keeps
  the channel with the lowest priority, starting from 1000. A channel with byte
  0 set ends the scan and is chosen, and the helper writes 0 to the priority of
  the record at the channel count, one past the scanned ones. The sound plays
  only when its priority is at least the lowest priority found, or when a
  channel with byte 0 set was found. It then calls `PlaySoundA(pointer, 0, 7)`
  when the byte at `0x0048735C` is 0, and stores the priority and the slot in
  the chosen channel.
- No instruction writes byte 0 of a channel record other than to clear it, so
  the branch for a set byte never runs. With one channel, the only record in
  use is channel 0, whose priority starts at 0.
- The byte at `0x0048735C` is 0 in the image and no instruction writes it. It
  is read at `0x00458630`, at `0x00458A99` in `fn_004589B8`, and twice in the
  Send handler `fn_0045EAB1` (`0x0045EB22`, `0x0045FA8C`).
- The wrapper `fn_00464290(slot)` calls `fn_0045851A(slot, 1)` while the
  effects-enabled byte at `0x0048783C` is nonzero. The turn-start call at
  `0x0046F201` also passes priority 1 (FND-AUDIO-003). Those are the only two
  callers of `fn_0045851A`.
- `fn_004589B8(number)` has no callers and no data reference to its entry. It
  builds the same path from the two bytes at `0x004877B0` (a space and a NUL),
  the install path and `data\snd00000` from `0x004877B4`, writes the digits,
  and calls `PlaySoundA(path, 0, 0x20003)` (`SND_FILENAME | SND_NODEFAULT |
  SND_ASYNC`) when `0x0048735C` is 0. It never overwrites the leading space
  with a length.
- `DATA` holds `SND00200` to `SND00204`, `SND00500` to `SND00517`, and
  `Snd00205` to `Snd00208` and `Snd00518`. There is no `SND00499`.
- In Detailed Combat, `fn_0042E040` sets the sound number to -1 when the attack
  was evaded (`0x0042E7A3`..`0x0042E7AC`) and then loads slot 5 with `500 +
  number` at `0x0042E883`, that is `SND00499`. For an unarmed attack it reads
  the 16-bit value at `0x004A289A + definition * 0x9C`, a field of the gang
  definition table loaded from `data\Gangs` at `0x004A2800`.
- The byte at `0x00482178` is set to 1 only at `0x0040BB78` in the Join handler
  `fn_0040B9C0` (the File menu's Join command `0x8107`) and at `0x004621DB` on a
  path that clears it again before a match starts. The byte at `0x00487B58` is
  set to 1 only at `0x0046849E` in the Host handler `fn_004677F0` (`0x8106`) and
  at `0x004578B4` in `fn_00456F80`, the handler the title runs after loading a
  saved network game. The New Game setup `fn_0040E0A0` clears both at
  `0x0040E0B4` and `0x0040E0BB`. Both are single bytes: every access is a byte
  move.

## Interpretation

Each effect file is read whole into memory when it is loaded and played from
there. A missing file leaves its slot empty and costs nothing but silence; the
game gives no message. The leading space of the path template is a placeholder
for the length byte of a counted string, and the install path from the App
Paths key goes in front of `data\`, so without the key the paths are relative
to the current directory.

The priority and channel records have no effect in this build: there is one
channel, every call passes priority 1, and the stored priority never rises
above 1, so every request reaches `PlaySoundA`. The byte at `0x0048735C` reads
as a switch that silences all effects, which the build never sets. The
priority that the helper writes past the scanned channels would land in the
second channel record, which is never used.

An evaded attack in Detailed Combat asks for a file that does not exist, so it
is silent. The Martial Arts value that picks the unarmed sound is the gang
definition's value.

The two flags that allow the turn-start sound mark the two sides of a network
session: `0x00482178` a computer that joined, `0x00487B58` the computer that
hosts, including a resumed network game. Neither is set in a game started with
New Game, so the turn-start sound plays only in network games of the
original's protocol.

## Alternatives

- `fn_004589B8` could be reached through a pointer computed at run time; no
  constant anywhere in the image equals its entry.
- Whether the resumed-network path through `fn_00456F80` is taken for every
  saved network game has not been followed past the load dispatcher.

## How to reproduce

At `0x00460F91` the title initialization pushes 1 before calling `0x00458290`.
In `0x00458290`, find the loop of 48 with the stride `0x114` and the loop of 8
with the stride `0x14`. In `0x0045867C`, find the divisor 10000 and the call to
`0x00449C41`. In `0x0045851A`, find the constant 1000, the test of
`0x0048735C` and the flags 7. List the references to `0x0048735C`,
`0x00482178` and `0x00487B58`.
