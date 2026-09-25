---
id: RULE-TIMER-002
title: A human planning turn ends when its time limit passes
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001, FND-OPTIONS-002]
conflicting: []
split_with: []
related: [RULE-OPTIONS-003]
---

## Summary

When a human player starts planning, the game notes the time. Once more time
has passed than the limit allows, the turn ends as if the player had pressed
Done, without asking about idle gangs. Computer players are not timed.

## When it runs

At the start of each human player's planning, and then on each pass of the human
planning input loop, after the loop has handled Done and the idle-gang warning.

## Parameters

None.

## Inputs

`controller`, `planning_limit_ms`, `planning_start_ms`, `timer_ms`.

## Procedure

```text
define planning_timer_start():
    planning_start_ms = timer_ms

define planning_time_expired() -> INT32:
    let elapsed = timer_ms - planning_start_ms
    return elapsed > planning_limit_ms

# at the start of a human player's planning
planning_timer_start()
# on each pass of the human planning loop, after Done and RULE-OPTIONS-003
if planning_time_expired():
    # the loop ends and the turn goes on as if Done had been accepted
    return
```

## Outputs

`planning_time_expired` returns 1 once the limit has passed. When it does, the
player's planning ends; RULE-OPTIONS-003 does not run for that ending.

## Edge cases

- The test is strictly greater: the turn ends on the first pass after the limit.
- With no limit (`planning_limit_ms` of -1) the turn must never expire; how the
  helper arranges that is not recorded (see Open questions).

## What the sources say

SRC-MANUAL-GOG, page 15, says the limit helps multi-player games; it does not
say what happens when time runs out.

## Differences between builds

None known.

## Open questions

- Whether the expiry helper tests for -1 itself, or its caller skips it. A
  signed compare of `elapsed` with -1 would expire at once, and an unsigned one
  never.
- The types of `elapsed` and of `planning_limit_ms` in the compare.
- Whether a modal panel open at expiry is closed, or the expiry waits for it.
- What happens when `timer_ms` wraps during a turn.
- Where `planning_start_ms` is stored.
