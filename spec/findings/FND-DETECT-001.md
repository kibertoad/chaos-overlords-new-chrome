---
id: FND-DETECT-001
title: The visibility rebuild takes each sector's best Detect and adds a helper bonus from every other friendly gang there
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046FA11
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046E766
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043B290
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0043D132
tool: Ghidra 12.1.3
environment: null
---

## Observation

- The visibility rebuild `0x0046FA11` is called once from the computer
  planning entry path and once from the human planning entry path of the outer
  turn function `0x0046E766`.
- For each observing player it sets 64 sector strengths to -32000, or to 1000
  when that player's byte in the name-modifier array at `0x004AB588` is set
  (FND-SETUP-011). It then scans the observer's 81 gang records. A record is active when its sector byte (offset
  `0x02`) is not 100. A strictly-greater comparison picks, in each occupied
  sector, the first gang with the highest effective Detect (signed byte at
  offset `0x15`) as the base, and remembers its roster slot.
- A second scan of the 81 records adds every other active gang in the sector as
  a helper. Each helper adds 1. When its signed Detect is greater than 9, it
  adds `(Detect - 8) / 2` more, with signed truncating division. So Detect up
  to 9 adds 1, 10 and 11 add 2, 12 and 13 add 3, 14 and 15 add 4, 16 and 17
  add 5, 18 and 19 add 6, and each further pair adds one more with no cap. A
  helper with negative Detect still adds 1.
- It then scans the active gang records of every other player and marks a gang
  visible to the observer exactly when its effective Stealth (signed byte at
  offset `0x14`) is less than or equal to the observer's strength for the
  gang's sector. The observer's own active records are marked visible without a
  test.
- The Attack picker `0x0043B290` calls the roster builder `0x0043D132` with the
  selected enemy player and the acting gang's sector. The builder scans that
  player's 81 records and lists a record only when its sector matches and its
  byte at offset `0x0C + observer` is nonzero. Only listed roster slots can be
  written back as the Attack target.

## Interpretation

What a player can see in a sector is decided once per planning entry, by the
best friendly Detect there plus a bonus for every other friendly gang. Roster
order only chooses which of several equally good gangs is the base, and since
equal values add the same bonus, it cannot change the total. Hide is not read
here and does not change what others can see. A gang can be attacked only if
its attacker's player sees it.

## Alternatives

None known.

## How to reproduce

Find `0x0046FA11` from its two calls in `0x0046E766`; read the constants
-32000, 1000, 9 and 8, the loads of record offsets `0x02`, `0x14` and `0x15`,
and the store to offset `0x0C + observer`.
