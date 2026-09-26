---
id: FND-AI-062
title: Families 13 and 14 make up to five draws on a contested objective, can write nothing after a failed attack, and compare Support with an unset value
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0040ABC0..0x0040B9B9
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00466910..0x004677EE
tool: Ghidra 12.1.3
environment: null
---

## Observation

Neither handler has a jump table. Their prologues (`0x0040ABC9..0x0040ABD7`,
`0x00466919..0x00466927`) set local -0x18 (the chosen site) to -1, the draw
counter at local -8 to 0 and the stop flag at local -0x1C to 0. Nothing else
writes the draw counter before either loop.

On an objective whose owner query differs from the active player
(`0x0040AC21`, `0x00466971`):

- An odd turns-remaining value, or a cached weight of 0 or less, writes Control
  and focus -1 (`0x0040AF51`).
- Otherwise the loop runs while the counter is below 5 and the flag is clear.
  Each draw takes the human pool (selectors `0x28`, `0x29`) when the attitude
  toward the owner query is negative and the weight is 10, and otherwise
  selectors `0xAD` and `0xAE`: the gangs of other players whose number equals
  the sector's raw owner byte, whose sector is this one and whose visibility
  byte for the player is 1. A negative draw sets the flag, and so does a
  nonzero selector `0x2B`.
- After the loop (`0x0040AD8B`): a drawn gang and Force (selector `0x3C` with
  the active player and slot) of at least 5 write Attack, with the current
  sector as focus. Otherwise Force below 10 and effective Heal above -4 write
  Heal with focus -1, and when that test fails the branch jumps past every
  write (`0x0040ADC9` to `0x0040AF4C`).

On an objective the player owns (owner query equal to the player):

- Cached weight of 1 or more (`0x0040B00E`, `0x00466D59`): the same loop with
  the full pool in place of selectors `0xAD` and `0xAE`, then the same Attack,
  Heal or no write.
- Cached weight below 1: Heal at Force below 10 and effective Heal above -4
  (`0x0040B2F6`, `0x00467041`); otherwise the weapon and armor steps of
  FND-AI-039 with focus -1; otherwise selector `0x75`, whose item is taken
  when it is not negative and its cost is at most the cash, with no
  previous-action test and focus -1; otherwise the site scan.
- The site scan (`0x0040B713..0x0040B782`, `0x0046745E..0x004674CD`) walks
  slots 0 to 2. A slot with positive remaining Resistance (selector 10) whose
  selector `0xC` value is greater than local -0x14 becomes the chosen site,
  and local -0x14 takes that value. A chosen site writes Influence with the
  current sector as focus; none writes None (0) with focus -1.

Local -0x14 is written only by the two draw loops (`0x0040AD0A`,
`0x0040AD4E`, `0x0040B0A3`, `0x0040B0E7` and the matching stores in family
14). Neither loop runs on the path to the site scan, so the scan starts from
whatever the stack slot held when the handler was entered.

The terminal Move of family 13 and the Heal switch of family 14 are as
FND-AI-039 describes them; family 14's switch also tests the previous action
against Attack after testing it against Control.

## Interpretation

FND-AI-039 is wrong on four points. The contested loop makes up to five draws;
FND-AI-039's "starts from 2" read the argument pushed for selector 2 as the
counter. The contested human-pool test is the hostile-owner attitude test with
no human-owner test. After a failed attack a gang whose Force is 10 or more,
or whose effective Heal is below -3, gets no Control; the handler writes
nothing and the planning record keeps None from the start of the turn
(RULE-AI-001). The Support scan's starting threshold is not set by the handler,
so which site, if any, gets Influence depends on stack contents left by
earlier calls (BUG-AI-006). The Force tested before an attack is the acting
gang's.

## Alternatives

The value left in local -0x14 may be the same on every call if the callers'
stack use is fixed; a run of the original would show whether the scan behaves
as if it started from 0, from a negative number or from a large value.

## How to reproduce

Open `0x0040ABC0` in the instruction view. Read the prologue stores, the loop
compare with 5 at `0x0040AC71`, the jumps from `0x0040ADC9` to `0x0040AF4C`,
the scan's compare at `0x0040B75C`, and every store to `[EBP-0x14]`. Repeat in
`0x00466910`; the scan's compare is at `0x004674A7`. Open the selector function
`0x00402D70` at cases `0xAD` (`0x004072A7`) and `0xAE` (`0x00407367`).
