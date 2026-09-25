---
id: FND-STATE-001
title: City generation stores Income and Tolerance in sector bytes 1 and 2, and the refresh before planning rebuilds bytes 3 to 6, 0x0D, 0x0E and 0x16 to 0x23 from them and the completed sites
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475FE1..0x004764B5
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004782C5..0x0047862F
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472A50..0x0047317B
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004127D4..0x00412856
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0044398E
tool: Ghidra 12.1.3
environment: null
---

## Observation

All offsets are in the 36-byte sector record at `0x004A08E8 + sector * 0x24`.
FND-EXE-004 gives the ranges of the functions named here.

City generation, `fn_00475FE1` (called once, from `0x0046DFE8` in the new-match
setup `fn_0046DC10`):

- first sets bytes 1, 2 and 6 and byte `0x0F` of every sector to 0
  (`0x004760BA`, `0x004760C8`, `0x004760D6`, `0x004760E4`) and the three
  site-definition bytes to 99;
- then, for each sector, stores the generated value `3 + ...` in byte 1 at
  `0x0047634A`, and `17` minus byte 1 in byte 2 at `0x0047636C`. It sets byte 6
  to 0 again at `0x00476379`, the site bytes to -1 and then to the drawn sites,
  and the three progress bytes to 0.
- It writes nothing at bytes 0, 3, 4, 5, `0x0D`, `0x0E` or `0x10..0x23`.

The refresh before planning, `fn_004782C5`, takes one sector record by value
(at `EBP+0x0C`), changes it, and returns the whole 36 bytes through its first
argument (`0x00478616..0x00478621`, nine DWORDs). It is called from
`0x0046F246` and `0x0046F77E` in `fn_0046E766`. Its writes, in record offsets:

| Offset | Instruction | Value |
|---|---|---|
| `0x03` | `0x004782D2`, then `0x004783A2` | 1, plus the Cash field (`+0x1E`) of each completed site |
| `0x04` | `0x004782D9` | a copy of byte 1 |
| `0x05` | `0x004782DF`, then `0x004783C3` | a copy of byte 2, plus the Tolerance field (`+0x1C`) of each completed site |
| `0x06` | `0x004782CE`, then `0x00478381` | 0, plus the Support field (`+0x18`) of each completed site |
| `0x0D` | `0x0047831A`, then `0x004785C1` or `0x004785D7` | 0; raised to at least 1 by a completed site whose special field (`+0x3C`) is 1, and to at least 2 by one whose special field is 2 |
| `0x0E` | `0x0047831E`, then `0x004785E0` | 0; set to 1 by a completed site whose special field is 3 |
| `0x16..0x23` | `0x004782E2..0x00478316`, then `0x004783E4..0x00478591` | 0, plus the site fields `+0x20, +0x22, ... +0x3A` of each completed site, in the same order: record byte `0x16 + k` takes site field `0x20 + 2k` |

The site fields are read from the table at `0x004AB668` with a stride of
`0x3E`, indexed by the signed site-definition byte of each slot (bytes `0x07`,
`0x09`, `0x0B`). A slot counts as completed when its signed progress byte
(`0x08`, `0x0A`, `0x0C`) is at least the signed 16-bit Resistance (`+0x16`):
the skip at `0x0047835D` is taken only when Resistance is greater than
progress. Each 16-bit site field is added to the byte as a signed value and
the sum is stored as a byte. Bytes 0, 1, 2, `0x07..0x0C`, `0x0F` and
`0x10..0x15` pass through unchanged.

Byte `0x0E` has no other writer. Its four direct references are reads:
`0x0043F36D`, `0x0044D498`, `0x0044DC36` and `0x004749B2`. Byte `0x0D` has two
reads: `0x004051E5` in the computer players' selector `fn_00402D70`, and
`0x0044398E` in the item-list builder `fn_004437E7`. That builder reads byte
`0x0D` of the gang's sector only when the gang's player owns the sector (0
otherwise), and caps the Tech Level of the listed items at 5 when the byte is
0 and at 8 when it is 1; with 2 it applies no cap below the limit passed in.

