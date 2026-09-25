---
id: RULE-AUDIO-009
title: The sound of an attack in Detailed Combat
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-013, FND-AUDIO-002, FND-AUDIO-006]
conflicting: []
split_with: []
related: [RULE-AUDIO-005, FMT-AUDIO-001]
---

## Summary

Each attack Detailed Combat shows makes one sound as its clip starts: the
attacker's weapon's sound, a bare-handed or Martial Arts sound when it has no
weapon, or the police sound.

## When it runs

In Detailed Combat, once for each attack it presents, before the attack's clip
starts.

## Parameters

- `police` (`INT32`): nonzero when the attacker is the police.
- `evaded` (`INT32`): nonzero when the target evaded the attack.
- `weapon_sound` (`INT32`): the sound number in the `ITEMS` record of the
  attacker's equipped weapon, or -1 when it has none.
- `base_martial_arts` (`INT32`): the Martial Arts of the attacking gang's
  definition in `DATA/Gangs`.

## Inputs

`effect_slots`, `effects_enabled` through `play_effect`.

## Procedure

```text
if police != 0:
    effect_slots[5] = resource("DATA/Snd00518")
else if evaded != 0:
    # the sound number -1 names file number 499, which the build does not
    # ship, so the slot stays empty and the play below is silent
    effect_slots[5] = 0
else if weapon_sound >= 0:
    effect_slots[5] = resource(sprintf("DATA/SND%05d", 500 + weapon_sound))
else if base_martial_arts > 0:
    effect_slots[5] = resource("DATA/SND00501")
else:
    effect_slots[5] = resource("DATA/SND00500")
# the timeline plays the slot just before it advances the first frame
play_effect(5)
# the clip plays here; the slot is emptied after the attack's sequence
effect_slots[5] = 0
```

## Outputs

No return value. Loads slot 5, plays it once through `play_effect`, and
empties it again. Retaliation has no sound of its own.

## Edge cases

A police pass that finds no gang is not presented, so it makes no sound.

## What the sources say

None of the sources describes the combat sounds.

## Differences between builds

None known.

## Open questions

- Whether an armed attack whose item's sound number is out of the range of the
  files can occur.
