---
id: FND-CHAOS-001
title: Chaos is rolled gang by gang in roster order before Combat and paid after Transactions, halved once per player and sector outside the owner's sectors
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A08E8..0x004A11E8
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The Chaos pass (action 3) of `0x00472775` scans player slots 0 to 5 and
  each player's 81 roster slots in ascending order. For each Chaos gang it
  computes a dice pool from the sector byte at `0x004A08EC + sector * 0x24`,
  the gang's current Force and its effective Chaos, rolls that gang on its own,
  stores the gang's success count by roster slot, and adds the count to a
  total per player and sector.
- After all the rolls, a pass over the sectors compares totals with Tolerance
  for Crackdowns and sets the stored results of the participating gangs to 0
  when the sector triggers.
- A later payout pass rebuilds the per-player, per-sector totals from the
  stored per-gang successes. When the player does not own the sector, it
  divides the whole total by two once and then adds it to the player's cash.
  The division is not made per gang.
- Between the two halves the resolver runs the Attack block and the police
  scan, then the Transaction pass for actions 5, 6 and 12. After the payout it
  runs Terminate and Move, and then Control.
- The per-gang successes and the per-player, per-sector totals are locals of
  the resolver. The pass writes no sector field for Chaos.

## Interpretation

The resolver's order is: the instant actions; Chaos rolls and Crackdown
creation; Combat; Transactions; Chaos payout; Terminate; Move; Control. Chaos
draws follow player slot and then roster slot, whatever order the orders were
given in. A player's gangs in one sector share one total, and outside the
player's own sectors the payout is the total halved once with truncation, so an
odd success spread over several gangs is kept. A new Crackdown exists before
the same turn's police scan. The sector byte at offset `0x04` of the sector
record is the Income that city generation sets to 3 to 7, not the owner's cash
byte rebuilt from completed sites.

## Alternatives

- FND-UPKEEP-001 reads the Chaos pool as taking the byte at offset `0x03`, the
  cash byte. This finding places the read at `0x004A08EC + sector * 0x24`,
  which is offset `0x04` of a record at `0x004A08E8 + sector * 0x24`. Neither
  finding gives the address of the load instruction. The base address here
  comes from the decompiled expression; the instruction and its displacement
  have not been recorded.
- The instruction addresses of the Chaos pass, the sector pass and the payout
  pass have not been recorded.

## How to reproduce

In `0x00472775`, find the scan whose action test is against 3; read the load
from `0x004A08EC + sector * 0x24` that feeds the dice pool, the later pass over
64 sectors that compares with Tolerance, and the payout pass that divides by two
under an ownership test.
