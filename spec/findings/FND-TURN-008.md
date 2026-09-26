---
id: FND-TURN-008
title: The resolver's blocks run in address order, from the prologue snapshots to the end evaluator
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775..0x00475F6F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476F3B..0x00477129
tool: Ghidra 12.1.3
environment: null
---

## Observation

Addresses are in the whole-turn resolver `fn_00472775` unless another function
is named. Each block below is a counted loop over players, roster slots or
sectors. The loop exits quoted below jump to the first instruction of the next
block.

Prologue:

- `0x0047277D` calls `fn_004787B0`, the runtime's stack probe.
- When the AI Mentality (`0x00487850`) is not 3, every pair value in the
  6 × 6 table at `0x004AB590` that is below 10 is raised by 1
  (`0x00472795..0x00472808`).
- One loop over players 0 to 5 (`0x0047281C`). For each player in turn it
  clears the report count at `0x004ABCA8 + player × 4` (`0x00472844`), then
  records for each of the 64 sectors whether the player has a gang there
  (`0x0047284F..0x004728E1`), then clears two per-gang arrays of that player.
- One loop over the 64 sectors (`0x00472964..0x00472ACD`). It compares the
  sector's byte `+2` with 17 minus its byte `+1` and moves `+2` one step toward
  that value: up by 1 at `0x00472A86`, down by 1 at `0x00472AC0`.

The Instant scan (`0x00472AD9..0x00473106`) visits players 0 to 5 and, for
each, roster slots 0 to 80. It copies the 32-byte record to a local and tests
its sector byte against 100 at `0x00472B50`; only a record whose sector is not
100 reaches the switch on the action byte (`0x004730A6`). The cases start at
`0x00472B71` (Bribe, 2), `0x00472BFC` (Heal, 7), `0x00472D00` (Hide, 8),
`0x00472D0C` (Influence, 9), `0x00472ED4` (Research, 11) and `0x00473046`
(Snitch, 13). The whole record is copied back after the switch
(`0x004730D5..0x004730FF`).

- The Hide case is one instruction pair: it adds 1 to the dword at
  `0x004A25D0 + player × 4` and leaves the switch.
- The Influence case compares the site's progress (sector byte `+8 + 2 ×
  target`) with the definition's `resistance` (`0x004AB67E + definition ×
  0x3E`) at `0x00472D0C..0x00472D3E`, rolls at `0x00472DC3`, `0x00472DE9` or
  `0x00472E0F`, clamps and records the completion report
  (`0x00472E6B..0x00472E98`), and writes the progress at `0x00472EA6`.

After the scan, `0x0047310B..0x0047317B` sets every sector's byte `+2` below 1
to 1 and above 40 to 40.

The Chaos sector pass follows. Its neutralizing branch is
`0x00473705..0x00473768`: report, owner -1 at `0x00473735`, and progress 0 at
`0x00473746`, `0x00473757` and `0x00473768` (FND-POLICE-004).

The police loop (`0x004740B7..0x0047424B`) visits players 0 to 5 and roster
slots 0 to 80, and makes its test only for a record whose sector byte is not
100 and whose sector's presence byte is above 0 (`0x0047414B`).

The Control block is the loop over the 64 sectors at `0x00475409..0x0047585D`.
It writes a new owner at `0x004757D4` and sets the three progress bytes to 0 at
`0x004757E4`, `0x004757F5` and `0x00475806`, then calls the report recorder
for control gained and lost. Its exit at `0x00475425` jumps to `0x00475862`.

The hire block is the loop over players 0 to 5 and offer slots 0 to 2 at
`0x00475862..0x00475E11`. Its exit at `0x0047587E` jumps to `0x00475E16`.
Inside it, the free-slot search at `0x00475BDB..0x00475C2D` starts at roster
slot 0 and stops at the first slot whose sector byte is 100 or at slot 80
(`0x50`), whichever comes first; the hire is made only when the index is below
80.

The presence countdown is the loop over the 64 sectors at
`0x00475E16..0x00475E88`: a presence byte that is above 0 and below 100 is
decreased at `0x00475E74`. Its exit at `0x00475E32` jumps to `0x00475E8D`.

Then, in this order:

1. `0x00475E8D..0x00475EB5` copies the six active bytes at `0x004ABBE0` to a
   local array.
2. `0x00475ECD` calls `fn_00476F3B`.
3. `0x00475ED2..0x00475F4F` compares each active byte with the copy and, for
   each player whose byte changed, records a report of type 9 for recipients 0
   to 5.
4. `0x00475F61` calls the end evaluator `fn_00476857`, and the function
   returns.

In `fn_00476F3B`, the Eliminate test at `0x00476F44` compares the scenario
(`0x004ABBE8`) with 7. For each of players 0 to 5, `0x00476F73` tests roster
slot 0's definition byte (`0x00498DA9 + player × 0xA20`) against 0 and its
sector byte (`0x00498DAA + player × 0xA20`) against 100; when the definition
is nonzero or the sector is 100 the player loses its sectors and gangs. The
general test follows at `0x00477069`, and the active byte is cleared at
`0x00477114`. Neither loop reads the active byte.

## Interpretation

Resolution runs its steps in exactly the order of their addresses:
Tolerance drift, then `instant_phase`, `chaos_phase`, the combat and police
work, the transactions, Chaos income, Terminate, Move, `control_phase`,
`hire_phase`, and `turn_end`. The exit of the Control loop jumps straight to
the hire loop, so `hire_phase` follows `control_phase` by control flow.

At the start of resolution the Last Turn report count and the Crackdown
presence snapshot are made in one loop, player by player; neither draws, so
the interleaving has no effect on the result. The return of Tolerance toward
17 minus Income runs once per turn, at the start of resolution, before any
instant action.

The Instant scan skips inactive records before looking at the action. Hide
does nothing in the scan except count.

In `turn_end` the presence countdown comes first, then the elimination helper,
its reports, and the end evaluation.

A hire goes into the first free roster slot from 0 to 79. Slot 0 can receive a
hire once the Right Hands record there is inactive; slot 80 never receives
one. In Eliminate, a player whose slot 0 holds a hired gang (definition not 0)
is treated as having lost the Right Hands.

The Eliminate scan does not skip a player who is already inactive; for such a
player it finds no owned sector and writes 100 again into records that already
hold it.

## Alternatives

None known for the order of `control_phase`, `hire_phase` and `turn_end`, which rests on the jump targets quoted. For the earlier blocks the order is that of their addresses in straight-line code; not every loop exit was checked. The purpose of
the pair table raised in the prologue belongs to the computer players'
findings.

## How to reproduce

In `fn_00472775`, read the loop exits at `0x00475425`, `0x0047587E` and
`0x00475E32` and their targets; the calls at `0x00475ECD` and `0x00475F61`
follow. Find the sector test at `0x00472B50` before the action switch, and the
Hide case at `0x00472D00`. In `fn_00476F3B`, read the two byte tests at
`0x00476F73`.
