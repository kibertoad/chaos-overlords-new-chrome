---
id: RULE-POLICE-001
title: In a Crackdown sector the police may find each gang and attack it with 25 minus its Defense in dice
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-COMBAT-001, FND-POLICE-001, FND-POLICE-003, FND-RNG-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-HIDE-001, FMT-STATE-001, FMT-STATE-002, FMT-STATE-003]
---

## Summary

While police are present in a sector, they look for every gang there. The
chance to be found is 115 percent minus 5 per point of Stealth, 20 less for a
hiding gang. A gang that is found is attacked with 25 minus its Defense in
dice, each die of 5 or 6 one point of damage.

## When it runs

As `police_phase`, called by RULE-COMBAT-002 after every gang attack and
before the damage is applied [FND-COMBAT-001].

## Parameters

None.

## Inputs

Each gang's `sector`, `action`, `stealth` and `defense`; each sector's
`crackdown_turns`, including presence a Crackdown added this turn
[FND-POLICE-001]; the state of `rng` through `roll`.

## Procedure

```text
for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE and sectors[gang.sector].crackdown_turns > 0:
            let hide_penalty = 0
            if is_hidden(gang):
                hide_penalty = 20
            let chance = 115 - 5 * gang.stealth - hide_penalty
            if roll(100) <= chance:
                let pool = 25 - gang.defense
                let successes = 0
                for d in 0..pool:
                    if roll(6) >= 5:
                        successes = successes + 1
                phase_damage[i] = phase_damage[i] + successes
                combat_records[i].police_damage = successes
```

## Outputs

No return value. Adds each found gang's police damage to its `phase_damage`
and writes it to its record's `police_damage`. Draws from `rng`: three for the
detection roll of every active gang in a sector with police, then three per
die for each gang found, gang by gang in `turn_order` and roster order.

## Edge cases

- The detection draw is made even when the chance is 0 or less, or 100 or
  more.
- With `<=` as written, a visible gang is always found up to Stealth 3, is
  found 95 times in 100 at Stealth 4, and is never found from Stealth 23; a
  hiding gang is never found from Stealth 19.
- Defense 25 or more gives an empty pool and no damage, but the gang still
  counts as found.
- Police damage does not count toward Damage Inflicted, and the police take no
  retaliation.

## What the sources say

SRC-MANUAL-GOG, numbered page 52 (Crackdown, in the Math of the Game), says the
police attack every gang in the sector with Combat 20, that a gang with
Stealth 5 or less is always spotted and each point above 5 lowers the chance by
5 percent, so that Stealth 25 is never spotted, and that the police use Detect
12 against a hiding gang. Numbered page 43 says the police attack every gang
they can find for 3 to 5 turns and cannot be attacked or killed. The
executable's chance is 10 points lower at every Stealth, treats a hiding gang
by a flat 20 points, and does not use a police Detect. Its pool of 25 is the
police Force 5 of the manual's table plus Combat 20.

## Differences between builds

None known.

## Open questions

- Whether the detection roll must be at most the chance, as written, or
  strictly less; FND-POLICE-003 records the comparison but not its direction.
- Whether the difficulty band of the gang's player changes the police pass;
  FND-AI-007 lists no band read there.
- When the per-player police flag of `combat_results` is set.
