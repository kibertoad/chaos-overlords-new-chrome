---
id: BUG-AI-006
title: An objective gang with nothing else to do picks its Influence site against a threshold the planner never sets
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-062]
conflicting: []
split_with: []
related: [RULE-AI-031]
---

## Symptom

A computer gang of family 13 or 14 that holds its objective sector, sees no
opponent there, is fit and has nothing to buy, is meant to Influence the site
with the most Support. Which site it picks, and whether it picks one at all,
does not follow from the game state.

## Trigger conditions

In Eliminate (scenario 6) or Big Man (scenario 8), a family-13 or family-14
gang stands on an objective sector its player owns. The sector's cached
opponent weight is 0, the gang fails the Heal test (Force 10 or more, or
effective Heal below -3), and the weapon, armor and miscellaneous Equip steps
all decline.

## Mechanism

The site scan keeps the slot whose Support is greater than the best value so
far. The best value lives in a stack local that only the handler's target-draw
loops write, and neither loop runs on this path. The comparison therefore
starts from whatever an earlier call left in that stack slot. A large leftover
value makes the scan choose no site, so the gang plans None; a negative one
lets a site with no positive Support win.

## Frequency

Every planning pass that meets the trigger conditions. The leftover value
depends on the calls made before the handler, which static reading cannot
determine.

## Player reliance

Unknown. The outcome is not visible as a rule a player could learn.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

- What the stack slot holds in practice, and whether it is the same on every
  call, needs a run of the original with a breakpoint on the scan's compare.