Byte 2 is written only by `fn_00475FE1` and by the whole-turn resolver
`fn_00472775`: a step of one toward `17 - byte 1` at the start of resolution
(`0x00472A86` increments, `0x00472AC0` decrements), `+3` at `0x00472BAC` in
the case for action 2 (Bribe) of the switch on the action byte, `-3` at
`0x00473065` in the case for action 13 (Snitch), and, in the loop over all 64
sectors that follows that switch, a clamp to 1 (`0x00473150`) and to 40
(`0x0047317B`).
Byte 5 has three direct references, all reads (`0x00402F1D`, `0x004121F1`,
`0x0047358D`). Byte 1 is read by the resolver at `0x00472A50`, as the target
of that step.

Readers of bytes 3 to 6 that earlier entries left open: the Control pass of
the resolver adds byte 4 and byte 6 to the defending owner's pool
(`0x00475573`, `0x00475584`) and subtracts them from each player's pool
(`0x00475625`, `0x00475636`). The computer players' selector `fn_00402D70`
returns byte 5 for case 4 (`0x00402F1D`), byte 6 for case 5 (`0x00402F33`),
byte 3 for case 6 (`0x00402F5F`) and byte 4 for case 7 (`0x00402F49`).

Bytes `0x10..0x15`: the only writer is the city map drawer `fn_004123CC`. For
each player slot `p` it clears byte `0x10 + p` of all 64 sectors
(`0x004127D4`), then sets it to 1 (`0x00412856`) for the sector of each living
gang of player `p` whose `visible_to` byte for the viewing player
(`0x004ABC84`) is nonzero. Bytes `0x11..0x15` have no direct references; the
code reaches them as `0x004A08F8 + p`. Their readers include the map and panel
code (`0x00412C66`, `0x00413B14`, `0x004146EA`, `0x00415371`, `0x0041795E`)
and the Attack picker `fn_0043B290` at `0x0043B7D1`.

## Interpretation

Bytes 1 and 2 are the sector's base Income and base Tolerance. The value shown
and used as Income (byte 4) is a copy of byte 1 made before every planning
phase, and the Tolerance shown (byte 5) is the base plus the completed sites'
Tolerance. Bribe, Snitch, the drift toward the generated value and the 1 to 40
clamp all act on the base in byte 2, so their effect reaches byte 5 only at
the next refresh. Byte `0x0D` is the sector's research-site level (0, 1 or 2)
and limits which items can be bought there; byte `0x0E` is the Factory flag,
set by a completed site whose special field is 3. Bytes `0x10..0x15` record,
for the player at the screen, which players have a visible gang in each
sector; they are rebuilt whenever the city map is drawn.

A completed site is one whose progress is at least its Resistance. The
refresh does not test for equality.

## Alternatives

- FND-UI-035 says the refresh leaves byte 4 unchanged. The store at
  `0x004782D9` copies byte 1 into it, so byte 4 is rewritten each time with the
  same value as long as byte 1 does not change; nothing else writes byte 1
  after city generation.
- What the Tech Level limit passed to `fn_004437E7` is has not been read here.

## How to reproduce

List the references to `0x004A08E9`, `0x004A08EA`, `0x004A08EC`,
`0x004A08ED`, `0x004A08F5`, `0x004A08F6` and `0x004A08F8`. In `0x004782C5`,
read the stores to `[EBP+0x0F]`, `[EBP+0x10]`, `[EBP+0x11]`, `[EBP+0x12]`,
`[EBP+0x19]`, `[EBP+0x1A]` and `[EBP+0x22..0x2F]` (record offset = `EBP`
offset - `0x0C`), the compare at `0x0047835B`, and the switch on the special
field at `0x004785EE..0x00478606`.
