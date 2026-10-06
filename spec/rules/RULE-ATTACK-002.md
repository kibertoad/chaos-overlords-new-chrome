---
id: RULE-ATTACK-002
title: An Attack can target only an enemy gang the attacker's player sees in the attacker's sector
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-ATTACK-001, FND-ATTACK-006, FND-COMBAT-006, FND-DETECT-001, EXP-ATTACK-001, EXP-ATTACK-002, EXP-ATTACK-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-DETECT-001, SCR-ATTACK-001, FMT-STATE-001]
---

## Summary

The Attack picker lists, for the opponent the player chose, only that
opponent's gangs in the attacking gang's sector that the player can see. The
chosen gang is written as the attacking gang's target.

## When it runs

In `planning_phase`, when the Attack picker (SCR-ATTACK-001) shows an
opponent's gangs, and when the player confirms a target.

## Parameters

- `attacker`, of FMT-STATE-001: the gang being given the order.
- `enemy`, the opponent's player slot.

## Inputs

The attacker's `player` and `sector`; the enemy's gangs' `sector` and
`visible_to`, as RULE-DETECT-001 last rebuilt them.

## Procedure

```text
let targets: INT32[] = []
for slot in 0..81:
    let g = gangs[enemy * 81 + slot]
    if g.sector == attacker.sector and g.visible_to[attacker.player] != 0:
        append(targets, slot)
return targets
```

On confirmation the picker writes the chosen opponent and one slot of the
returned list into the attacker's `target` and `target_2`, and sets its
`action` to `ACTION_ATTACK`.

## Outputs

Returns the roster slots of the enemy gangs that can be targeted, in roster
order. Changes nothing itself and makes no draws. Confirming writes only the
attacker's own order bytes [FND-COMBAT-006].

## Edge cases

- An inactive gang (sector 100) never matches a sector, so it is never
  listed.
- The visibility tested is the active player's [FND-ATTACK-006]; the picker
  is only opened for the active player's own gangs, so it is the attacker's
  player.
- A gang that is hiding but that the player still sees can be targeted; the
  attack may then miss it (RULE-ATTACK-001).

## What the sources say

SRC-MANUAL-GOG, numbered page 29, says Attack can be given only when there are
enemy gangs in the sector that the player can detect, and that the player picks
the target in the Target Acquisition panel and may change it until pressing
Done. Numbered page 36 says every friendly gang in a sector can attack an enemy
that one of them can see. Both agree with the executable.

## Differences between builds

None known.

## Open questions

- Which of `target` and `target_2` receives the player and which the roster
  slot, and where the picker writes `action`, are not recorded.
- Whether the picker enables an opponent's portrait only when the list is not
  empty (FND-ATTACK-001 says only that disabled entries do not react).
- The picker shows at most six targets (SCR-ATTACK-001). The roster builder
  does not bound its count, so a seventh target is stored past the six entries
  [FND-ATTACK-006]; what that does is not recorded. Hires keep a player to six
  gangs in a sector, but some moves are not checked against that limit.
- No recorded list holds a hidden gang the player still sees; EXP-ATTACK-001
  to EXP-ATTACK-003 record 15 lists. With the seventh target above, this rests
  on FND-ATTACK-006 and FND-DETECT-001. Until a run reaches them, the entry
  stays `supported` (DECISIONS.md, 2026-10-06).
