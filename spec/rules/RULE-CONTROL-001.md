---
id: RULE-CONTROL-001
title: Control pools each player's strength per sector and settles sectors in ascending order, with a neutral candidate at a zero margin
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006, FND-CONTROL-001, FND-CONTROL-002, FND-RNG-003, FND-EVENT-001, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-EVENT-012, RULE-EVENT-013, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004, RULE-AI-017]
---

## Summary

Each player's gangs ordered to Control a sector add their Force and Control
together. From that total the game subtracts the sector's Income, the Force and
Control of the owner's gangs defending it, and the Support of its influenced
sites. The player with the largest positive result takes the sector; ties are
settled at random, and a result of exactly zero wins only half the time for a
single challenger.

## When it runs

`control_phase`, after `move_phase`.

## Parameters

None.

## Inputs

`turn_order`, `gangs` with each gang's `sector`, `action`, `force` and
`control`, `sectors` with each sector's `owner`, `income`, `support` and
`sites`, and the state of `rng` through `roll`.

## Procedure

```text
let pool: INT32[] = []
for i in 0..384:
    append(pool, 0)
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_CONTROL:
            let cell = player * 64 + gang.sector
            pool[cell] = pool[cell] + gang.force + gang.control
for s in 0..64:
    let sector = sectors[s]
    let defense = 0
    if sector.owner != SECTOR_NEUTRAL:
        for slot in 0..81:
            let defender = gangs[sector.owner * 81 + slot]
            if defender.sector == s and defender.action != ACTION_HIDE:
                defense = defense + defender.force + defender.control
    let best = 0
    let candidates: INT32[] = [-1]
    for each player in turn_order:
        let margin = pool[player * 64 + s] - sector.income - defense - sector.support
        if margin > best:
            best = margin
            candidates = [player]
        else if margin == best:
            append(candidates, player)
    let winner = candidates[0]
    if count(candidates) > 1:
        winner = candidates[roll(count(candidates)) - 1]
    if winner != -1:
        let previous = sector.owner
        sector.owner = winner
        for each site in sector.sites:
            site.progress = 0
        grudge_after_takeover(previous, winner)
        emit ControlGainedReport(winner, s)
        if previous != SECTOR_NEUTRAL:
            emit ControlLostReport(previous, s)
```

## Outputs

No return value. For each sector with a winner, sets `owner` to the winner and
`progress` of its three site slots to 0, then emits `ControlGainedReport` to
the winner and, when the sector had an owner, `ControlLostReport` to that
owner. Makes one call of `roll` for each
sector whose candidate list holds more than one entry, in ascending sector
order, and no draw otherwise.

## Edge cases

- A margin below 0 never enters the list, so it never wins.
- A single player with the largest positive margin wins without a draw.
  Players tied at the largest positive margin are chosen between with one
  `roll`, each with equal chance.
- At a best margin of 0 the neutral entry -1 stays first: one challenger wins
  when `roll(2)` gives 2, half the time, and `n` tied challengers each win with
  probability `1 / (n + 1)`, the rest leaving the owner unchanged.
- The scan compares all six players, including those who gave no Control
  order, whose pools are 0. When `income + defense + support` is below 0, every
  such player has a positive margin and can tie or beat the real challenger and
  be given the sector (BUG-CONTROL-001).
- Ownership changes only here and when a Crackdown neutralizes a sector:
  moving away or terminating the last gang in a sector does not give it up
  (FND-CONTROL-002).
- Read literally, a winner equal to the current owner also resets the sector's
  site progress.

## What the sources say

SRC-MANUAL-GOG, page 30 (Control), says more gangs make control easier, that
control is needed to influence sites, and that gangs in a controlled sector
help defend it. Page 50 (The Math of the Game, Control) gives the formula:
the Force plus Control of all the player's gangs, minus the sector's Income,
and for an enemy sector also minus the Force plus Control of every defending
gang that is not hiding and minus the Support of the influenced sites. There
is no dice roll; a positive result takes the sector, a negative one fails, and
zero gives a 50 percent chance. When several players try for a neutral sector
the one with the highest number wins. Losing a sector loses its influenced
sites, which return to full Resistance. The manual does not mention ties
between players or the neutral candidate with several zero-margin challengers,
and does not mention that players without a Control order take part.

## Differences between builds

None known.

## Open questions

- The grudge of RULE-AI-017 is placed at the owner change by interpretation:
  FND-AI-006 gives the update but not the instruction that calls it.
- FND-CONTROL-001 does not list the terms of a gang's strength or of the
  defense. Force plus Control for both, and leaving hidden defenders out, are
  taken from the manual.
- Which byte of the sector record the pass reads as Income is not recorded; the
  procedure uses `income`, whose offset is disputed in FMT-STATE-002.
- Whether sectors with no Control order, or sectors under a Crackdown, are
  skipped is not recorded. If neither is skipped, the zero-pool effect above can
  change the owner of a sector nobody tried to take.
- What else "clears the sector's influence totals" resets besides the site
  progress, and whether the winner's `overthrow_count` is raised when the
  sector had another owner, are not recorded in these findings.
- FND-EVENT-001 gives the report types (2 to the new owner, 3 to the previous
  owner) but not where in the capture they are recorded, their order, their
  arguments, or whether they are recorded when the winner already owned the
  sector. The procedure records them after the ownership change, the gain
  first.
- Whether the owner's own gangs count as defenders when the owner is itself a
  candidate is not recorded.
