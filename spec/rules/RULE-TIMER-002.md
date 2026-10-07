---
id: RULE-TIMER-002
title: A human planning turn ends when its time limit passes
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-TIMER-001, FND-TIMER-003, FND-OPTIONS-002, FND-STATE-010, FND-EXE-004, FND-UI-044, EXP-TURN-046, EXP-TURN-047, EXP-TURN-052, EXP-TURN-102, EXP-UI-035, FND-RNG-001]
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

`controller`, `planning_limit_ms`, `planning_start_ms`, `planning_timed`, `timer_ms`.

## Procedure

```text
define planning_timer_start():
    planning_start_ms = timer_ms
    planning_timed = planning_limit_ms != -1
    # the clock bar is drawn at once (RULE-TIMER-003)

define planning_time_expired() -> INT32:
    if planning_timed == 0:
        return 0
    # 32-bit difference, compared as signed values
    let elapsed: INT32 = timer_ms - planning_start_ms
    return elapsed > planning_limit_ms

# at the start of a human player's planning
planning_timer_start()
# on each pass of the human planning loop, after Done and RULE-OPTIONS-003;
# a pass starts only once the handler of the previous event has returned, so
# not while a panel is open or an offer, a control or a gang card's portrait
# is held under the left button (FND-UI-044)
if planning_time_expired():
    # the loop ends and the turn goes on as if Done had been accepted
    return
```

## Outputs

`planning_time_expired` returns 1 once the limit has passed. When it does, the
player's planning ends; RULE-OPTIONS-003 does not run for that ending.

## Edge cases

- The test is strictly greater: the turn ends on the first pass after the limit.
- With no limit, `planning_timed` is 0 and the test returns 0 without comparing.
- The clock is not started in the planning passes that follow the end of a
  match, while `no_match_in_play` is set (FND-STATE-010); `planning_timed`
  stays 0 from the end of the previous planning, so those passes never
  expire.
- The difference is taken in 32 bits, so `timer_ms` wrapping during a turn
  gives the right elapsed time.
- The test runs only in the planning loop itself. A panel open when the time
  passes keeps running, with the bar and warning sounds of RULE-TIMER-003
  going on, and the turn ends on the loop's next pass after the panel closes.
- A left press on the portrait of one of the player's gang cards in the
  detailed sector view holds the game in the command handler's own loops until
  the left button comes up, whether or not the gang is dragged (FND-UI-044).
  A turn whose time passes during the hold ends on the loop's next pass after
  the release, after the order of the drop has been given. Those loops do not
  call the event pump, so the bar is not redrawn and the warnings of
  RULE-TIMER-003 do not sound while the gang is held.
- The menu bar runs a loop of its own while it is open, so the planning loop
  does not test the limit then, but `timer_ms` runs on. A turn whose limit
  passes while the menu bar is open ends on the loop's first pass after it
  closes (EXP-TURN-102).
- When planning ends the flag is cleared and the bar is left as last drawn. The
  next planning entry redraws the console before the clock starts, which puts
  back the full bar of the console's art: the panels that entry opens, and the
  next human's in hot seat, show that bar and not the one the turn before left
  (EXP-UI-035).

## What the sources say

SRC-MANUAL-GOG, page 15, says the limit helps multi-player games; it does not
say what happens when time runs out.

## Differences between builds

None known.

## Open questions

- No recorded run lets the time pass while a gang card's portrait is held. The
  expiry after the release rests on the static reading of FND-UI-044 alone.
