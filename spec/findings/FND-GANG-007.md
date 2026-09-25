---
id: FND-GANG-007
title: The statistics rebuild pairs the fourteen fields of the gang, item and site records in one order, adds the weapon's combat skills to Combat, and runs for every active gang at the top of each turn, including a gang hired in the turn before
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047781F..0x004782C4
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046F27C..0x0046F312
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475AEC..0x00475BA9
tool: Ghidra 12.1.3
environment: null
---

## Observation

Ranges of the functions named here are in FND-EXE-004.

`fn_0047781F` takes the 32-byte gang record by value and returns the rebuilt
copy. At `0x00477830` it sets a flag when the record's sector byte is not 100
and the owner byte of that sector (`0x004A08E8 + sector * 0x24`) equals the
record's player byte. It then builds the fourteen statistic bytes one at a
time. Each starts from a 16-bit field of the gang definition
(`0x004A2800 + definition * 0x9C`, so the offsets below are record offsets),
adds the 16-bit field of each of the weapon, armor and miscellaneous items
that is not -1 (`0x004A5F08 + item * 0xA6`), and adds the sector byte when the
flag is set. The fields are built in the order `0x13` to `0x1F`, then `0x12`:

| Gang record | Definition | Item | Sector record | Stored at |
|---|---|---|---|---|
| `0x13` | `0x80` | `0x84` | `0x17` | `0x00477908` |
| `0x14` | `0x84` | `0x86` | `0x18` | `0x004779BB` |
| `0x15` | `0x86` | `0x88` | `0x19` | `0x00477A6E` |
| `0x16` | `0x88` | `0x8A` | `0x1A` | `0x00477B21` |
| `0x17` | `0x8A` | `0x8C` | `0x1B` | `0x00477BD4` |
| `0x18` | `0x8C` | `0x8E` | `0x1C` | `0x00477C87` |
| `0x19` | `0x8E` | `0x90` | `0x1D` | `0x00477D3A` |
| `0x1A` | `0x90` | `0x92` | `0x1E` | `0x00477DED` |
| `0x1B` | `0x92` | `0x94` | `0x1F` | `0x00477EA0` |
| `0x1C` | `0x94` | `0x96` | `0x20` | `0x00477F53` |
| `0x1D` | `0x96` | `0x98` | `0x21` | `0x00478006` |
| `0x1E` | `0x98` | `0x9A` | `0x22` | `0x004780B9` |
| `0x1F` | `0x9A` | `0x9C` | `0x23` | `0x0047816C` |
| `0x12` | `0x7E` | `0x82` | `0x16` | `0x004782A8` |

The definition field at `0x82` is not read.

The byte at `0x12` gets one more term, chosen at `0x0047818C` by the weapon
byte (offset `0x04`), using the bytes just rebuilt at `0x1B`, `0x1C`, `0x1D`,
`0x1E` and `0x1F`:

- no weapon (-1): adds `0x1B + 0x1E + 0x1F` (`0x00478195..0x004781A5`);
- otherwise it adds the weapon's field `0x82` (`0x004781BC`) and switches on
  the weapon's 16-bit type at item offset `0x7A` (`0x004781D6`): type 0 adds
  `0x1B` (`0x004781E6`), type 1 adds `0x1B + 0x1C` (`0x004781F2`), type 2 adds
  `0x1D` (`0x00478204`), and any other type adds nothing.

The armor's and miscellaneous item's field `0x82` (`0x00478254`,
`0x0047826C`) and, with the flag, the sector byte `0x16` (`0x00478293`) are
added after that term. The copy is returned at `0x004782B6`.

At the top of the turn loop of `fn_0046E766`, `0x0046F27C..0x0046F312` scans
player slots 0 to 5 and roster slots 0 to 80, skips a record whose sector byte
is 100 (`0x0046F2B7`), calls `fn_0047781F` (`0x0046F2FD`) and writes the
result back. This block runs on every pass of the loop, the first included,
right after the sector rebuild through `fn_004782C5` (`0x0046F246`). The
whole-turn resolver runs later in the same pass (`0x0046F706`, through
`fn_004726C0`), and the loop then returns to its top. The only other call of
`fn_0047781F` (`0x0046F835`) is in a block that runs after the resolver only
when the byte `0x004ABBD4` is nonzero, and that block also sets every active
gang's action byte to 8.

The hire block of the resolver, `0x00475AEC..0x00475BA9`, fills the new
record's bytes `0x12` to `0x1F` from definition fields `0x7E`, `0x80`, `0x84`,
`0x86` and `0x88` to `0x9A`, the same pairing as the table, without items,
sites or the combat term.

## Interpretation

- The fourteen statistics lie in the same order in the gang definition
  (from `0x7E`, skipping `0x82`), the item record (from `0x82`), the sector
  record's site sums (from `0x16`) and the gang record (from `0x12`), so the
  field names of one record carry over to the others. The definition field
  at `0x82`, left out here, is the gang's Tech Level (FND-EQUIP-008).
- The byte at `0x12` holds the gang's whole combat rating, its Combat
  statistic plus the skills of its weapon: bare handed it includes Strength, Fighting and Martial Arts, with
  a weapon of type 0 Strength, type 1 Strength and Blade, type 2 Ranged. The
  skills added are the rebuilt values, items and sites included. Nothing
  later adds the skills again: the attack pool reads Force plus this byte
  (FND-AI-007).
- A gang hired during a turn's resolution has raw definition values in its
  statistic bytes until the top of the next pass, where the rebuild runs for
  it like for every other active gang before its first planning phase.

## Alternatives

- The block guarded by `0x004ABBD4` was not identified further; it is not the
  normal flow of a turn.
- Item types 3 and 4 can reach the weapon byte only if something other than
  the transaction pass stores them there; the pass does not (FND-EQUIP-007).

## How to reproduce

Open `fn_0047781F` and follow its fourteen blocks, each reading a definition
word with stride `0x9C`, three item words with stride `0xA6` and one sector
byte with stride `0x24`. The last block compares the weapon byte with -1 at
`0x0047818C` before the type switch. In `fn_0046E766`, the call at
`0x0046F2FD` sits in the player-then-roster loop that follows the sector
rebuild at `0x0046F246`.
