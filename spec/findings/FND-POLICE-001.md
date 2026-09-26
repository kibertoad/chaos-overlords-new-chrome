---
id: FND-POLICE-001
title: Each sector keeps its last two Crackdown turns; a third within five turns neutralizes the sector, and each Crackdown adds 3 to 5 police turns
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
    address: 0x004ABCC0..0x004ABDC0
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047415E
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475E74
tool: Ghidra 12.1.3
environment: null
---

## Observation

- Each sector's two signed 16-bit Crackdown turns live at
  `0x004ABCC0 + sector * 4` and `0x004ABCC2 + sector * 4`.
- In the sector pass of `0x00472775`, before a new trigger is evaluated, a slot
  that is not -100 is set to -100 only when it is strictly less than the
  current turn minus 5.
- When the sector triggers, the current turn is written into the first empty
  (-100) slot, or else the second. If neither is empty, the resolver reports
  the loss of control to the owner, sets the sector's owner to -1, clears the
  sector's three influence-derived values, and writes the current turn into
  both slots.
- Only after that block does the resolver make one bounded draw for a value
  from 1 to 3, add 2, and add the result, 3 to 5, to the sector's police
  presence byte (sector record offset `0x0F`).
- Earlier in the same sector pass the resolver has already set the triggering
  sector's per-gang Chaos results to 0 and sent the reports to the players
  concerned (FND-POLICE-002).
- The police scan later reads the presence byte at `0x0047415E`, so it sees
  the new value.
- Near the end of the whole-turn resolver, `0x00475E74` decreases by one every
  presence byte that is positive and less than 100, after that turn's police
  attacks.

## Interpretation

The five-turn window is inclusive: a Crackdown exactly five turns before the
current one still counts. A third Crackdown in the window neutralizes the
sector and fills both slots with the current turn, so another Crackdown while
they are recent neutralizes a retaken sector again. A Crackdown adds to police
presence already there. Its one draw comes after the history update and any
neutralization. A new Crackdown's police attack in the same turn, and the
decrease at the end of the turn then leaves 2 to 4 further police Combat
phases.

## Alternatives

- The old record of this finding put the duration draw at `0x0047419B`.
  FND-POLICE-003 puts the police detection draw (1 to 100) at the same address,
  just after the police read at `0x0047415E`. The duration draw belongs to the
  sector pass after the Chaos rolls, well before the police scan, so
  `0x0047419B` is taken here to be the detection draw, and the address of the
  duration draw is not recorded.
- Which values "the three influence-derived values" are was not recorded. The
  sector record's three site slots each hold an influence progress byte
  (FMT-STATE-004), which fits.
- Which counter "the current turn" is was not recorded.

## How to reproduce

Find the reads and writes of `0x004ABCC0` in `0x00472775`: the comparison with
-100, the subtraction of 5, and the fill and reset of the two slots. Follow the
bounded-random call with argument 3 after it. Find `0x00475E74` from the
resolver's end and read its comparisons with 0 and 100.
