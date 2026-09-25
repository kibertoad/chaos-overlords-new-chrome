---
id: RULE-SEARCH-001
title: Each player's Search filter starts empty and is changed by ALL, NONE and its rows
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SEARCH-001, FND-SEARCH-002]
conflicting: []
split_with: []
related: []
---

## Summary

Every player has their own list of the 22 site types to look for on the city
map. It starts empty in a new game. In the Search panel, ALL selects every
type, NONE clears them, and clicking a row switches that one type on or off.

## When it runs

The procedure's last loop runs once, when a new game is set up. The functions
run in the Search panel: `search_set_all(1)` for ALL, `search_set_all(0)` for
NONE and `search_toggle(n)` for a single click on row `n`.

## Parameters

None.

## Inputs

`active_player`, `search_filters`.

## Procedure

```text
define search_set_all(value: UINT8):
    for d in 0..22:
        search_filters[active_player * 22 + d] = value
    return

define search_toggle(definition):
    let i = active_player * 22 + definition
    search_filters[i] = search_filters[i] == 0
    return

# new game
for i in 0..132:
    search_filters[i] = 0
```

## Outputs

No return value. Changes `search_filters` for the active player, or clears the
whole table at a new game. The city's site markers change accordingly
(RULE-SEARCH-002).

## Edge cases

Each player's filter is separate, so in a hot-seat game each player sees the
markers they chose.

## What the sources say

SRC-MANUAL-GOG does not describe the Search panel.

## Differences between builds

None known.

## Open questions

- Whether `search_filters` is written to the save file, and so survives saving
  and loading, is not recorded.
- Whether the flip stores exactly 1 and 0 is not recorded; the finding says
  only that a click toggles the byte.
