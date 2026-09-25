---
id: RULE-COMBAT-004
title: Detailed Combat plays the viewer's fights sector by sector, one clip per attack
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-004, FND-COMBAT-005, FND-UI-001]
conflicting: []
split_with: []
related: [SCR-COMBAT-002, FMT-STATE-001, FMT-STATE-003]
---

## Summary

Detailed Combat shows only fights that involve the viewer's gangs. It goes
through the sectors in order and, in each, through the viewer's gangs that
fought. For each such gang it shows the gang's own attack, then the attacks
on it, then the police. A retaliation has no clip of its own: it shows as the
attacker's bar dropping during the attacker's own clip.

## When it runs

When the Detailed Combat presentation opens for a viewing player, from the
main console's combat route [FND-AUDIO-002] or at the start of the viewer's
planning.

## Parameters

- `viewer`, the viewing player's slot.

## Inputs

`combat_results` (the viewer's entries in each sector's row), `combat_records`,
and each listed gang's `sector`, `action`, `target` and `target_2`.

## Procedure

```text
show SCR-COMBAT-002
for s in 0..64:
    for k in 0..6:
        let e = combat_results[s].entries[viewer * 6 + k]
        if e.roster_slot < 0:
            continue
        let focal_index = viewer * 81 + e.roster_slot
        let focal = gangs[focal_index]
        let focal_rec = combat_records[focal_index]

        # The list: the focal gang's target, then the gangs that attacked it
        let listed: INT32[] = []
        let target_index = -1
        if focal.action == ACTION_ATTACK:
            target_index = focal.target * 81 + focal.target_2
            append(listed, target_index)
        for p in 0..6:
            for slot in 0..81:
                let j = p * 81 + slot
                let other = gangs[j]
                if j != focal_index and j != target_index and other.sector == s and other.action == ACTION_ATTACK and other.target * 81 + other.target_2 == focal_index:
                    append(listed, j)

        focal_rec.force_shown = focal_rec.force_start
        for each j in listed:
            combat_records[j].force_shown = combat_records[j].force_start

        for each j in listed:
            let other = gangs[j]
            let other_rec = combat_records[j]
            let is_target = j == target_index
            let attacks_focal = other.action == ACTION_ATTACK and other.target * 81 + other.target_2 == focal_index
            if is_target:
                other_rec.force_shown = other_rec.force_shown - focal_rec.damage_dealt
                focal_rec.force_shown = focal_rec.force_shown - focal_rec.retaliation_taken
                # a pair attacking each other: no hold, the next clip follows at once
                emit CombatClip(focal_index, j, false, not attacks_focal)
            if attacks_focal:
                focal_rec.force_shown = focal_rec.force_shown - other_rec.damage_dealt
                other_rec.force_shown = other_rec.force_shown - other_rec.retaliation_taken
                emit CombatClip(focal_index, j, true, true)

        if focal_rec.police_damage != -1:
            focal_rec.force_shown = focal_rec.force_shown - focal_rec.police_damage
            emit CombatClip(focal_index, -2, true, true)
```

## Outputs

No return value. Sets `force_shown` in the records of the gangs it shows, and
emits `CombatClip` for each clip in the order they play. Makes no draws.

## Edge cases

- A fight between two other players is never shown, even in a sector where
  the viewer has gangs.
- A gang the viewer owns that both attacks and is attacked back by its target
  plays two clips back to back: its own attack, released at tick 16, and then
  the target's attack in the mirrored strips [FND-COMBAT-005].
- A bar only ever shows damage from the clips played so far.

## What the sources say

None known. The manual describes the Detailed Combat option only as showing
combat in detail.

## Differences between builds

None known.

## Open questions

- Whether "every other gang in the sector whose target is the focal gang" also
  requires that gang's action to be Attack, as written, and whether the target
  is listed when the focal gang's action is not Attack.
- What the presenter subtracts when `damage_dealt` is -1 (an evaded attack);
  the clip then uses the evasion strips (SCR-COMBAT-002).
- Whether displayed Force is reset once for the whole presentation or once per
  focal gang, as written. A gang shown in two focal lists would show different
  bars.
- The layout of a `combat_results` entry (see RULE-COMBAT-002).
- Which of `target` and `target_2` holds the target's player (FMT-STATE-001).
- The exact trigger of the automatic presentation (the Detailed Combat option,
  and its place in the planning entry) is not recorded in these findings.
