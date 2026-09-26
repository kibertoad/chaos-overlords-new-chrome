---
id: FND-SETUP-015
title: The fresh-match initializer draws a reaction for every slot, then builds the city, then scans each name against six modifier strings in one pass, in local games only
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10..0x0046E765
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449CDE..0x00449D3A
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00449C41..0x00449C8D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B9C..0x00487BDF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046EB4E..0x0046EC83
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00487B98
tool: Ghidra 12.1.3
environment: null
---

## Observation

Match entry. The outer match loop `0x0046E766` takes one byte argument. With 1
it starts a new match (`0x0046EB56` to `0x0046EC83`): it sets `0x0049CA68` and
`0x004ABBD4` to 0 and, for each slot, the score dword `0x004A2790 + 4 * slot`
to 0, the byte `0x004ABBE0 + slot` to 1, and the cash dword `0x004A25E8 + 4 *
slot` to 500 when the scenario `0x004ABBE8` is 9 and to 20 otherwise; it also
fills three bytes per slot at `0x004ABBC0` with `0x9C` and at `0x004A27C8`
with `0xFF`, and clears the first byte of 32 ten-byte records per slot at
`0x004AAE08`. It then calls `0x0046DC10`, `0x0040AA65`, the score builder
`0x0047712A` and `0x0046D22F`, and last sets the cash of every slot whose byte
at `0x0049CA70` is set to 1,500 (`0x0046EC83`). With 0 it calls only
`0x0040AAE3` and `0x0046D22F`.

The local new-game path of the title menu (`0x00461763`, command `0x81`/1)
calls the full local setup and then the loop with 1. The byte `0x00487B98` is
0 in the executable's data and is set to 1 only on the paths that enter the
loop with 0 after a saved game or a network session is loaded
(`0x00461921`, `0x00461937`, `0x0046194D`, `0x00461BA5`, `0x00462030`,
`0x004621F8`). The human planning entry `0x0046FD80` calls the Game Information
presenter `0x0045519D` while it is set (`0x00470264`), and the loop clears it
at `0x0046F67E`, after the round's walk over the six slots and before the
resolution call.

Fresh-match initializer `0x0046DC10`, in order:

1. When the byte `0x00487850` is 3, for each observer slot: the byte
   `0x004A2600 + observer` is set to 1, the dword `0x004AB590 + 24 * observer
   + 4 * other` to -10 when slot `other` has type 0 or 3 at `0x004AB638` and to
   10 otherwise, and the dword `0x004AB650 + 4 * observer` to 0. Otherwise, for
   each slot: `0x004A2600` gets 0, the six attitude dwords 0, and
   `0x004AB650 + 4 * slot` the bounded draw `0x0045D227(4)` plus 2
   (`0x0046DC83`). This draw is made for all six slots in slot order, whatever
   their type.
2. The dword `0x004A2570 + 4 * slot` is set to 1 for every slot, then to 0 for
   each type-1 slot when `0x00487850` is 0, and to 2 for each type-1 slot when
   it is 2 or 3.
3. The location byte of all 81 gang slots of each player is set to 100.
4. The per-player research table `0x004A2608` (`item * 6 + player`, 64 items)
   is set to 0 in scenario 9 and otherwise to each item's value at
   `0x004A5F84 + 0xA6 * item`.
5. `0x004A8888` and `0x004A8918` are cleared for 64 sectors and six players.
6. The city generators `0x00475FE1` and `0x00476726` run.
7. Slot 0 of each player's gangs is written as the Right Hands, and the byte
   `0x004ABBF0 + 4 * player` takes that gang's location.
8. Only when both network bytes `0x00487B58` and `0x00482178` are 0: for each
   player in slot order, six flag bytes are cleared (`0x004ABBD8`,
   `0x004AB588`, `0x004A5EF0`, `0x004A2788`, `0x004ABC10`, `0x0049CA70`, one
   byte per slot each), and the player's name at `0x004A2588 + 12 * player` is
   compared with the six strings at `0x00487B9C`, `0x00487BA8`, `0x00487BB4`,
   `0x00487BC0`, `0x00487BCC` and `0x00487BD8`, in that order; a match sets
   the flag of the same position. After this scan, a second pass over the
   players applies the first flag (five more gangs in slots 1 to 5 copied from
   the Right Hands' location, definition 0 and no equipment), then the fourth
   (slots 1 to 5 as definition 59 with three items), then the fifth (every
   sector whose owner byte is -1 gets 100 at offset `0x0F`).

Name comparison. Each string in the executable is stored as a placeholder
byte (a space) followed by upper-case ASCII and a NUL. Before each compare the
initializer passes the string through `0x00449C41`, which counts the
characters after the first byte up to the NUL and writes that count over the
placeholder, turning the string in place into a length-prefixed one.
`0x00449CDE(name, string)` then compares the bytes at positions 0 to
`string[0]` of both, the length byte first, without stopping at a difference,
and returns 1 only when every byte matched. The comparison is exact and case
sensitive, and the name's bytes after its own length are compared as well
when the lengths match.

## Interpretation

The six strings are, in order, `modifier_name_right_hands`,
`modifier_name_visibility` (whose flag is `modifier_visibility`,
FND-SETUP-011), the string of `hire_force_modifier` (FND-HIRE-005),
`modifier_name_elite`, `modifier_name_islands` (FND-SETUP-003) and
`modifier_name_cash` (FND-SETUP-001). The
scan is one pass that sets all six flags for a player before the next player;
the gang and sector effects follow in a second pass, and the cash effect only
after the initializer returns. None of the modifiers applies in a network
game, and none of them is re-evaluated when a saved game is resumed.

`reaction` is the dword array at `0x004AB650`. Every slot, computer or human or
empty, gets a draw unless the mentality is Homicidal Maniac, and those six
draws come before the city generators' draws.

`0x00487B98` marks a resumed match: the Game Information panel opens once for
each local human who plans in the first round after a load, and never at the
start of a new local game.

## Alternatives

The strings' first byte is overwritten on the first compare and stays
overwritten; a second match started in the same session compares against the
same length-prefixed strings, so the result does not change.

What `0x0040AA65`, `0x0040AAE3` and `0x0046D22F` do is not recorded here.

## How to reproduce

In Ghidra, open `0x0046E766` and follow the branch on its argument at
`0x0046EB4E`. List the references to `0x00487B98`. Open `0x0046DC10` and read
it from the mentality test at `0x0046DC20` to the second pass that ends at
`0x0046E74A`; read the strings at `0x00487B9C` to `0x00487BDF`, and open
`0x00449C41` and `0x00449CDE`.
