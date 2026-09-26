---
id: FND-RNG-004
title: The computer players' 46 bounded draws come from their dispatcher, two shared helpers and twelve family handlers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00432DA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408642
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00408214
tool: Ghidra 12.1.3
environment: null
---

## Observation

Of the 61 direct calls to the bounded wrapper `fn_0045D227` (FND-RNG-003), 46
are made by the computer players. They sit in the per-gang command dispatcher
`fn_00432DA0`, in the shared sector and placement helpers `fn_00408642` and
`fn_00408214`, and in twelve family-handler functions. Each of those calls has
been traced to the branch that makes it: action choice, target pools, retry
loops and the persistent planning state they write. None of them is made while
a turn is being resolved; they all run while a computer player plans.

## Interpretation

The computer players draw from the same generator as the rest of the game, so
their planning moves the state that the following resolution draws from. Their
draws are separate from the whole-turn resolver's dice and tie-break draws only
in when they are made: during `planning_phase`, in the slot order in which the
computer players plan.

## Alternatives

None known for the ownership of the calls. The order of the draws inside each
handler belongs to the computer-player rules, and has not been checked against
a trace of the original.

## How to reproduce

List the references to `fn_0045D227`. The calls whose containing function is
`fn_00432DA0`, `fn_00408642` or `fn_00408214`, together with those in the
twelve family-handler functions the computer-player findings describe, make
the 46 of this group.
