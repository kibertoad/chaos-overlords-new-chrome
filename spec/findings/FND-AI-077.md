---
id: FND-AI-077
title: Family 2's late Control gates read a local that holds the item of a planned Equip
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0041FEF0..0x0042086C
tool: Ghidra 12.1.3
environment: null
---

## Observation

The family-2 handler `0x0041FEF0` (FND-AI-058) keeps two locals that its
late Control gates read:

- `[EBP-0x4]` holds the result of selector `0x5A`, the gang's sector, stored
  once at `0x0041FF18`.
- `[EBP-0x10]` is written four times: at `0x0041FF2F` with the result of
  selector `0x64`, the armor upgrade the gang would take, or -1; at
  `0x0042009D` with the result of selector `0x61`, the weapon upgrade, on the
  path that does not equip the armor; and at `0x004201FA` and `0x0042038A`
  with `[EBP-0x4]`, on the paths that equip nothing.

The armor Equip branch ends with the `JMP 0x00420779` at `0x00420084`, and
the weapon Equip branch with the one at `0x004201F2`, before the write at
`0x004201FA`. Every other branch reaches `0x00420779` after one of the two
writes of `[EBP-0x4]`.

The gates at `0x00420779..0x0042086C` pass their arguments as follows:

| Call | Selector | Arguments |
|---|---|---|
| `0x00420783`, `0x004207FF`, `0x00420847` | `0x21`, the owner query | `[EBP-0x10]` |
| `0x004207AE` | `0x28`, the count of visible gangs of human players | the player, `[EBP-0x4]` |
| `0x004207C8` | `0x35`, the human-owner test | `[EBP-0x4]` |
| `0x0042082D` | `0xB0`, the count of the owner's gangs the player sees | the player, `[EBP-0x10]` |

The attitude read at `0x00420794` and `0x00420810` and the pair flag read at
`0x0042085D` are indexed by the owner query's result.

## Interpretation

When the gang plans an Equip, the late gates take the owner, the attitude
toward it, its visible gangs and the combat-advantage flag from the sector
numbered like the item, while the human gang count and the human-owner test
still read the gang's own sector. After any other action every argument is
the gang's sector, as FND-AI-058 reads it. Item numbers run from 0 to 52, so
the misread sector is always one of the 64.

## Alternatives

The selector could treat its argument as an item for these selectors. The
hostility step `0x0040A1A7` calls selector `0xB0` with the player and the
sector it loops over (`0x0040A45C`), and selector `0x21` takes a sector
everywhere else, so the gates read the item as a sector.

## How to reproduce

List the writes to `[EBP-0x10]` in `0x0041FEF0`, the references to
`0x00420779`, and the arguments pushed before each selector call in
`0x00420779..0x0042086C`. EXP-TURN-055 and EXP-TURN-056 show the result in
the running original.
