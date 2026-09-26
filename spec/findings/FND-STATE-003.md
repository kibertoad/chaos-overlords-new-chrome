---
id: FND-STATE-003
title: Save blocks 16, 17, 18, 21, 36 and 37 hold the AI hire limit, an always-zero per-gang table, the takeover flag, an unused hire flag, the reactions and a Homicidal Maniac flag
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482110..0x00482127
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0048DB48..0x0048E2DF
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482158..0x0048215D
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00482140..0x00482157
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB650..0x004AB667
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2600..0x004A2605
tool: Ghidra 12.1.3
environment: null
---

## Observation

FND-SAVE-001 lists these six blocks without a meaning. The load function
`fn_0046381A` and the save function `fn_00463CC5` push each block's address once;
every other reference to each range is below.

Block 16, `INT32LE[6]` at `0x00482110`, element `player`. `fn_0040AA65` writes
81 for every player when a match is created (`0x0040AAAA`). The planning pass
`fn_00458FA0` writes it once per call for the player it plans: at `0x004595BC`,
`0x00459597`, `0x00459607`, `0x00459638` and `0x0045965B` with the four results
FND-AI-012 gives for the hire limit, and then caps it at 80 (`0x00459665`,
`0x00459676`). Each case of the scenario switch that follows compares the
player's count of active gangs at `0x0048E2E0` (FND-AI-044) with it, at
`0x004596C1`, `0x00459C11`, `0x0045A0D5`, `0x0045A5A5`, `0x0045AB47`,
`0x0045AFC7`, `0x0045B447`, `0x0045B6BA` and `0x0045BC6C`, and goes on to hire
only when the count is at most the value.

Block 17, `INT32LE[486]` at `0x0048DB48`, element `player * 81 + slot`. The one
store writes 0 (`0x00459323`); the one reader is selector `0x5D` of
`fn_00402D70` (`0x00405C7B`, `0x00405CB7`). FND-AI-041 records both.

Block 18, `UINT8[6]` at `0x00482158`, element `player`. `fn_0040AA65` writes 0
(`0x0040AA94`), `fn_0040AB20` writes 1 (`0x0040AB36`), and `fn_00458FA0` reads it
at `0x004594AF` and makes every gang of the player a family-9 gang when it is
set. The store at `0x00459467` misses this array: FND-AI-043 records all four
and the misdirected store.

Block 21, `INT32LE[6]` at `0x00482140`, element `player`. The 54 other
references are stores by `fn_00458FA0`, of 1 at `0x0045A43B`, `0x0045A9A7` and
`0x0045A9D9` and of 0 at the rest, each next to a store of the hire role at
`0x00482128`. No instruction reads it (FND-AI-042).

Block 36, `INT32LE[6]` at `0x004AB650`, element `player`. The fresh-match
initializer `fn_0046DC10` writes it at `0x0046DC91` and `0x0046DD3B` (0 for
every player under Homicidal Maniac, a draw of 3 to 6 otherwise,
FND-SETUP-015). The resolver `fn_00472775` reads it at `0x00473F03`, in the
attitude drop after an attack, and at `0x00475762`, in the drop after a Control
takeover (FND-AI-006).

Block 37, `UINT8[6]` at `0x004A2600`, element `player`. `fn_0046DC10` writes 1
for every player under Homicidal Maniac (`0x0046DCBE`) and 0 for every player
otherwise (`0x0046DC45`). No instruction reads it.

## Interpretation

Block 16 is the computer player's hire limit (RULE-AI-011's `hire_limit`),
kept per player between planning passes. Every pass recomputes it before the
first comparison, so the 81 written at a new match and the value a save
restores are never used.

Block 17 is zero in every match that starts from a new game, and a save of such
a match stores zeros.

Block 18 is the takeover flag that turns a network player's gangs into raiders
once a computer player replaces them.

Block 21 is written and saved but has no effect.

Block 36 is the glossary's `reaction`.

Block 37 records that the match was set up under Homicidal Maniac; the game
never consults it after setup.

## Alternatives

A reader through a computed pointer would not appear in the reference lists.
None of the six arrays is passed by address anywhere other than the two save
routines, and every other reference uses the array base with an index
register, so a hidden reader would need a pointer built from an unrelated
base.

## How to reproduce

List the references to each of the six ranges. Match the pushes in
`fn_0046381A` and `fn_00463CC5` with the block order in FND-SAVE-001, then read
each remaining reference in its function.
