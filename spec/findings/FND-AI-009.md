---
id: FND-AI-009
title: The AI hire role comes from a per-scenario schedule indexed by the elapsed turn
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00481018
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0049CA68..0x0049CA6C
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047712A
tool: Ghidra 12.1.3
environment: null
---

## Observation

Before its adjustments, the scenario switch at the end of `0x00458FA0` takes a
schedule slot from the current turn (selector `0x2F`). Nine scenarios use
`turn % 10`; scenario 3 alone uses `turn % 11`. Each cell below is
`ranking mode / hire role`: the mode is the argument passed to `0x004078D9`
and the role is the 32-bit value the branch writes to
`0x00482128 + player * 4`.

| Scenario | 0 | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|---|
| 0 | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 0/1 | 2/2 | 3/3 | - |
| 1 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| 2 | 0/1 | 4/6 | 3/4 | 0/1 | 2/2 | 3/3 | 2/2 | 0/1 | 2/2 | 0/1 | - |
| 3 | 0/1 | 0/1 | 2/5 | 2/2 | 4/6 | 3/3 | 2/2 | 0/1 | 2/5 | 0/1 | 3/4 |
| 4 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| 5 | 0/0 | 1/1 | 0/0 | 0/0 | 4/6 | 0/0 | 3/4 | 0/0 | 3/3 | 2/5 | - |
| 6 | 0/0 | 1/2 | 0/0 | 1/2 | 4/6 | 1/1 | 1/2 | 0/0 | 1/1 | 2/5 | - |
| 7 | 0/1 | 4/6 | 0/1 | 2/2 | 0/1 | 3/4 | 2/2 | 3/4 | 2/2 | 5/3 | - |
| 8 | 0/0 | 1/2 | 0/0 | 1/1 | 1/1 | 2/3 | 1/2 | 0/0 | 1/1 | 1/2 | - |
| 9 | 0/0 | 1/1 | 0/0 | 3/3 | 0/0 | 3/4 | 0/0 | 3/3 | 0/0 | 2/5 | - |

The branches for scenarios 1, 4 and 5 are identical. The constants from
`0x00481018` decode as the floats 52, 4, 2, 100, 3 and 6. The planner divides
the match length in turns by 52 and keeps the result on the x87 stack or in a
local for its quota comparisons. The decompiler shows several later uses as a
multiplication by 0.0; the instructions show that they read the saved factor.

For scenarios 1, 4 and 5 the adjustments are, in order: late turns remap slots
4 and 8; the visible-hostile-sector query (selector `0x9A`, FND-AI-013), the
family-6 coverage query (selector `0x5F`) and the previous hire role (selector
`0x8F`, FND-AI-014) together with the presence of families 5 and 7 redirect
slot 6; the counts of existing families cap slots 8, 6, 9 and 4 at
`factor * 4`, `factor * 4`, `factor * 3` and `factor`; and fewer than four
gangs of family 0 or 4 force slot 0. The other scenarios have their own
adjustment blocks: scenario 7 has a strict cash floor and a last fallback when
family 6 or 12 is missing; scenario 8 redirects four slots only when the count
of family 0 or 4 gangs is strictly above five; scenario 9 applies its
missing-family-2 override last, after any other reset; and scenarios 0, 2 and
3 have their own late-turn windows, fallback chains and quota multipliers, with
slot 10 only in scenario 3. The thresholds of those blocks are not written out
in this finding.

Selector `0x2F` returns the zero-based turn counter at `0x0049CA68`; selector 2
returns the match length minus that counter. The counter starts at 0 and is
incremented once after the outer planner loop, so the first turn uses slot 0
and sees the full match length remaining.

In the scenario 0 branch, schedule slot 9 is reset to slot 0 when fewer than
ten turns remain or cash is below 100, but only for a player whose byte at
`0x004ABC08 + player` is nonzero. The scenario scorer `0x0047712A` stores in
that byte, for each active player, the number of players with a strictly
greater scenario score, so tied leaders both hold 0.

## Interpretation

Each computer player cycles through a fixed ten-turn (Dominance: eleven-turn)
list of hire roles, bent by quotas that scale with the match length in 52-turn
units. The hire role picks both the offer ranking mode and, through the family
table (FND-AI-002), the family that new and existing gangs take. In Greed, a
player that is behind at least one other player stops hiring healers late in
the match or when poor.

## Alternatives

The adjustment blocks other than those of scenarios 1, 4 and 5 were checked by
instruction, but their comparisons are not written out here, so a rule cannot
yet state them. The exact meaning of "late turns" for the slot 4 and 8 remap is
not given.

## How to reproduce

At the end of `0x00458FA0`, find the ten-way switch on selector 0 (scenario).
Each case computes selector `0x2F` modulo 10 or 11, indexes its schedule, calls
`0x004078D9` with the mode, and stores the role to `0x00482128 + player * 4`.
The float constants are loaded from `0x00481018` onward.
