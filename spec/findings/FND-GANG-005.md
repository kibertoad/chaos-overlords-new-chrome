---
id: FND-GANG-005
title: Combat deaths are counted in a per-player INT32 array at 0x004AB620, raised by one instruction in the damage loop that attack and police damage share
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0047476F..0x00474890
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB620..0x004AB637
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00476045
tool: Ghidra 12.1.3
environment: null
---

## Observation

The damage application of the whole-turn resolver `fn_00472775` (FND-EXE-004
gives its range) is the double loop at `0x0047476F..0x00474890`, over player
slots 0 to 5 and, inside each, roster slots 0 to 80. For each gang record it:

1. tests the sector byte (offset `0x02`) against 100 and skips the record when
   it is equal (`0x004747BF`);
2. subtracts the gang's accumulated damage from the Force byte (offset `0x03`)
   and stores the result back in the Force byte (`0x00474827`), for every
   active gang, including those with no damage;
3. compares the stored Force with 1 (`0x0047485A`); when it is below 1, stores
   100 in the sector byte (`0x0047487B`) and executes
   `INC dword ptr [player*4 + 0x004AB620]` (`0x00474889`), where `player` is
   the loop's player slot.

The accumulated damage is a local array of 486 32-bit entries, one per player
and roster slot, cleared at the start of the resolver. The attack block adds
to it at `0x00473C9F` (damage to the target) and `0x00473ED0` (damage back to
the attacker), and the police block adds to it at `0x00474202`. The loop at
`0x00474255..0x004742D7` caps every entry at 10 before the damage application
runs. Police damage therefore reaches the same subtraction and the same
increment as attack damage; no other instruction in the resolver writes
`0x004AB620`.

Every reference to `0x004AB620..0x004AB637` in the game code:

| Instruction | Function | Access |
|---|---|---|
| `0x00474889` | `fn_00472775` | increment, the only one |
| `0x00476045` | `fn_00475FE1` | stores 0 for each player slot, next to the stores of 0 into `0x004A25D0`, `0x0049CA78` and `0x004A27E0` |
| `0x0042D778`, `0x0042DDBA` | `fn_0042CE61` | reads one player's entry and passes it to the number drawer `fn_00414187` |
| `0x0046B576` | `fn_0046A7CB` | reads the six entries into a local buffer through `fn_00449DD3` |
| `0x0046C2B6..0x0046C2F4` | `fn_0046BA84` | passes the address and length 24 to `fn_004689B6`, then rewrites each of the six entries through `fn_00449E26` |
| `0x00463AAE`, `0x00463FB3` | `fn_0046381A`, `fn_00463CC5` | push the address as one entry of the save list (entry 11 of FND-SAVE-001, 24 bytes) |

The instruction at `0x0040C127` writes `0x004AB638`, the next array, and is not
a reference to this one.

## Interpretation

The casualty statistic is `INT32LE[6]` at `0x004AB620`, indexed by the player
slot that owned the dead gang. It counts every gang whose Force falls below 1 in
the damage application, whether the damage came from attacks, retaliation or
the police, and nothing else: a Terminate (which writes the sector byte in its
own pass) and the Eliminate clean-up `fn_00476F3B` do not raise it. It starts at
0 in a new game, travels with the save and with the session host's state
packet, and is drawn by the endgame statistics renderer.

A dead gang's Force byte keeps the reduced value, 0 or below, since the
subtraction is stored before the comparison.

## Alternatives

- That `fn_00475FE1` is the new-game initialisation is inferred from its
  neighbouring stores into the other per-player totals; its caller was not
  followed.
- That `fn_00449DD3` and `fn_00449E26` only convert the byte order of each
  entry for the network packet was not checked.

## How to reproduce

List the references to `0x004AB620..0x004AB637`. The only write that adds is
`INC dword ptr [EAX*0x4 + 0x4AB620]` at `0x00474889`, inside `fn_00472775`.
Read backwards from it to the store of 100 at `0x0047487B`, the comparison with
1 at `0x0047485A` and the Force store at `0x00474827`; the loop head at
`0x0047476F` scans players 0 to 5 and roster slots 0 to 80. The police damage
reaches the same local array at `0x00474202`.
