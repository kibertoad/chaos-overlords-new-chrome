---
id: RULE-RESEARCH-001
title: Each Research gang rolls Force plus Research and takes its successes off the item's remaining research at once
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-RESEARCH-001, FND-RESEARCH-002, FND-RESEARCH-003, FND-RESEARCH-004, FND-TURN-007, FND-STATE-002, FND-AI-007, FND-GANG-001, FND-TURN-001, FND-TURN-004, FND-EVENT-001, FND-EXE-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-001]
---

## Summary

A researching gang rolls dice equal to its Force plus its Research, and only
a 6 counts, or a 5 or 6 for the hardest computer players. Each success takes
one point off the research the item still needs. At 0 the item is researched
for that player, and any other gang of the player still due to research it
this turn rolls nothing.

## When it runs

During `instant_phase`, for each gang whose `action` is `ACTION_RESEARCH`, at
that gang's place in the phase's player and roster order.

## Parameters

- `player`: the player slot that owns the gang.
- `gang`: the acting gang, FMT-STATE-001.

## Inputs

The gang's `target`, `force` and `research` (the effective value rebuilt at
`turn_start`); `research_remaining` for that item and player;
`difficulty_band[player]`; and the state of `rng` through `roll`.

## Procedure

```text
let i = gang.target * 6 + player
if research_remaining[i] != 0:
    let pool = gang.force + gang.research
    let threshold = 6
    if difficulty_band[player] == 0:
        pool = pool - pool / 5
    else if difficulty_band[player] == 2:
        threshold = 5
    let successes = 0
    for d in 0..pool:
        if roll(6) >= threshold:
            successes = successes + 1
    let left = research_remaining[i] - successes
    if left < 0:
        left = 0
    research_remaining[i] = left
    if left == 0:
        emit ResearchCompleted(player, gang.target)
```

## Outputs

No return value. Lowers the player's `research_remaining` for the item by the
gang's successes, to no less than 0, and emits `ResearchCompleted` when it
reaches 0. Makes one `roll(6)`, three draws from `rng`, for each die of the
pool, and none when the item was already researched.

## Edge cases

- An item brought to 0 by an earlier gang in the same phase is skipped by
  every later gang of the same player, which makes no draws.
- For a band-0 player the pool loses a fifth, rounded toward zero.
- Research is kept per player: one player's research never changes another's.
- A recurring Research order is cleared at `turn_start` once the item is
  researched (FND-TURN-004).
- Site bonuses from a site completed earlier in the same phase do not apply
  until the next `turn_start` (RULE-SITE-001).

## What the sources say

SRC-MANUAL-GOG, page 33, describes Research as working toward an item the gang
cannot yet equip, with the number shown beside an item being the research it
still needs and the item researched at zero; it says researching the high-tech
items needs influenced Science Centers or Research Labs in controlled sectors.
Page 49 gives the roll as Force plus the Research skill, research points
accumulating toward one item, and success only on a 6. Pages 36 and 37 say a
gang can research only items of its own Tech Level or lower, and no higher
than Tech Level 5 without a controlled Science Center or Research Lab. The
executable agrees with the roll, and for band 2 also counts a 5. The Tech
Level limits are not part of this resolution step.

## Differences between builds

None known.

## Open questions

- The case itself applies no Tech Level limit; the limits are in the Research
  picker's list (FND-RESEARCH-003, SCR-RESEARCH-001), which offers only items
  the gang may research. The picker writes the item number into `target`, and
  the case reads it from there (FND-RESEARCH-004, FND-TURN-007).
- The report itself is RULE-EVENT-007, the handler of `ResearchCompleted`.
