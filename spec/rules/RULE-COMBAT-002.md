---
id: RULE-COMBAT-002
title: The combat phase runs every attack, then the police, then applies the damage and fills the combat records
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AUDIO-002, FND-AUDIO-013, FND-COMBAT-001, FND-COMBAT-003, FND-COMBAT-004, FND-COMBAT-008, FND-COMBAT-011, FND-EXE-004, FND-GANG-003, FND-GANG-005, FND-STATE-005, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-ATTACK-001, RULE-POLICE-001, RULE-GANG-002, FMT-STATE-001, FMT-STATE-003, FMT-STATE-008]
---

## Summary

All combat in a turn is simultaneous. Every attacking gang attacks in player
and roster order, then the police attack in Crackdown sectors. Each gang's
damage for the phase is capped at 10, the combat records and the sector rows
are written for the gangs that fought, and only then is the damage taken off
each gang's Force. A gang left with less than 1 Force dies.

## When it runs

As `combat_phase`, in the resolver `fn_00472775` (range in FND-EXE-004),
after `chaos_phase` and before `transaction_phase` [FND-CHAOS-001,
FND-COMBAT-008].

## Parameters

None.

## Inputs

`gangs` (each gang's `definition`, `sector`, `action`, `force`, `target`,
`target_2`, `weapon`, `armor`, `misc`), `phase_damage`, `fight_marks`,
`opening_damage`, `retaliation_damage`, `combat_records`, `combat_results`,
`casualties`, and what RULE-ATTACK-001 and RULE-POLICE-001 read.

## Procedure

```text
# phase_damage was set to 0 for all 486 gangs at the start of resolution
for i in 0..486:
    fight_marks[i] = 0
let next_entry: INT32[] = []
for i in 0..384:
    append(next_entry, 0)

# Gang attacks
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_ATTACK:
            call RULE-ATTACK-001(gang, i)

# Sector rows cleared; only the first word of each entry
for s in 0..64:
    for p in 0..6:
        for k in 0..6:
            combat_results[s].entries[p * 6 + k].gang = -1
        combat_results[s].police_hit[p] = 0

# Police
call RULE-POLICE-001()

for i in 0..486:
    if phase_damage[i] > 10:
        phase_damage[i] = 10

# Records and rows, for the gangs that fought
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if fight_marks[i] != 0:
            let rec = combat_records[i]
            rec.definition = gang.definition
            rec.force_start = gang.force
            rec.force_final = gang.force - phase_damage[i]
            rec.weapon = gang.weapon
            rec.armor = gang.armor
            rec.misc = gang.misc
            rec.damage_dealt = opening_damage[i]
            rec.retaliation_taken = retaliation_damage[i]
            let n = next_entry[player * 64 + gang.sector]
            let e = combat_results[gang.sector].entries[player * 6 + n]
            e.gang = i
            if gang.action == ACTION_ATTACK:
                e.target = gang.target * 81 + gang.target_2
            else:
                e.target = -1
            next_entry[player * 64 + gang.sector] = n + 1

# Damage and deaths
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE:
            gang.force = gang.force - phase_damage[i]
            if gang.force < 1:
                call RULE-GANG-002(player, gang, true)
```

## Outputs

No return value. Lowers each active gang's `force` by its capped
`phase_damage`, and for each gang left with less than 1 Force sets `sector` to
`GANG_INACTIVE` and counts a casualty through RULE-GANG-002, leaving its
equipment bytes as they were [FND-GANG-003, FND-GANG-005]. Writes bytes 0 to
8 of the combat record of every gang that fought (FMT-STATE-003) and one
entry of `combat_results` (FMT-STATE-008) for each, and copies `fight_marks`
into the global array at `0x00498BC0` [FND-COMBAT-008, FND-STATE-005]. The
draws are those of RULE-ATTACK-001 and RULE-POLICE-001, in that order.

## Edge cases

- Damage beyond a gang's Force is lost; the statistic in RULE-COMBAT-003
  still counts it. Since Force never exceeds 10, the cap at 10 changes no
  Force; it bounds `force_final`, which is Force minus the capped damage and
  is not clamped at 0, and the stored Force of a dead gang, which can be
  below 0 [FND-COMBAT-008, FND-GANG-005].
- A gang killed by one attack still makes its own attack and retaliation from
  its Force at the start of the phase, since nothing changes Force until the
  damage step.
- A gang counts as having fought when it attacked, when it was the target of
  an attack (evaded or not), or when the police found it, even for no damage.
  Only these gangs get a record and a row entry; every other record keeps
  bytes 0 to 8 from the last phase in which its roster slot fought
  [FND-STATE-005]. Byte 9, `police_damage`, is set to -1 for all 486 records
  by RULE-POLICE-001.
- `force_shown` (byte 3) is not written here; Detailed Combat sets it
  (RULE-COMBAT-004).
- `damage_dealt` and `retaliation_taken` of a gang that fought without
  attacking (a target, or a gang only the police found) are copied from
  stack locations the phase never wrote, so their values are undefined
  [FND-COMBAT-008]. The presentation reads `damage_dealt` only of attackers
  and police entries.
- The record and row step does not test the gang's sector. An attack on an
  inactive target would add an entry for the target to the row of sector 100,
  past the 64 rows.
- The entry counter has no upper bound. A seventh fighting gang of one player
  in one sector would be written into the next player's row, or past the
  row's end for player 5; the six-gang limit per sector normally prevents it.
- Only the first word of each entry is cleared. An entry left empty keeps the
  `target` it had in an earlier phase, and the readers test `gang` first.

## What the sources say

SRC-MANUAL-GOG, numbered page 29, says all combat is simultaneous.

## Differences between builds

None known.

## Open questions

None known. `phase_damage`, `fight_marks`, `opening_damage` and
`retaliation_damage` are locals of `fn_00472775` [FND-COMBAT-008].
