---
id: RULE-POLICE-002
title: A Crackdown is recorded in the sector's history, a third within five turns neutralizes the sector, and it adds 3 to 5 turns of police
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-POLICE-001, FND-POLICE-002, FND-RNG-003, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Each sector remembers the turns of its last two Crackdowns that fall within
five turns. A Crackdown fills a free slot; when both slots are still in use,
this is the third Crackdown in five turns, and the owner loses the sector. Every
Crackdown then adds 3 to 5 turns to the police presence in the sector.

## When it runs

In `chaos_phase`, called by RULE-CHAOS-001 for a sector whose Chaos total
exceeds its Tolerance, after that sector's Crackdown reports.

## Parameters

- `s`, the sector.

## Inputs

`crackdown_history[s]`, `elapsed_turns`, the sector's `owner`, `sites` and
`crackdown_turns`, and the state of `rng` through `roll`.

## Procedure

```text
if crackdown_history[s][0] == -100:
    crackdown_history[s][0] = elapsed_turns
else if crackdown_history[s][1] == -100:
    crackdown_history[s][1] = elapsed_turns
else:
    emit ControlLostReport(sectors[s].owner, s)
    sectors[s].owner = SECTOR_NEUTRAL
    for k in 0..3:
        sectors[s].sites[k].progress = 0
    crackdown_history[s][0] = elapsed_turns
    crackdown_history[s][1] = elapsed_turns
sectors[s].crackdown_turns = sectors[s].crackdown_turns + roll(3) + 2
```

## Outputs

No return value. Updates `crackdown_history[s]`; on the third Crackdown in the
window emits `ControlLostReport` to the owner, makes the sector neutral and
clears its influence; adds 3 to 5 to `crackdown_turns`. Draws from `rng`:
three, for `roll(3)`, after the history update.

## Edge cases

- The window is inclusive: a Crackdown exactly five turns ago still fills a
  slot (RULE-CHAOS-001 forgets only turns strictly older).
- After a neutralization both slots hold the current turn, so one more
  Crackdown within five turns neutralizes the sector again, even if another
  player took it in between.
- The added turns extend any police presence already there. A permanent
  presence of 100 (`CRACKDOWN_PERMANENT`) grows to 103 to 105 and then counts
  down like any other; whether that can happen is not known.

## What the sources say

SRC-MANUAL-GOG, numbered pages 42 and 43 (Crackdown), says three Crackdowns in
a sector within five turns make the controlling Overlord lose it, that the
police stay 3 to 5 turns, and that a Crackdown while they are there makes them
stay longer. It does not say how the five turns are counted.

## Differences between builds

None known.

## Open questions

- Which values the finding's "three influence-derived values" are; the
  procedure takes them to be the three sites' `progress`.
- What happens when the sector has no owner and both slots are full: whether a
  report is sent to owner -1.
- Whether `elapsed_turns` is the turn counter the history stores.
- The address of the duration draw is not recorded (FND-POLICE-001).
- Whether the sector's Support and other values rebuilt from completed sites
  are cleared here or only at the next `turn_start` rebuild.
