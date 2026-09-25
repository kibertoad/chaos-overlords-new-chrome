---
id: RULE-SEARCH-001
title: Each player's Search filter starts empty and is changed by ALL, NONE and its rows
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-SEARCH-001, FND-SEARCH-002, FND-SEARCH-004, FND-SEARCH-005, FND-COMLINK-006]
conflicting: []
split_with: []
related: []
---

## Summary

Every player has their own list of the 22 site types to look for on the city
map. It starts empty in a new game. In the Search panel, ALL selects every
type, NONE clears them, and clicking a row switches that one type on or off.

## When it runs

The procedure's last loop runs each time the outer match function starts,
before anything else it does [FND-COMLINK-006]. The functions run in the
Search panel: `search_set_all(1)` when ALL is released inside itself,
`search_set_all(0)` when NONE is, and `search_toggle(n)` when row `n` is
pressed, including the first press of a double-click [FND-SEARCH-004].

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

# the match loop starts
for i in 0..132:
    search_filters[i] = 0
```

## Outputs

No return value. Changes `search_filters` for the active player, or clears the
whole table at a new game. The city's site markers change accordingly
(RULE-SEARCH-002).

## Edge cases

- Each player's filter is separate, so in a hot-seat game each player sees the
  markers they chose.
- The filter is not saved: a save file has no block for it [FND-SEARCH-004].
  The match function empties it on entry before it chooses between a new
  match and the state already loaded, and the shell enters every match, new
  or loaded, through it, so a loaded match starts with every filter empty
  [FND-SEARCH-005].
- A double-click on a row flips it once, on its first press, and opens Site
  Information without flipping it back.

## What the sources say

SRC-MANUAL-GOG does not describe the Search panel.

## Differences between builds

None known.

## Open questions

None known.
