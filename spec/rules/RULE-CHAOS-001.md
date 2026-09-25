---
id: RULE-CHAOS-001
title: Chaos is rolled gang by gang, and a sector whose Chaos exceeds its Tolerance gets a Crackdown
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-007, FND-CHAOS-001, FND-POLICE-001, FND-POLICE-002, FND-RNG-003, SRC-MANUAL-GOG]
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

As `chaos_phase`, after `instant_phase` and before `combat_phase`
[FND-CHAOS-001].

## Parameters

None.

## Inputs

Each gang's `sector`, `action`, `force` and `chaos`; each sector's `income`,
`tolerance` and `owner`; `difficulty_band`; `crackdown_history`;
`elapsed_turns`; `sector_presence` (RULE-POLICE-004); the state of `rng`
through `roll`.

## Procedure

```text
let sector_total: INT32[] = []
for s in 0..64:
    append(sector_total, 0)

for each player in turn_order:
    for slot in 0..81:
        let i = player * 81 + slot
        let gang = gangs[i]
        chaos_successes[i] = 0
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_CHAOS:
            let s = gang.sector
            let band = difficulty_band[player]
            let pool = sectors[s].income + gang.force + gang.chaos
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
            let counted = successes
            if band == 2 and sectors[s].owner == player:
                counted = successes - successes / 4
            sector_total[s] = sector_total[s] + counted

for s in 0..64:
    # Crackdowns older than the five-turn window are forgotten
    for k in 0..2:
        let t = crackdown_history[s][k]
        if t != -100 and t < elapsed_turns - 5:
            crackdown_history[s][k] = -100
    if sector_total[s] > sectors[s].tolerance:
        for i in 0..486:
            if gangs[i].sector == s and gangs[i].action == ACTION_CHAOS:
                chaos_successes[i] = 0
        for p in 0..6:
            if sector_presence[s * 6 + p] != 0:
                emit CrackdownReport(p, s)
        call RULE-POLICE-002(s)
```

## Outputs

No return value. Sets `chaos_successes` for every gang (0 for a gang that is
not doing Chaos or whose sector cracked down), updates `crackdown_history`,
emits `CrackdownReport` to each player present in a sector that cracks down,
and through RULE-POLICE-002 neutralizes sectors and adds police presence.
Draws from `rng`: three per die, gang by gang in `turn_order` and roster
order, then, sector by sector in ascending order, the draws of RULE-POLICE-002
for each sector that cracks down.

## Edge cases

- The comparison is strict: a total equal to the Tolerance does not crack
  down.
- A band-2 player's successes in a sector it owns count only three quarters,
  rounded up, toward the Crackdown; its payout still uses all of them.
- A sector already under Crackdown can crack down again, which adds to the
  police presence (RULE-POLICE-002).
- A player with a gang in the sector gets the report even without having
  ordered Chaos there.

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

- The procedure clears `chaos_successes` for every gang at the start; the
  finding says only that the successes are stored per gang.
- Whether the history expiry runs for every sector, as written, or only for a
  sector that is about to be tested; the finding places it before the trigger
  is evaluated. Whether `elapsed_turns` is the turn counter the history
  stores.
- The order in which the Crackdown reports visit the players.
- How the sector total is kept: the finding describes a total per player and
  sector, and the procedure sums them per sector for the comparison.
- Whether a sector already under Crackdown pays Chaos when this turn's total
  stays at or below the Tolerance: the manual's wording suggests not, and no
  finding shows a test of the police presence in the Chaos passes.
- `income` is a disputed row of FMT-STATE-002: FND-UPKEEP-001 reads the Chaos
  pool as taking the cash byte instead.
- `chaos_successes` and `sector_presence` are locals of the resolver with no
  fixed address.
