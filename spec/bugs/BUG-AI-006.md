---
id: BUG-AI-006
title: An objective gang with nothing else to do picks its Influence site against a threshold the planner never sets
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-062, FND-AI-063]
conflicting: []
split_with: []
related: [RULE-AI-031]
---

## Symptom

A computer gang of family 13 or 14 that holds its objective sector, sees no
opponent there, is fit and has nothing to buy, is meant to Influence the site
with the most Support. The handler compares Support with a value it never
sets. In this build the value is always 0, so the gang picks the first
unfinished site with the highest positive Support, as it would with the
threshold set to 0, and the defect has no visible effect.

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
starts from whatever an earlier call left in that stack slot. On every path
into the handler the last call made at the same stack depth is the AI
selector, whose prologue writes 0 to the local at that address and whose cases
`0x48` and `0x7C` leave it alone (FND-AI-063). A different call sequence
before the handler, in another build or after a change to the dispatcher,
could leave a large value, making the scan choose no site, or a negative one,
letting a site with no positive Support win.

## Frequency

Every planning pass that meets the trigger conditions reads the unset value,
and in this build it is 0 every time (FND-AI-063).

## Player reliance

Unknown. With the value fixed at 0 the scan behaves as a set threshold of 0
would, so players see the intended choice.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

None.
