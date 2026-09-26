---
id: RULE-CONTROL-001
title: Control pools each player's strength per sector and settles contested sectors in ascending order, with the owner's defense added to its own pool and a neutral candidate at a zero margin
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-AI-006, FND-CONTROL-001, FND-CONTROL-002, FND-RNG-003, FND-EVENT-001, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, RULE-EVENT-012, RULE-EVENT-013, FMT-STATE-001, FMT-STATE-002, FMT-STATE-004, RULE-AI-017]
---

## Summary

Each player's gangs ordered to Control a sector add their Force and Control
together. Only sectors where some gang gave a Control order and where no
police are present are settled. The owner's defending gangs that are not
hiding, plus the sector's Income and Support, are added to the owner's own
pool; then the Income and Support are subtracted from every player's pool.
The player with the largest positive result takes the sector, so a challenger
has to beat the owner's defense. Ties are settled at random, and a result of
exactly zero wins only part of the time.

## When it runs

`control_phase`, after `move_phase`.

## Parameters

None.

## Inputs

`turn_order`, `gangs` with each gang's `player`, `sector`, `action`, `force`
and `control`, `sectors` with each sector's `owner`, `income`, `support`,
`crackdown_turns` and `sites`, `overthrow_count`, and the state of `rng`
through `roll`.

## Procedure

```text
let pool: INT32[] = []
for i in 0..384:
    append(pool, 0)
let contested: INT32[] = []
for s in 0..64:
    append(contested, 0)
for each player in turn_order:
    for slot in 0..81:
        let gang = gangs[player * 81 + slot]
        if gang.sector != GANG_INACTIVE and gang.action == ACTION_CONTROL:
            let cell = gang.player * 64 + gang.sector
            pool[cell] = pool[cell] + gang.force + gang.control
            if sectors[gang.sector].crackdown_turns == 0:
                contested[gang.sector] = 1
for s in 0..64:
    if contested[s] == 0:
        continue
    let sector = sectors[s]
    if sector.owner != SECTOR_NEUTRAL:
        let cell = sector.owner * 64 + s
        for slot in 0..81:
            let defender = gangs[sector.owner * 81 + slot]
            if defender.sector == s and defender.action != ACTION_HIDE:
                pool[cell] = pool[cell] + defender.force + defender.control
        pool[cell] = pool[cell] + sector.income + sector.support
    let best = 0
    let candidates: INT32[] = [-1]
    for each player in turn_order:
        let margin = pool[player * 64 + s] - (sector.income + sector.support)
        if margin == best:
            append(candidates, player)
        if margin > best:
            best = margin
            candidates = [player]
    let winner = candidates[0]
    if count(candidates) > 1:
        winner = candidates[roll(count(candidates)) - 1]
    if winner != -1 and winner != sector.owner:
        let previous = sector.owner
        if previous != SECTOR_NEUTRAL:
            overthrow_count[winner] = overthrow_count[winner] + 1
            grudge_after_takeover(previous, winner)
        sector.owner = winner
        for each site in sector.sites:
            site.progress = 0
        emit ControlGainedReport(winner, s, previous)
        emit ControlLostReport(previous, s, winner)
```

## Outputs

No return value. For each sector whose winner is a player other than its
owner, raises the winner's `overthrow_count` when the sector had an owner,
sets `owner` to the winner and `progress` of its three site slots to 0, then
emits `ControlGainedReport` to the winner and `ControlLostReport` to the
previous owner; the second is dropped when the sector was neutral
[FND-CONTROL-003]. Makes one call of `roll` for each settled sector whose
candidate list holds more than one entry, in ascending sector order, and no
draw otherwise.

## Edge cases

- A sector where no active gang was ordered to Control is not settled, and
  neither is a sector whose `crackdown_turns` is not 0, even a permanent
  Crackdown; Control orders there have no effect [FND-CONTROL-003].
- The owner's margin is its own Control pool in the sector plus its
  defenders' Force and Control, since Income and Support are added to its pool
  and then subtracted again. A challenger's margin is its pool minus Income
  minus Support. A challenger with no rival takes the sector when its margin
  exceeds the owner's, which is the manual's `pool - income - defense -
  support > 0`.
- An owner's gang ordered to Control its own sector counts twice for the
  owner: in its pool and as a defender.
- A margin below 0 never enters the list, so it never wins.
- The winner being the current owner changes nothing: no site progress is
  reset and no report is recorded.
- A single player with the largest positive margin wins without a draw.
  Players tied at the largest positive margin, the owner included, are chosen
  between with one `roll`, each with equal chance.
- At a best margin of 0 the neutral entry -1 stays first and every player at
  0, the owner included, is appended: the sector changes hands only when the
  draw picks a challenger. One challenger alone at 0 in a neutral sector wins
  half the time.
- The scan compares all six players, including those who gave no Control
  order in the sector. A non-owner's margin is then `-(income + support)`, so
  when `income + support` is below 0 in a contested sector such a player can
  tie or beat the real challenger and be given the sector (BUG-CONTROL-001).
- Ownership changes only here and when a Crackdown neutralizes a sector:
  moving away or terminating the last gang in a sector does not give it up
  (FND-CONTROL-002).
- The sites keep their definitions; only their progress is reset.

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

- The grudge of RULE-AI-017 is the update of the previous owner's attitude
  toward the winner that FND-CONTROL-003 observes at the owner change; its
  arithmetic belongs to the AI entries.
- Whether `income + support` can be below 0 with the shipped site table
  decides whether BUG-CONTROL-001 can fire at all.
