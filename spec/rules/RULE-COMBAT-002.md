---
id: RULE-COMBAT-002
title: The combat phase runs every attack, then the police, then applies the damage and fills the combat records
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-001, FND-COMBAT-003, FND-COMBAT-004, FND-GANG-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-ATTACK-001, RULE-POLICE-001, RULE-GANG-002, FMT-STATE-001, FMT-STATE-003]
---

## Summary

All combat in a turn is simultaneous. Every attacking gang attacks in player
and roster order, then the police attack in Crackdown sectors, and only then is
the damage taken off each gang's Force. A gang whose Force reaches 0 dies.

## When it runs

As `combat_phase`, after `chaos_phase` and before `transaction_phase`
[FND-CHAOS-001].

## Parameters

None.

## Inputs

`gangs` (each gang's `sector`, `action`, `force`, `weapon`, `armor`, `misc`),
`phase_damage`, `fight_marks`, `combat_records`, `combat_results`, and what
RULE-ATTACK-001 and RULE-POLICE-001 read.

## Procedure

```text
for i in 0..486:
    phase_damage[i] = 0
    fight_marks[i] = 0
    combat_records[i].damage_dealt = 0
    combat_records[i].retaliation_taken = 0
    combat_records[i].police_damage = -1

# Gang attacks
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_ATTACK:
            call RULE-ATTACK-001(gang, i)

# Police
call RULE-POLICE-001()

# Damage and records
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE:
            let rec = combat_records[i]
            rec.definition = gang.definition
            rec.force_start = gang.force
            let left = gang.force - phase_damage[i]
            if left < 0:
                left = 0
            rec.force_final = left
            rec.force_shown = gang.force
            rec.weapon = gang.weapon
            rec.armor = gang.armor
            rec.misc = gang.misc
            gang.force = left

# Sector rows for the presentation
for s in 0..64:
    for p in 0..6:
        for k in 0..6:
            combat_results[s].entries[p * 6 + k].roster_slot = -1
for each player in turn_order:
    let next_slot: INT32[] = []
    for s in 0..64:
        append(next_slot, 0)
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if fight_marks[i] != 0 and gang.sector != GANG_INACTIVE and next_slot[gang.sector] < 6:
            let e = combat_results[gang.sector].entries[player * 6 + next_slot[gang.sector]]
            e.player = player
            e.roster_slot = slot
            next_slot[gang.sector] = next_slot[gang.sector] + 1

# Deaths
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector != GANG_INACTIVE and gang.force == 0:
            call RULE-GANG-002(player, gang, true)
```

## Outputs

No return value. Lowers each damaged gang's `force` by its `phase_damage`,
stopping at 0, and sets `sector` to `GANG_INACTIVE` for each gang left with no
Force through RULE-GANG-002, which leaves its equipment bytes as they were
and counts the casualty [FND-GANG-003]. Fills
`combat_records` and the rows of `combat_results`. The draws are those of
RULE-ATTACK-001 and RULE-POLICE-001, in that order.

## Edge cases

- Damage beyond a gang's Force is lost; the statistic in RULE-COMBAT-003
  still counts it.
- A gang killed by one attack still makes its own attack and retaliation from
  its Force at the start of the phase, since nothing changes Force until the
  damage step.
- A sector row has six slots per player, which matches the six gangs a player
  may keep in one sector.

## What the sources say

SRC-MANUAL-GOG, numbered page 29, says all combat is simultaneous.

## Differences between builds

None known.

## Open questions

- The order of the steps after the police scan (records, damage, sector rows,
  deaths) is not recorded instruction by instruction; the findings place the
  record fill and then the sector rows after the police scan, and FND-GANG-003
  describes the death. The procedure's order within that part is an
  assumption, and so is the clearing at the start of the phase.
- Which gangs get a record (all active gangs, as written, or only those that
  fought), and whether records of other gangs keep an earlier turn's values.
- Byte 0 of the combat record: written here as the gang's `definition`; see
  FMT-STATE-003, where the row is disputed.
- Which gangs count as having fought for the sector rows, and whether a gang
  the police hit is listed. The entry layout of `combat_results` (a player and
  a roster slot per entry, -1 when empty) is not recorded; FND-AUDIO-002 says
  only that each entry is a four-byte pair that the resolver clears to -1.
- When the resolver sets the per-player police flag of `combat_results`.
- Where `phase_damage` and `fight_marks` are kept.
