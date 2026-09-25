---
id: RULE-TIMER-001
title: Planning time limit chosen for a match
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001, FND-OPTIONS-001, FND-UI-003, SRC-MANUAL-GOG, SRC-HELP-GOG]
conflicting: []
split_with: []
related: []
---

## Summary

At setup a player can limit each human planning turn to 30 seconds, 2 minutes or
5 minutes, or leave it unlimited, which is the default. The choice becomes a
limit in milliseconds when the match starts.

## When it runs

During new-match initialization, before the first turn.

## Parameters

None.

## Inputs

`planning_limit_choice`.

## Procedure

```text
let limits: INT32[4] = [-1, 30000, 120000, 300000]
planning_limit_ms = limits[planning_limit_choice]
```

## Outputs

No return value. Sets `planning_limit_ms`: -1 for no limit, otherwise the limit
in milliseconds.

## Edge cases

`planning_limit_choice` comes from the registry (RULE-OPTIONS-001) or from the
setup screen, so a registry value outside 0 to 3 could reach this rule. What
the original reads for it is not known.

## What the sources say

SRC-MANUAL-GOG, page 15 (Game Settings Panel), says games normally have no
limit on turns, that limits from 30 seconds to 5 minutes can be set, and that
they help multi-player games where some players take a long time. SRC-HELP-GOG,
Game Settings Panel topic, lists None, 30 seconds, 2 minutes and 5 minutes. Both
agree with the executable.

## Differences between builds

None known.

## Open questions

- Whether the mapping is a table or a chain of compares, and what a choice
  outside 0 to 3 gives.
- Where `planning_limit_ms` is stored.
- Whether a loaded game keeps the limit it was saved with; the save carries the
  choice byte (FND-PLATFORM-003), but the mapping on load has not been read.
