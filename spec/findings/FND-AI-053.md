---
id: FND-AI-053
title: The difficulty band table is set from controller 1 only, saved, and read at nine places in the resolver
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DD67..0x0046DE21
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472BFC
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00473DF7
tool: Ghidra 12.1.3
environment: null
---

## Observation

The references to the six 32-bit values at `0x004A2570` are three stores in
`0x0046DC10`, nine loads in `0x00472775`, and the address pushed by the save
writer `0x0046381A` (`0x00463B06`) and the loader `0x00463CC5` (`0x0046400B`).

In `0x0046DC10` the loop at `0x0046DD67` stores 1 for every player. The
switch on the Mentality at `0x0046DE21` (cases 0 to 3, larger values skip it)
then runs, for Mentality 0, a loop that stores 0 (`0x0046DDB3`) for each player
whose controller at `0x004AB638 + player * 4` equals 1 (`0x0046DDA2`), and for
Mentality 2 and 3 a loop that stores 2 (`0x0046DDFA`) under the same test
(`0x0046DDE9`). Mentality 1 stores nothing more.

The nine loads in `0x00472775`, in address order, and the dice thresholds
passed to `0x00475F70` after each for bands 0, 1 and 2:

| Load | Use | Thresholds |
|---|---|---|
| `0x00472BFC` | Heal | 5, 5, 4 |
| `0x00472D78` | Influence | 5, 5, 4 |
| `0x00472F05` | Research | 6, 6, 5 |
| `0x0047328B` | Chaos pool | 5, 5, 4 |
| `0x0047350A` | Chaos, owner's band compared with 2 | none |
| `0x004739CC` | evasion of a hidden target | none; adds 14 or 10 |
| `0x00473B47` | defender's band, Defense lowered by a quarter (`SAR 2`) | none |
| `0x00473BA0` | main attack | 6, 5, 4 |
| `0x00473DF7` | retaliation, successes halved (`SAR 1`) | 5, 5, 4 |

## Interpretation

These are the nine reads FND-AI-007 describes, in the order it lists them.
Only controller 1, a local computer player, has its band changed; a human
(controller 0) and a network player (controller 3) keep band 1 at every
Mentality. The table is saved, so a loaded match keeps the bands it was
started with.

## Alternatives

None known.

## How to reproduce

List the references to `0x004A2570`. In `0x0046DC10` read the stores and the
comparisons with `0x004AB638`. In `0x00472775` read the `PUSH` of each
threshold before the calls to `0x00475F70` that follow each load.
