---
id: RULE-TIMER-001
title: Planning time limit chosen for a match
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001, FND-TIMER-003, FND-OPTIONS-001, FND-UI-003, FND-EXE-004, SRC-MANUAL-GOG, SRC-HELP-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

At setup a player can limit each human planning turn to 30 seconds, 2 minutes or
5 minutes, or leave it unlimited, which is the default. The choice becomes a
limit in milliseconds each time a match is entered, new or loaded.

## When it runs

At each entry into a match, new or loaded, before the first turn played.

## Parameters

None.

## Inputs

`planning_limit_choice`.

## Procedure

```text
let limits: INT32[4] = [-1, 30000, 120000, 300000]
if planning_limit_choice >= 0 and planning_limit_choice <= 3:
    planning_limit_ms = limits[planning_limit_choice]
```

## Outputs

No return value. Sets `planning_limit_ms`: -1 for no limit, otherwise the limit
in milliseconds.

## Edge cases

- `planning_limit_choice` comes from the registry (RULE-OPTIONS-001), from the
  setup screen or from a loaded save. A value outside 0 to 3 leaves the limit
  as it was: 0 in a fresh session, which ends every timed turn at its first
  test, or the limit of the match entered before.
- A loaded game takes its limit from the choice its save carries.

## What the sources say

SRC-MANUAL-GOG, page 15 (Game Settings Panel), says games normally have no
limit on turns, that limits from 30 seconds to 5 minutes can be set, and that
they help multi-player games where some players take a long time. SRC-HELP-GOG,
Game Settings Panel topic, lists None, 30 seconds, 2 minutes and 5 minutes. Both
agree with the executable.

## Differences between builds

None known.

## Open questions

None.
