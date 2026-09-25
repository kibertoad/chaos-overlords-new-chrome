---
id: RULE-ATTACK-001
title: One gang's attack and the retaliation it provokes
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006, FND-AI-007, FND-COMBAT-001, FND-COMBAT-003, FND-COMBAT-004, FND-COMBAT-006, FND-RNG-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-HIDE-001, RULE-COMBAT-001, RULE-COMBAT-003, RULE-AI-016, FMT-STATE-001, FMT-STATE-003]
---

## Summary

An attacking gang rolls Force plus its combat rating minus the target's
Defense in dice, and each die at or above a threshold is one point of damage.
If the target was hiding it may evade first. Unless the attacker fought bare
handed with Martial Arts, or the target was hiding, the target strikes back
with half its own successes. Nothing is taken off Force until the end of the
combat phase.

## When it runs

In `combat_phase`, called by RULE-COMBAT-002 for each active gang whose
`action` is `ACTION_ATTACK`, in `turn_order` and then roster slot order
[FND-COMBAT-001].

## Parameters

- `attacker`, of FMT-STATE-001: the attacking gang.
- `attacker_index`, its element number in `gangs`, `player * 81 + roster_slot`.

## Inputs

The attacker's and the target's `player`, `force`, `action`, `weapon`,
`defense`, `detect`, `stealth`, `martial_arts` and the fields
`combat_rating` reads; the attacker's `target` and `target_2`;
`difficulty_band`; `phase_damage`; the state of `rng` through `roll`.

## Procedure

```text
let target_index = attacker.target * 81 + attacker.target_2
let target = gangs[target_index]
let attacker_band = difficulty_band[attacker.player]
let target_band = difficulty_band[target.player]
fight_marks[attacker_index] = 1
fight_marks[target_index] = 1

# A target that is hiding may evade
if is_hidden(target):
    let evade_below = target.stealth + 14 - attacker.detect
    if attacker_band == 2:
        evade_below = target.stealth + 10 - attacker.detect
    if roll(20) < evade_below:
        combat_records[attacker_index].damage_dealt = -1
        return

# Opening attack
let defense = target.defense
if target_band == 0:
    defense = defense - defense / 4
let pool = attacker.force + combat_rating(attacker) - defense
let threshold = 5
if attacker_band == 0:
    threshold = 6
if attacker_band == 2:
    threshold = 4
let successes = 0
for i in 0..pool:
    if roll(6) >= threshold:
        successes = successes + 1
let damage = successes
if pool > 0:
    damage = max(successes, pool / 4)
phase_damage[target_index] = phase_damage[target_index] + damage
call RULE-COMBAT-003(attacker.player, damage)
combat_records[attacker_index].damage_dealt = damage
grudge_after_attack(attacker.player, target.player, damage)

# Retaliation
if is_hidden(target):
    return
if attacker.martial_arts > 0 and attacker.weapon == -1:
    if not (target.martial_arts > 0 and target.weapon == -1):
        return
let back_pool = target.force + combat_rating(target) - attacker.defense
let back_threshold = 5
if target_band == 2:
    back_threshold = 4
let back_successes = 0
for i in 0..back_pool:
    if roll(6) >= back_threshold:
        back_successes = back_successes + 1
let back_damage = back_successes / 2
phase_damage[attacker_index] = phase_damage[attacker_index] + back_damage
combat_records[attacker_index].retaliation_taken = back_damage
```

## Outputs

No return value. Adds the opening damage to the target's `phase_damage` and
the retaliation damage to the attacker's, credits the opening damage to the
attacker's player through RULE-COMBAT-003, writes `damage_dealt` and
`retaliation_taken` of the attacker's element of `combat_records`, and sets
`fight_marks` for both gangs. Draws from `rng`: three per call of `roll`, so
three for the evasion roll when the target is hiding, then three per die of
the opening pool, then three per die of the retaliation pool when there is a
retaliation.

## Edge cases

- A pool of 0 or less rolls no dice and does no damage; the minimum damage
  `pool / 4` applies only to a positive pool.
- Force is read from the records, which the combat phase does not change until
  its end (RULE-COMBAT-002), so a gang that another attack has already
  killed in this phase still attacks and retaliates with its Force at the start
  of the phase.
- The block checks only that the attacker is active and its action is Attack
  [FND-COMBAT-006]. It does not check that the target is active or in the
  attacker's sector.
- Two gangs that attack each other each run this rule when the scan reaches
  them, so the pair makes two opening attacks and up to two retaliations
  [FND-COMBAT-006].
- A hiding target that is hit never retaliates.

## What the sources say

SRC-MANUAL-GOG, numbered page 29 (the Attack command) and page 51 (Combat, in
the Math of the Game), gives the attack roll as the gang's Combat minus the
defender's Defense, each success one point of damage, and a retaliation that
reverses the two gangs and halves the result. It leaves out the attacker's
current Force, which the executable adds [FND-AI-007]. It says an unarmed
Martial Artist takes no retaliation unless the defender is also an unarmed
Martial Artist, and that a hiding gang that is found and attacked does not
retaliate, which agrees with the executable. It does not mention the
difficulty bands, the lowered Defense of band-0 defenders, the minimum damage,
or the thresholds other than 4 and above. Page 51 and 52 (Hiding) give a hidden
gang a 50 percent chance to be hit when the attacker's Detect equals its
Stealth, moved by 5 percent per point of difference; the executable instead
rolls 1 to 20 against `Stealth + 14 - Detect` (or `+ 10` for band 2), which
at equal values lets the target evade 13 times in 20, a 35 percent chance to
be hit, for bands 0 and 1.

## Differences between builds

None known.

## Open questions

- Where in the attack the grudge of RULE-AI-016 is applied is placed here
  after the opening damage by interpretation: FND-AI-006 gives the update but
  not the instruction that calls it, so whether an evaded attack or the
  retaliation also lowers the attitude is not recorded.
- The attack block's instruction addresses are not recorded; the order of the
  evasion draw, the opening dice and the retaliation dice follows the order the
  findings describe the calculations in, and has not been checked instruction by
  instruction.
- Whether the attacker's Defense is lowered by a quarter for a band-0 attacker
  when the target retaliates, whether the retaliation has a minimum damage, and
  whether the retaliation threshold for band 0 is 5 (as FND-AI-007 states for
  bands 0 and 1) were read from the band findings only.
- Whether the evasion compares the target's Stealth with the attacker's Detect,
  as written, or other fields: FND-AI-007 names Stealth and Detect without
  saying whose.
- Which of `target` and `target_2` holds the target's player and which its
  roster slot is not pinned (FMT-STATE-001).
- Whether an evaded attack writes -1 into `damage_dealt` before or after
  anything else, and whether it credits anything to `damage_inflicted`.
- The Martial Arts test: FND-AI-007 states it both as "effective Martial Arts
  zero" and as "no positive effective Martial Arts"; the procedure uses the
  second. A negative effective Martial Arts would tell them apart.
- `fight_marks`: where the game keeps it and exactly which gangs it marks.
- `phase_damage`: where the game keeps it (unknown).
