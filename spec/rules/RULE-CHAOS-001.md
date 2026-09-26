---
id: RULE-CHAOS-001
title: Chaos is rolled gang by gang, and a sector whose Chaos exceeds its Tolerance gets a Crackdown
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-007, FND-CHAOS-001, FND-CHAOS-002, FND-EXE-004, FND-POLICE-001, FND-POLICE-002, FND-POLICE-004, FND-RNG-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-POLICE-002, RULE-POLICE-004, FMT-STATE-001, FMT-STATE-002]
---

## Summary

Each gang ordered to Chaos rolls its sector's Income plus its Force plus its
Chaos in dice. The successes of every player in a sector are added up, and if
the sum is more than the sector's Tolerance the police crack down: nobody is
paid for Chaos there this turn. The payout itself comes later in the turn
(RULE-CHAOS-002).

## When it runs

As `chaos_phase`, after `instant_phase` and before `combat_phase`, in the
resolver `fn_00472775` at `0x00473188..0x004737D3` (range in FND-EXE-004)
[FND-CHAOS-001, FND-CHAOS-002].

## Parameters

None.

## Inputs

Each gang's `sector`, `action`, `force` and `chaos`; each sector's `income`,
`tolerance` and `owner`; `difficulty_band`; `crackdown_history`;
`elapsed_turns`; `sector_presence` (RULE-POLICE-004); the state of `rng`
through `roll`.

## Procedure

```text
let player_total: INT32[] = []
for n in 0..384:
    append(player_total, 0)

for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_CHAOS:
            let s = gang.sector
            let band = difficulty_band[player]
            let pool = sectors[s].income + gang.chaos + gang.force
            let threshold = 5
            if band == 0:
                pool = pool - pool / 5
            if band == 2:
                threshold = 4
            let successes = 0
            for d in 0..pool:
                if roll(6) >= threshold:
                    successes = successes + 1
            chaos_successes[i] = successes
            player_total[player * 64 + s] = player_total[player * 64 + s] + successes

for s in 0..64:
    # Crackdowns older than the five-turn window are forgotten
    for k in 0..2:
        let t = crackdown_history[s][k]
        if t != -100 and t < elapsed_turns - 5:
            crackdown_history[s][k] = -100
    let sector_total = 0
    for p in 0..6:
        let counted = player_total[p * 64 + s]
        if sectors[s].owner == p and difficulty_band[p] == 2:
            counted = counted - counted / 4
        sector_total = sector_total + counted
    if sector_total > sectors[s].tolerance:
        for p in 0..6:
            for slot in 0..81:
                if gangs[p * 81 + slot].sector == s:
                    chaos_successes[p * 81 + slot] = 0
            if sector_presence[s * 6 + p] != 0:
                emit CrackdownReport(p, s)
        call RULE-POLICE-002(s)
```

## Outputs

No return value. Sets `chaos_successes` for every gang doing Chaos, and to 0
for every gang in a sector that cracks down, updates `crackdown_history`,
emits `CrackdownReport` to each player present in a sector that cracks down,
and through RULE-POLICE-002 neutralizes sectors and adds police presence.
Draws from `rng`: three per die, gang by gang in `turn_order` and roster
order, then, sector by sector in ascending order, the draws of RULE-POLICE-002
for each sector that cracks down.

## Edge cases

- The comparison is strict: a total equal to the Tolerance does not crack
  down.
- A band-2 player's successes in a sector it owns count only three quarters,
  rounded up, toward the Crackdown; the quarter is taken from the player's
  summed total in the sector, not gang by gang. Its payout still uses all of
  them.
- No test of police presence is made. A sector already under police presence
  pays Chaos when its total stays at or below the Tolerance, and can crack
  down again, which records another Crackdown in its history; presence is
  added only by the third Crackdown in the window (RULE-POLICE-002,
  FND-POLICE-004).
- A player with a gang in the sector when resolution began gets the report
  even without having ordered Chaos there. The reports go out in ascending
  player order, each after the zeroing of that player's gangs.
- The history expiry runs for every sector, whether or not it cracks down.
- `chaos_successes` is written only for gangs doing Chaos and for gangs in a
  sector that cracks down. Other gangs' values are never initialized; the
  payout reads only gangs whose action is Chaos [FND-CHAOS-002].

## What the sources say

SRC-MANUAL-GOG, numbered pages 29 and 30 (the Chaos command), 42 (Crackdown)
and 49 (Chaos, in the Math of the Game), gives the Chaos roll as the Force plus
Chaos of all the player's gangs doing Chaos in the sector plus the sector's
Income, one cash per success, half cash outside the player's own sectors with
full successes counted toward the Crackdown, and a Crackdown when the total
Chaos exceeds the Tolerance, which stops all Chaos income in the sector. The
executable rolls each gang separately with its own copy of the Income, and the
manual does not mention the difficulty bands.

## Differences between builds

None known.

## Open questions

None known. `chaos_successes`, the per-player totals and `sector_presence`
are locals of `fn_00472775` [FND-CHAOS-002]. The pool reads `income`, offset
`0x04` of the sector record [FND-CHAOS-002].
