---
id: FND-AI-042
title: Byte +1 of a planning record is set for an empty roster slot and by the Greed Terminate branches, and bytes +11 and +15 are never used
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00458FA0..0x0045C176
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00409DE1..0x00409F46
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048A250..0x0048C0AF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482108..0x00482177
tool: Ghidra 12.1.3
environment: null
---

## Observation

Byte +1 of the 16-byte planning record at
`0x0048A250 + player * 0x510 + slot * 0x10` is read only by selector `0x48`
(`0x00405A72`). It is written by these instructions and no others:

| Address | Function | Value | When |
|---|---|---|---|
| `0x00459036` | `0x00458FA0` | 1 | First pass of a player, record of roster slot 0, after all 81 records were reset |
| `0x0045930C` | `0x00458FA0` | 1 | Every pass, for each roster slot whose gang sector (gang byte +2) is 100 |
| `0x00409EFD` | `0x00409DE1` | 0 | The record reset |
| `0x00435387`, `0x0042093A`, `0x00436C55`, `0x0043B26F`, `0x004327AB`, `0x004384AD`, `0x00435BAF` | the handlers of families 1, 2, 3, 5, 6, 7 and 12 | 1 | At the end of the handler, when selector 2 (turns remaining) is below 4 and the scenario is 0; the same branch writes 14 (Terminate) as the planned action and as the gang's action |

At `0x00459303..0x00459353` an empty slot also has the 32-bit value at
`0x0048DB48 + player * 0x144 + slot * 4` and both 16-bit cooldowns at +12 and
+14 set to 0; its history bytes +2..+10 are not touched.

No instruction references byte +11 (`0x0048A25B`) or byte +15 (`0x0048A25F`)
of any record. They are moved only by the save and load functions, which
transfer the whole `0x1E60`-byte block from `0x0048A250`.

On a player's first pass (`0x00458FB1..0x00459040`), after calling
`0x00409DE1` for the 81 slots, `0x00458FA0` also clears the planned action of
slot 0, and writes 0 to the 32-bit values at `0x00482128`, `0x00482160` and
`0x00482140` (each `+ player * 4`). On every pass, before the history roll, it
copies `0x00482128 + player * 4` (the hire role) into
`0x00482160 + player * 4` (`0x0045904A`). The array at `0x00482140` is written
with 0 in every case of the hire switch and read by no instruction; the save
and load functions transfer it.

## Interpretation

Byte +1 says that the record needs a new family: the slot was empty at a
planning pass, so the next gang to occupy it is new, or the gang was ordered to
terminate at the end of a Greed match. The dispatcher wipes such a record and
assigns a family before the gang plans (FND-AI-041). This is how a reused slot
stops its new gang inheriting the previous occupant's family and history: the
history of an empty slot is kept until a new gang arrives, and the wipe then
clears it.

The previous hire role (`0x00482160`) is the role chosen at the end of the
previous turn's pass, copied at the start of the current pass whether or not
the player then hires.

Bytes +11 and +15 are padding. The array at `0x00482140` is set but unused.

## Alternatives

The flag is only set at a planning pass. A gang that dies in the combat of a
turn leaves its record with byte +1 at 0; if the hire phase of the same turn,
which runs after combat, places a new gang in that slot, the new gang is active
at the next pass and keeps the dead gang's family and history. The hire phase
searches the player's first 80 records for a free one (FND-HIRE-001), so a
slot emptied earlier in the same resolution can be taken; whether the search
takes the lowest free slot is not checked here.

## How to reproduce

List the references to `0x0048A251`: every write is a byte store of 0 or 1 at
the addresses above. The references to `0x0048A25B` and `0x0048A25F` are
empty. The first-pass block is at the start of `0x00458FA0`, guarded by the
byte at `0x00482108 + player`.
