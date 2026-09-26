---
id: RULE-ATTACK-001
title: One gang's attack and the retaliation it provokes
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006, FND-AI-007, FND-AI-047, FND-COMBAT-001, FND-COMBAT-003, FND-COMBAT-004, FND-COMBAT-006, FND-COMBAT-008, FND-EXE-004, FND-RNG-003, FND-RNG-006, FND-STATE-002, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-HIDE-001, RULE-COMBAT-001, RULE-COMBAT-003, RULE-AI-016, FMT-STATE-001, FMT-STATE-003]
---

## Summary

An attacking gang rolls its Force plus its Combat minus the target's Defense
in dice, and each die at or above a threshold is one point of damage. If the
target was hiding it may evade first. Unless the target was hiding, or the
attacker fought bare handed with Martial Arts against a target that is not
also a bare-handed Martial Artist, the target strikes back with half its own
successes. Nothing is taken off Force until the end of the combat phase.
Every attack, evaded or not, lowers the target's owner's attitude toward the
attacker.

## When it runs

In `combat_phase`, called by RULE-COMBAT-002 for each active gang whose
`action` is `ACTION_ATTACK`, in `turn_order` and then roster slot order
[FND-COMBAT-001, FND-COMBAT-008].

## Parameters

- `attacker`, of FMT-STATE-001: the attacking gang.
- `attacker_index`, its element number in `gangs`, `player * 81 + roster_slot`.

## Inputs

The attacker's and the target's `player`, `force`, `action`, `weapon`,
`combat`, `defense`, `detect`, `stealth` and `martial_arts`; the attacker's
`target` (the target's player) and `target_2` (its roster slot)
[FND-STATE-002, FND-COMBAT-008]; `difficulty_band`; `phase_damage`,
`fight_marks`, `opening_damage` and `retaliation_damage`; the state of `rng`
through `roll`.

## Procedure

```text
let target_index = attacker.target * 81 + attacker.target_2
let target = gangs[target_index]
let attacker_band = difficulty_band[attacker.player]
fight_marks[attacker_index] = 1
fight_marks[target_index] = 1
opening_damage[attacker_index] = -1
retaliation_damage[attacker_index] = 0

# A target that is hiding may evade
let evaded = false
if is_hidden(target):
    let evade_below = target.stealth + 14 - attacker.detect
    if attacker_band == 2:
        evade_below = target.stealth + 10 - attacker.detect
    if roll(20) < evade_below:
        evaded = true

if not evaded:
    # Opening attack
    let defense = target.defense
    if difficulty_band[target.player] == 0:
        defense = defense - defense / 4
    let pool = attacker.force + attacker.combat - defense
    let threshold = 5
    if attacker_band == 0:
        threshold = 6
    if attacker_band == 2:
        threshold = 4
    let damage = 0
    for i in 0..pool:
        if roll(6) >= threshold:
            damage = damage + 1
    if pool > 0 and damage < pool / 4:
        damage = pool / 4
    phase_damage[target_index] = phase_damage[target_index] + damage
    call RULE-COMBAT-003(attacker.player, damage)
    opening_damage[attacker_index] = damage

    # Retaliation
    let strikes_back = false
    if not is_hidden(target):
        if attacker.martial_arts == 0 or attacker.weapon != -1:
            strikes_back = true
        if target.martial_arts > 0 and target.weapon == -1:
            strikes_back = true
    if strikes_back:
        let back_pool = target.force + target.combat - attacker.defense
        let back_threshold = 5
        if difficulty_band[attacker.target] == 2:
            back_threshold = 4
        let back_successes = 0
        for i in 0..back_pool:
            if roll(6) >= back_threshold:
                back_successes = back_successes + 1
        let back_damage = back_successes / 2
        phase_damage[attacker_index] = phase_damage[attacker_index] + back_damage
        retaliation_damage[attacker_index] = back_damage

grudge_after_attack(attacker.player, attacker.target, opening_damage[attacker_index])
```

## Outputs

No return value. Adds the opening damage to the target's `phase_damage` and
the retaliation damage to the attacker's, credits the opening damage to the
attacker's player through RULE-COMBAT-003, sets the attacker's
`opening_damage` and `retaliation_damage`, which RULE-COMBAT-002 copies into
`damage_dealt` and `retaliation_taken` of its combat record, marks both gangs
in `fight_marks`, and lowers one cell of `attitude` through
`grudge_after_attack` (RULE-AI-016). Draws from `rng`: three per call of
`roll`, so three for the evasion roll when the target is hiding, then, unless
the attack was evaded, three per die of the opening pool, then three per die
of the retaliation pool when there is a retaliation.

## Edge cases

- A pool of 0 or less rolls no dice and does no damage; the minimum damage
  `pool / 4` applies only to a positive pool.
- Only the target's Defense against the opening attack is lowered for band 0.
  The attacker's Defense against a retaliation is never lowered, and the
  retaliation has no minimum damage [FND-COMBAT-008].
- The defender's band is read through the target's own `player` byte, the
  retaliation threshold through the attacker's `target` byte. The two name the
  same player for any target the picker can offer.
- `combat` is the stored effective Combat, which already holds the skills that
  go with the weapon (RULE-COMBAT-001). It is rebuilt at the top of each turn,
  so a sector owner lost to this turn's Crackdown still counts in it.
- Force is read from the records, which the combat phase does not change until
  its end (RULE-COMBAT-002), so a gang that another attack has already
  killed in this phase still attacks and retaliates with its Force at the start
  of the phase.
- The block checks only that the attacker is active and its action is Attack
  [FND-COMBAT-006, FND-COMBAT-008]. It does not check that the target is active
  or in the attacker's sector.
- Two gangs that attack each other each run this rule when the scan reaches
  them, so the pair makes two opening attacks and up to two retaliations
  [FND-COMBAT-006].
- A hiding target that is hit never retaliates. An evaded attack does no
  damage, credits nothing to Damage Inflicted, draws no retaliation, keeps
  `opening_damage` at -1, and still lowers the attitude by the target's
  owner's reaction [FND-AI-047].
- The Martial Arts test is exactly 0 on the attacker and above 0 on the
  target, so a bare-handed attacker with negative Martial Arts takes no
  retaliation from a target that is not a bare-handed Martial Artist.

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

None known. `phase_damage`, `fight_marks`, `opening_damage` and
`retaliation_damage` are locals of the resolver `fn_00472775` with no fixed
address [FND-COMBAT-008].
