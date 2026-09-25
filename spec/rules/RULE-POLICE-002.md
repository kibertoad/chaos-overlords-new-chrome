---
id: RULE-POLICE-002
title: A Crackdown is recorded in the sector's history, and a third within five turns neutralizes the sector and adds 3 to 5 turns of police
status: supported
builds: [BLD-GOG-EN-1.1]
superseded_by: []
evidence: [FND-POLICE-001, FND-POLICE-002, FND-POLICE-004, FND-RNG-003, FND-RNG-006, FND-TURN-006, FND-EVENT-004, SRC-MANUAL-GOG]
conflicting: []
split_with: []
related: [RULE-RNG-002, FMT-STATE-002, FMT-STATE-004]
---

## Summary

Each sector remembers the turns of its last two Crackdowns that fall within
five turns. A Crackdown fills a free slot; when both slots are still in use,
this is the third Crackdown in five turns: the owner loses the sector, and only
then do 3 to 5 turns of police presence arrive in it. A Crackdown that fills a
free slot brings no police.

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
    emit ControlLostReport(sectors[s].owner, s, 0)
    sectors[s].owner = SECTOR_NEUTRAL
    for k in 0..3:
        sectors[s].sites[k].progress = 0
    crackdown_history[s][0] = elapsed_turns
    crackdown_history[s][1] = elapsed_turns
    sectors[s].crackdown_turns = sectors[s].crackdown_turns + roll(3) + 2
```

## Outputs

No return value. Updates `crackdown_history[s]`; on the third Crackdown in the
window emits `ControlLostReport` to the owner, makes the sector neutral,
clears its influence and adds 3 to 5 to `crackdown_turns`. Draws from `rng`
only on the third Crackdown: three, for `roll(3)`, after the history update.
FND-POLICE-001 read the draw as made for every Crackdown; the branch at the
end of the history update skips it whenever a slot was free (FND-POLICE-004).

## Edge cases

- The window is inclusive: a Crackdown exactly five turns ago still fills a
  slot (RULE-CHAOS-001 forgets only turns strictly older).
- After a neutralization both slots hold the current turn, so one more
  Crackdown within five turns neutralizes the sector again, even if another
  player took it in between.
- Police presence therefore always comes with the loss of the sector. The
  added turns extend any police presence already there. A permanent
  presence of 100 (`CRACKDOWN_PERMANENT`) grows to 103 to 105 and then counts
  down like any other; whether that can happen is not known.

## What the sources say

SRC-MANUAL-GOG, numbered pages 42 and 43 (Crackdown), says three Crackdowns in
a sector within five turns make the controlling Overlord lose it, that the
police stay 3 to 5 turns, and that a Crackdown while they are there makes them
stay longer. It does not say how the five turns are counted. The executable
sends the police only with the third Crackdown, the one that takes the sector
(FND-POLICE-004); the history stores `elapsed_turns` (FND-TURN-006).

## Differences between builds

None known.

## Open questions

- Which values the finding's "three influence-derived values" are; the
  procedure takes them to be the three sites' `progress`.
- What happens when the sector has no owner and both slots are full: whether a
  report is sent to owner -1.
- Whether the sector's Support and other values rebuilt from completed sites
  are cleared here or only at the next `turn_start` rebuild.
