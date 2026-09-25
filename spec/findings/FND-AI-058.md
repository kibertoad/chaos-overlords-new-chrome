---
id: FND-AI-058
title: The family-2 handler tests the hostile pool's own count, reads the owner query, and runs its late Control gates after every branch
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041FEF0..0x0042094B
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 2's handler `0x0041FEF0` has no jump table; it is a chain of tests.
Reading it through confirms the order FND-AI-032 gives and adds these details:

- Both Equip branches jump to `0x00420779`, and the Heal, Move, Attack and
  Control branches reach the same address when they finish. Every path runs
  the two late gates and the scenario-0 Terminate test.
- The owned-sector test before the Move through mode 6 (`0x004202BD`) compares
  the owner query (selector `0x21`) with the active player.
- The attack step requires the cached weight to be positive and selector `0xAB`
  to be positive (`0x004203A6`, `0x004203C5`). Selector `0xAB` is the count of
  the pool that selector `0x92` indexes, the visible gangs of players the
  player is hostile to. Inside the loop each draw takes the human pool
  (selectors `0x28`, `0x29`) when the weight is 10, the hostile pool (`0xAB`,
  `0x92`) when its count is at least 1, and the full pool (`0xAA`, `0x91`)
  otherwise.
- The late gate at `0x00420783..0x004207E4` needs all of: the attitude
  toward the owner query negative, selector `0x28` (the count of visible gangs
  of human players) equal to 0, selector `0x35` nonzero, and a previous action
  other than 4 (Control).
- The late gate at `0x004207FF..0x00420847` needs all of: the attitude toward
  the owner query negative, selector `0xB0` (the count of the owner's visible
  gangs) equal to 0, and a nonzero byte in the player-pair record at
  `0x0048F824`, indexed by the player and the owner query.
- Either gate writes Control and -1 in the first auxiliary value. An Equip's
  cooldown write stays.

## Interpretation

The open questions of RULE-AI-021 are settled: the attack step tests the size
of the hostile pool, and the late gates replace any earlier action, Equip and
Heal included, without undoing a cooldown. Under police presence the owner
query is -2, so a gang in its own sector does not take the mode-6 Move and
goes on to the attack step and the Control or Move choice. The human-owner test
of the second late gate is selector `0x35`, which reads the raw owner byte
(FND-AI-057).

## Alternatives

None known.

## How to reproduce

Open `0x0041FEF0`. Follow the jumps to `0x00420779` from the Equip branches,
the selector `0x21` compare at `0x004202BD`, the two selector `0xAB` calls
before the loop, and the selector calls `0x28`, `0x35`, `0x3E`, `0xB0` and the
byte read of `0x0048F824` in the late gates.
