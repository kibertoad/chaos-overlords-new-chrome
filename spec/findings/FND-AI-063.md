---
id: FND-AI-063
title: The unset Support threshold of families 13 and 14 always holds the 0 the selector's prologue leaves at the same stack address
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0..0x00432DD7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004335AD..0x004335BC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00433811..0x0043385F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00433A83..0x00433BC3
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70..0x00402D95
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00405A5E..0x00405A94
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004062DF..0x004062EC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004078AB..0x004078B7
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040ABC0..0x0040ABC8
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00466910..0x00466918
tool: Python 3.14.7 script disassembling the file bytes with Capstone 5.0.7
environment: null
---

## Observation

`D` is the frame pointer of the per-gang dispatcher `fn_00432DA0`. Its
prologue reserves `0x40` bytes and pushes three registers, so between calls its
stack pointer is `D - 0x4C`, and every call it makes is followed by an `add esp`
that restores that value.

- The family-13 handler `fn_0040ABC0` and the family-14 handler `fn_00466910`
  are called only from the dispatcher, at `0x00433BA9` and `0x00433BBE`, with
  two arguments. Each starts with `push ebp`, `mov ebp, esp` and
  `sub esp, 0x1C`, so its frame pointer is `D - 0x5C` and its local -0x14, the
  Support threshold of FND-AI-062, is at `D - 0x70`.
- The selector `fn_00402D70` takes four arguments. Called from the dispatcher,
  its frame pointer is `D - 0x64`, and its local -0xC is at `D - 0x70`. Its
  prologue writes 0 to locals -4, -8, -0xC and -0x10 (`0x00402D79..0x00402D8E`)
  before the case switch.
- Case `0x48` (`0x00405A5E..0x00405A94`) and case `0x7C`
  (`0x004062DF..0x004062EC`) write only local -0x14, and the common exit at
  `0x004078AB` pops registers and returns. Neither writes local -0xC.
- The dispatcher reaches the handler call through the family switch at
  `0x00433BD0`, entered from `0x00433A83`. There are two ways there:
  - Selector `0x48` returns other than 1 and the branch at `0x00432DD7` jumps
    straight to `0x00433A83`. The last call is that selector call.
  - Selector `0x48` returns 1. The dispatcher calls `fn_00409DE1` and selector
    0, then runs the case for the scenario, which in scenario 6 calls selector
    `0x7C` at `0x004335B7` and in scenario 8 calls selectors `0x2F` and 0 and
    then, on every path, selector `0x7C` at `0x0043385A`. It sets the family
    byte and jumps to `0x00433A83`. No call follows the selector `0x7C` call.
- Between that last selector call and the handler call the dispatcher only
  reads memory, compares and jumps.

## Interpretation

On every path to a family-13 or family-14 handler, the last function that ran
at the handler's stack depth is the selector, in case `0x48` or `0x7C`, and it
left 0 at the address the handler later reads as its Support threshold.
FND-AI-062's site scan therefore compares each slot's Support with 0: it
chooses the first slot with the highest positive Support among the slots with
remaining Resistance, and chooses none when no such slot has positive Support.

The threshold remains uninitialised in the handler's code (BUG-AI-006); the
call sequence of this build is what makes its value fixed.

## Alternatives

- The reading assumes nothing writes the thread's stack below its stack pointer
  between two instructions of the dispatcher. A debugger stepping through, or
  an exception handler, could; neither happens in play.

## How to reproduce

In `0x00432DA0`, read the prologue, every `add esp` after a call, the branch at
`0x00432DD7`, the scenario cases at `0x004335AD` and `0x00433811`, and the path
from `0x00433A83` through the switch at `0x00433BD0` to the calls at
`0x00433BA9` and `0x00433BBE`. Read the prologues of `0x0040ABC0`,
`0x00466910` and `0x00402D70`, the selector's case table at `0x004075E3` for
entries `0x48` and `0x7C`, and every write to `[EBP-0xC]` in `0x00402D70`.
