---
id: FND-RNG-002
title: The raw step is the statically linked runtime rand, a 32-bit linear congruential generator returning bits 16 to 30
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00478CD0
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ghidra's function signature database identifies `fn_00478CD0` as the
statically linked Visual Studio 1998 C runtime `rand`. It fetches the calling
thread's runtime data block, multiplies the 32-bit random state there by
`0x343FD`, adds `0x269EC3`, stores the result back, and returns bits 16 to 30
of the new state (`(state >> 16) & 0x7FFF`).

The only direct caller in the game's code is the bounded wrapper `fn_0045D227`.

## Interpretation

This is the generator's raw step. Every raw value the game uses is one of
these 15-bit results, from 0 to 32767, and every one of them passes through
`fn_0045D227`.

## Alternatives

The identification rests on the signature match and on the constants, which
are those of the Microsoft runtime's `rand`. No runtime trace has confirmed the
output.

## How to reproduce

Go to `0x00478CD0` in Ghidra, or search the code for the constant `0x343FD`.
List its references: the only caller is `fn_0045D227`.
