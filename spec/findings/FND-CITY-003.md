---
id: FND-CITY-003
title: Headquarters go to six fixed sectors by a random permutation, and each player's Right Hands starts there at Force 10
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00439563
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476726
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x00439563` writes the sector numbers 9, 12, 30, 33, 51 and 54 into a table
of six dwords (the city renderer reads the same table at `0x00494818`, see
FND-UI-033).

`0x00476726` builds a permutation of six values by calling the bounded random
wrapper `0x0045D227` with 6 repeatedly for each player and rejecting any value
already taken. It maps each player through the permutation to one of the six
table entries, makes that player the owner of the sector, and writes site
definition 21 into site slot 0 of the sector.

After that call returns, the fresh-game initializer `0x0046DC10` creates in
each player's assigned sector a gang of definition 0 at Force 10.

## Interpretation

The six fixed sectors are the only possible headquarters in a new game, and
which player gets which is random. Definition 21 is the headquarters site.
The Right Hands is gang definition 0 and always starts at full Force. In a
local game every slot has a player by the time this runs (FND-SETUP-002), so
all six headquarters are taken.

## Alternatives

The observation does not say which roster slot the Right Hands takes; the
turn-end elimination check reads roster slot 0 as the Right Hands
(FND-TURN-003), and FND-SETUP-004 says the name modifiers fill slots 1 to 5
after the normal Right Hands, so slot 0 is the reading used. Whether the
permutation value is used as `value - 1` into the table, and what the
generator writes to the headquarters slot's influence progress, is not
recorded. The equipment bytes of the Right Hands record are not stated here;
FND-SETUP-004 describes the extra Right Hands as unequipped.

## How to reproduce

Open `0x00476726`, called from `0x0046DC10` after the city generator
`0x00475FE1`. Find the loop over six players that calls `0x0045D227` with 6
and retries on a value already present, the owner write into the sector
record, and the store of 21 into the first site slot. The table of six sector
numbers is written by `0x00439563`.
