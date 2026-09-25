---
id: FND-AI-057
title: The family-1 handler's switch has a fourth branch for Attack, Hide and Move, and its crime gate falls through to the Goon test
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00434080..0x004353A0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00403741
tool: Ghidra 12.1.3
environment: null
---

## Observation

Family 1's handler `0x00434080` compares the previous action with 13 at
`0x004352C2` and dispatches at `0x004352D7` through the byte table at
`0x00435302` and the address table at `0x004352DE`. The case labels are:

- 0 and 3 go to `0x004340BC`;
- 4, 5 and 13 go to `0x00434357`;
- 1, 8 and 10 go to `0x00434A38`;
- 7 goes to `0x0043486F`;
- 2, 6, 9, 11 and 12 go to `0x00435310`, which writes nothing.

The branches for 0 and 3 and for 7 are as FND-AI-020 describes them.

In the branch for 4, 5 and 13, the equipment step runs only when selector
`0x6C` is positive, as in FND-AI-021, and a written Equip ends the branch. The
crime gate then has two tests in sequence:

1. Selector `0x35` nonzero, cash at least 50 and Mentality at least 1: crime.
2. Otherwise, whatever the owner, the owner query (selector `0x21`) is compared
   with the active player and with 0. When it differs from the player, is
   greater than 0, cash is greater than 49 and Mentality is exactly 0: crime.

Anything else writes Move through mode 5. The crime is Chaos when selector 4
(the sector's Tolerance) is below 4 and Snitch otherwise.

Selector `0x35` (`0x00403741`) indexes the controller table at `0x004AB638`
with the sector's raw owner byte and returns 1 when the value is 0 or 3. It
has no range test: for a neutral sector (owner -1) it reads `0x004AB634`, the
last entry of the casualties table at `0x004AB620` (player 5's count).

The branch for 1, 8 and 10 starts from the cached opponent weight of the
current sector (selector `0xAF`):

- At weight 10: one target draw. The pool is the visible gangs of human
  players when the owner query's column of the player's attitude row is
  negative and the weight is 10, and all visible opponents otherwise; selector
  `0x2B` compares with the same ordinal in the full list. A passing comparison
  writes Attack on the drawn gang and stores the current sector in the first
  auxiliary value.
- At other weights: when the owner query equals the active player
  (`0x00434F2C`), Move through mode 5.
- After a failed comparison, and at other weights in a sector the owner query
  does not give to the player (`0x00434F41..0x004351F5`): Heal when Force is
  below 9 and effective Heal is at least -3; otherwise Control when selector
  `0x2C` accepts the sector; otherwise a Snitch gate. The gate passes when
  selector `0x35` is nonzero and either the attitude read above is negative
  and Mentality is at least 1 (`0x00434DA6`, `0x00435090`), or Mentality is
  exactly 2 (`0x00434DD9`, `0x004350C3`). A passing gate with cash greater than
  50 writes Snitch; everything else writes Move through mode 5. Heal, Control,
  Snitch and Move store -1 in the first auxiliary value.

After the switch, in scenario 0 with fewer than four turns remaining, the
handler writes Terminate and sets `needs_family` (FND-AI-042).

## Interpretation

The branch for Attack, Hide and Move is the one FND-AI-020 recorded without its
previous actions: the Snitch that needs cash above 50 and the exact-Mentality-2
gate both belong to it. FND-AI-020's crime gate is incomplete. A human owner
that fails the first test still reaches the second, so at Goon a gang commits a
crime in a sector owned by a human in slot 1 to 5. The second test uses the
owner query, so a sector under police presence (query -2) never passes it.
A neutral sector passes selector `0x35` whenever player 5 has 0 or 3
casualties, so a gang can commit a crime in a neutral sector at Mentality 1 or
2.

## Alternatives

The table at `0x004AB634` could be meant as a separate value placed before the
controller table. Nothing in the handler range-tests the owner, and the
casualties table is six entries long, so the read lands on its last entry.

## How to reproduce

Open `0x00434080`. Read the compare at `0x004352C2`, the byte table at
`0x00435302` and the address table at `0x004352DE`. In the branch at
`0x00434A38`, follow the six selector `0x36` calls listed above and the cash
compares with 50 (greater than) and 49 (greater than). Open the selector
function `0x00402D70` at case `0x35` (`0x00403741`) for the controller read.
