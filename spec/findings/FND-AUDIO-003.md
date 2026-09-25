---
id: FND-AUDIO-003
title: The turn-start sound plays at every turn start after the first, and every effect interrupts the one playing
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00464290
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0045851A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482178..0x00482179
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B58..0x00487B59
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The wrapper `fn_00464290` calls the lower play helper `fn_0045851A` with a
  slot and a priority.
- The outer turn function `fn_0046E766` calls `fn_0045851A` directly at
  `0x0046F201`, with slot 9 and priority 1, when the flag at `0x00482178` or
  the flag at `0x00487B58` is nonzero. The call comes after the per-sector
  financial work at the start of a turn and before player planning.
- The enclosing loop sets a local to 1 before its first pass and reaches the
  call only when that local is 0, so the first pass of the loop does not play
  the sound and later passes can.
- The lower helper checks the slot and that the slot's memory is loaded, keeps
  a record of its channel, and, when sound output is available, calls
  `PlaySoundA(pointer, 0, 7)`. The flags 7 are `SND_ASYNC | SND_MEMORY |
  SND_NODEFAULT`. They leave out `SND_NOSTOP`.
- The lower helper does not read the effects-enabled byte at `0x0048783C` that
  the wrapper tests.
- Music plays through MCI (FND-AUDIO-001), not through `PlaySoundA`.

## Interpretation

`SND00208`, loaded into slot 9, is a turn-start sound. It plays at the start of
each turn after the first one a game session plays, in a local game or a
network game of the original's own protocol, and it plays even while sound
effects are turned off. Without `SND_NOSTOP`, Windows stops the sound that is
playing when a new `PlaySoundA` call arrives, so general effects, the combat
sounds and the turn-start sound cut each other off. Music is not affected.

## Alternatives

- The flag at `0x00482178` is read as "local game" and the one at `0x00487B58`
  as "network game of the original's protocol". Neither reading has been
  checked against all their writers.
- What the priority and channel record change has not been read, so it is not
  shown that every call reaches `PlaySoundA`.
- Whether the first pass of the loop is the first turn of the match or only the
  first turn after the function was entered (for example after loading a save)
  depends on where the loop starts; that has not been recorded.

## How to reproduce

In the outer turn function at `0x0046E766`, find the call at `0x0046F201`, which
pushes 9 and 1 and calls `0x0045851A`, and the tests of `0x00482178` and
`0x00487B58` that guard it. In `0x0045851A`, find the `PlaySoundA` import call
and its constant flags 7.
