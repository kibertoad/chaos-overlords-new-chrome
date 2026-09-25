---
id: FND-AI-006
title: A six-by-six attitude matrix starts from the Mentality, recovers each turn and drops after attacks and takeovers
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004AB590..0x004AB620
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00402D70
tool: Ghidra 12.1.3
environment: null
---

## Observation

`0x004AB590` is a six-by-six matrix of signed integers indexed
`observer * 6 + other player`.

New-match initialization `0x0046DC10` fills it from the Mentality. At 3
(Homicidal Maniac) every cell whose other player has controller type 0 or 3 is
set to -10, and every cell whose other player is a computer to +10. At 0 to 2
every cell starts at 0. The same function gives each player a reaction value:
at 0 to 2, one call of the bounded wrapper with bound 4, plus 2, for each of
the six players, at `0x0046DC83`; at 3, reaction 0 and no draw. These six draws
are the only random draws between entry to `0x0046DC10` and its call of the
city generator. Nothing later writes the reaction values.

At the start of turn resolution, at Mentality 0 to 2, every cell below +10
rises by 1. At 3 the whole loop is skipped. Later resolution paths lower one
cell and clamp it at -10: the combat path lowers the defender's owner's cell
toward the attacker by `max(reaction, damage dealt)`, and a change of a
sector's owner lowers the previous owner's cell toward the new owner by twice
the previous owner's reaction. The player-pair pass `0x0040A1A7` can also set
a cell to -10 (FND-AI-018).

A negative cell is the hostility test the target queries use. Selector `0x92`
lists the visible gangs in a sector only of players toward whom the observer's
cell is negative; selector `0xAB` counts them for visibility state 1. Selector
`0x90` gives weight 10 to a visible human gang of a negatively viewed player
and 1 to other visible gangs (FND-AI-013). Many handlers test `< 0` directly,
among them sector selector mode 6.

## Interpretation

The matrix is how much each player likes each other player, from -10 to +10.
Homicidal Maniac computers start as enemies of every human and friends of every
computer, and never calm down. At the other settings everyone starts neutral,
grudges come from attacks and lost sectors, and fade by one point a turn. The
reaction value sets how strongly a player bears a grudge. The matrix gives no
resources, statistics, rolls or visibility.

## Alternatives

Which of the resolution's player and gang loops holds the combat and takeover
decrements, and which player's reaction the combat decrement uses (the
defender's owner is assumed), is not given with instruction addresses. The name
the game uses for the reaction value is unknown. The address of the reaction
values is not recorded.

## How to reproduce

Find the references to `0x004AB590`. The initializer `0x0046DC10` compares the
Mentality at `0x00487850` with 3 and calls the wrapper `0x0045D227` with 4 at
`0x0046DC83`. The recovery loop is near the start of `0x00472775`; the clamps
compare with -10.
