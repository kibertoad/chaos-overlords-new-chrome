---
id: RULE-AUDIO-004
title: Loading the general sound effects
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002]
conflicting: []
split_with: []
related: [FMT-AUDIO-001]
---

## Summary

At startup the game loads its nine general sound effects into the sound slots
0 to 4 and 6 to 9. Slot 5 stays empty for the attack sounds of Detailed Combat.

## When it runs

Once, in the title initialization, before the title screen is first shown.

## Parameters

None.

## Inputs

None.

## Procedure

```text
for slot in 0..5:
    effect_slots[slot] = resource(sprintf("DATA/SND%05d", 200 + slot))
for slot in 6..10:
    effect_slots[slot] = resource(sprintf("DATA/SND%05d", 199 + slot))
```

## Outputs

No return value. Fills `effect_slots` 0 to 4 with `DATA/SND00200` to
`DATA/SND00204` and 6 to 9 with `DATA/Snd00205` to `DATA/Snd00208` (the file
names differ in case only). The roles of the slots are:

| Slot | File | Played when |
|---|---|---|
| 0 | `DATA/SND00200` | A panel slides in (RULE-UI-003) |
| 1 | `DATA/SND00201` | A panel slides out (RULE-UI-003) |
| 2 | `DATA/SND00202` | A push-button control is pressed (RULE-UI-001) |
| 3 | `DATA/SND00203` | A choice or confirmation is accepted |
| 4 | `DATA/SND00204` | A choice or step is refused |
| 5 | none | Detailed Combat attack sounds (RULE-AUDIO-009) |
| 6 | `DATA/Snd00205` | The Comlink alert (RULE-AUDIO-007, RULE-AUDIO-008) |
| 7 | `DATA/Snd00206` | The planning clock's last ten seconds (RULE-TIMER-003) |
| 8 | `DATA/Snd00207` | The planning clock's last second (RULE-TIMER-003) |
| 9 | `DATA/Snd00208` | The start of a turn (RULE-AUDIO-006) |

## Edge cases

None known.

## What the sources say

None of the sources lists the sound effects.

## Differences between builds

None known.

## Open questions

- What the loader does when a file is missing or cannot be read.
- `effect_slots` has no recorded address.
