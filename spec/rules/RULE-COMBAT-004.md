---
id: RULE-COMBAT-004
title: Detailed Combat plays the viewer's fights sector by sector, one clip per attack
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-004, FND-COMBAT-005, FND-COMBAT-008, FND-COMBAT-010, FND-COMBAT-011, FND-COMBAT-013, FND-EXE-004, FND-UI-001]
conflicting: []
split_with: []
related: [SCR-COMBAT-002, RULE-COMBAT-002, RULE-AUDIO-005, FMT-STATE-001, FMT-STATE-003, FMT-STATE-008]
---

## Summary

Detailed Combat shows only fights that involve the viewer's gangs. It goes
through the sectors in order and, in each, through the viewer's gangs that
fought. For each such gang it shows the gang's own attack, then the attacks
on it, then the police, all taken from the sector rows the combat phase wrote.
A retaliation has no clip of its own: it shows as the attacker's bar dropping
during the attacker's own clip.

## When it runs

As `fn_0042E040(viewer, flag)` (range in FND-EXE-004). At the start of the
viewer's planning, `fn_0046FD80` calls it with `flag` 1 when the Detailed
Combat option byte `0x0048785C` is nonzero, and calls the Combat Results
handler (SCR-COMBAT-001) otherwise; the main console's combat control calls
one or the other with `flag` 0 [FND-COMBAT-010, FND-AUDIO-002].

## Parameters

- `viewer`, the viewing player's slot.
- `flag`, 1 when the presentation opens by itself at planning, 0 when the
  player asked for it.

## Inputs

`combat_results` (FMT-STATE-008) and `combat_records`. The gang records are
not read: who attacked whom comes from the rows [FND-COMBAT-010].

## Procedure

```text
let sectors_shown: INT32[] = []
for s in 0..64:
    if combat_results[s].entries[viewer * 6].gang != -1 or combat_results[s].police_hit[viewer] != 0:
        append(sectors_shown, s)

# Displayed Force is reset once, for every gang in any row
for s in 0..64:
    for n in 0..36:
        let g = combat_results[s].entries[n].gang
        if g != -1:
            combat_records[g].force_shown = combat_records[g].force_start

if sectors_shown == []:
    if flag == 0:
        play_effect(4)
    return

show SCR-COMBAT-002
# The player can end the presentation during any clip (Escape or the exit
# face); every remaining clip of every loop below is then skipped.
for each s in sectors_shown:
    for k in 0..6:
        let e = combat_results[s].entries[viewer * 6 + k]
        if e.gang == -1:
            continue
        let focal_index = e.gang
        let focal_rec = combat_records[focal_index]

        # The list: the focal gang's target, then the gangs that attacked it
        let listed: INT32[] = []
        let listed_target: INT32[] = []
        let target_index = e.target
        if target_index != -1:
            append(listed, target_index)
            let tt = -1
            for n in 0..6:
                let te = combat_results[s].entries[(target_index / 81) * 6 + n]
                if te.gang == target_index:
                    tt = te.target
            append(listed_target, tt)
        for n in 0..36:
            let a = combat_results[s].entries[n]
            if a.gang != -1 and a.target == focal_index and a.gang != target_index:
                append(listed, a.gang)
                append(listed_target, focal_index)
        if focal_rec.police_damage != -1:
            append(listed, -2)
            append(listed_target, focal_index)

        for each j, t in listed, listed_target:
            if j == -2:
                # the police are drawn with Force 10 on both tracks
                focal_rec.force_shown = max(0, focal_rec.force_shown - focal_rec.police_damage)
                emit CombatClip(focal_index, -2, true, true)
                continue
            let other_rec = combat_records[j]
            if j == target_index:
                if focal_rec.damage_dealt != -1:
                    other_rec.force_shown = max(0, other_rec.force_shown - focal_rec.damage_dealt)
                focal_rec.force_shown = max(0, focal_rec.force_shown - focal_rec.retaliation_taken)
                emit CombatClip(focal_index, j, false, t != focal_index)
            if t == focal_index:
                if other_rec.damage_dealt != -1:
                    focal_rec.force_shown = max(0, focal_rec.force_shown - other_rec.damage_dealt)
                other_rec.force_shown = max(0, other_rec.force_shown - other_rec.retaliation_taken)
                emit CombatClip(focal_index, j, true, true)
```

## Outputs

No return value. Sets `force_shown` in the record of every gang listed in any
row, lowers it for the gangs it shows, and emits `CombatClip` for each clip in
the order they play. Makes no draws.

The game keeps the procedure's locals in globals [FND-COMBAT-011]: `focal_index`
in `combat_focal`, `target_index` in `combat_focal_target`, `focal_rec` in
`combat_focal_record`; `listed`, `listed_target` and the records of the listed
gangs in `fight_list` (from element 1; element 0 is the focal gang),
`fight_list_targets` and `fight_list_records`, with the length in
`fight_list_count`; the current `j`, its target and `other_rec` in
`combat_other`, `combat_other_target` and `combat_other_record`; the two bars
in `combat_focal_bar` and `combat_other_bar`. `combat_presenting` is 1 while
the presentation runs, and ending it clears the byte.

## Edge cases

- A fight between two other players is never shown, even in a sector where
  the viewer has gangs.
- A gang the viewer owns that both attacks and is attacked back by its target
  plays two clips back to back: its own attack, released at tick 16, and then
  the target's attack in the mirrored strips [FND-COMBAT-005].
- A bar only ever shows damage from the clips played so far. Since the reset
  happens once, a gang shown in two focal lists, or in the lists of two
  sectors, keeps the damage of the clips already shown [FND-COMBAT-010].
- An evaded attack (`damage_dealt` -1) lowers no bar of the target; the
  attacker's bar drops by its `retaliation_taken`, which is 0 for an evaded
  attack.
- A gang that did not attack has target -1 in its entry, so it appears in a
  list only as the focal gang's target. The action is thus tested when the
  combat phase writes the rows, and only gangs listed in the focal gang's
  sector are found as attackers.
- The target's own target is looked up in the target's player's row of the
  same sector; a target listed elsewhere keeps the stale value there.
- `damage_dealt` and `retaliation_taken` of a gang that fought without
  attacking are undefined (RULE-COMBAT-002). A listed attacker's own values
  are always defined; the target branch reads only the focal gang's values.
- The fight list has room for 36 elements [FND-COMBAT-013]. Since the picker
  offers only other players' gangs, at most 30 gangs attack one gang in a
  sector and a list holds at most 33 elements. The builder does not check the
  count; a 37th element would overwrite the list length.
- With no sector to show, the presentation plays effect 4 only when the
  player asked for it.

## What the sources say

None known. The manual describes the Detailed Combat option only as showing
combat in detail.

## Differences between builds

None known.

## Open questions

- Whether a computer player can order an Attack on one of its own gangs,
  which could make a list longer than 36, was not checked [FND-COMBAT-013].
