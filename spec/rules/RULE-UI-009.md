---
id: RULE-UI-009
title: The texts of the Game Information panel
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-UI-003, FND-UI-024, FND-TURN-003, FND-SETUP-002, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

Game Information shows the scenario, with its length in parentheses for the four
timed scenarios, the computer players' Mentality, the planning time limit, and
for each of the six player slots its name and whether it is a human, a computer
or eliminated.

## When it runs

When the Game Information panel (SCR-UI-008) is drawn.

## Parameters

None.

## Inputs

`scenario`, `turn_limit`, `mentality`, `planning_limit_choice`,
`player_active`, `players_human`.

## Procedure

```text
define game_info_scenario_text():
    let scenario_name = resource("Chaos Overlords.exe", "STRING", scenario + 1)
    if scenario >= 0 and scenario <= 3:
        let lengths: INT32[4] = [26, 52, 104, 208]
        # a length outside the four leaves the name in the suffix buffer
        let suffix = scenario_name
        for k in 0..4:
            if turn_limit == lengths[k]:
                suffix = resource("Chaos Overlords.exe", "STRING", 0x36 + k)
        return sprintf("%s (%s)", scenario_name, suffix)
    return scenario_name

define game_info_mentality_text():
    return resource("Chaos Overlords.exe", "STRING", 0x2E + mentality)

define game_info_limit_text():
    return resource("Chaos Overlords.exe", "STRING", 0x32 + planning_limit_choice)

define player_status_text(slot):
    if player_active[slot] == 0:
        return resource("Chaos Overlords.exe", "STRING", 0x3C)
    if players_human[slot] == 0:
        return resource("Chaos Overlords.exe", "STRING", 0x3B)
    return resource("Chaos Overlords.exe", "STRING", 0x3A)
```

## Outputs

Each function returns the text for one field of SCR-UI-008. The strings
`Chaos Overlords.exe#STRING/0x36` to `0x39` name the four scenario lengths,
`0x2E` to `0x31` the four Mentalities, `0x32` to `0x35` the four time-limit
choices, and `0x3A`, `0x3B` and `0x3C` the human, computer and eliminated
labels.

## Edge cases

- An eliminated player is labelled eliminated whether it was a human or a
  computer; the test reads `player_active`.
- A network human has `players_human` set and is labelled human. A player
  handed to the computer during a network game is labelled computer from then
  on.
- A timed scenario whose `turn_limit` is none of the four lengths shows its name
  twice, the second time in the parentheses. Scenarios 4 and above show the
  name alone.
- Every one of the six slots has a row, since every slot not configured at setup
  becomes a computer player (FND-SETUP-002).

## What the sources say

SRC-MANUAL-GOG, page 18 (Game Info Button), says the panel lists the scenario
type, the difficulty level of the computer players and each player, human or
computer, and that it opens by itself at the start of a multi-player game and
when a saved game is opened. It does not mention the time limit, which the
executable shows.

## Differences between builds

None known.

## Open questions

None.
