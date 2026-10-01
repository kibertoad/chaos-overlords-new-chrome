---
id: EXP-TURN-036
title: Do repeated turn runs agree on running totals, hire roles and the planning and resolution behaviour they exercise?
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
environment: Windows 11 Pro 10.0.26200, an unelevated copy of the executable and SMACKW32.DLL beside junctions to the install's DATA, MUSIC and HELP directories, run with the compatibility layers DWM8And16BitMitigation, WINXPSP2, DISABLEDWM, 640X480 and DISABLEDXMAXIMIZEDWINDOWEDMODE, windowed, Warn if Idle Gangs and Detailed Combat switched off and the sound levels set to 0 in memory, under the Windows debugging interface of tools/Rechaos.OriginalProbe
starting_state: new-game
recording: null
repetitions: 28
fixture: EXP-TURN-036.json
---

## Question

Do the original's running totals and computer hire roles agree with the static
rules, and what additional behaviour do the repeated runs cover?

## Setup

The setups, seeds, orders and stop points are those of EXP-TURN-001 and
EXP-TURN-010 through EXP-TURN-035. EXP-TURN-002 through EXP-TURN-009 have no
supplemental running-total samples here. This entry records supplemental observations from their reruns;
it does not claim that another run was made to write this entry.

## Procedure

Repeat each source run with its recorded setup and inputs, extracting each
player's cash earned and spent, damage inflicted, casualties, overthrows,
hides, current hire role and previous hire role at the final planning phase.
The supplemental fixture identifies each source fixture and its zero-based
run index and preserves these extracted fields. The source fixtures retain
the complete draw sequences and final state alongside these measurements.

## Observations

The supplemental fixture records 28 final-state samples. EXP-TURN-016
ends with cash earned of 5, 40, 30, 34, 36 and 48 and cash spent of 8, 32,
36, 30, 21 and 35 for players 0 through 5. All six damage totals are zero;
the human's current hire role is -1 and every computer player's is 0.

12 samples include nonzero damage totals. They agree with counting full
opening attack damage, including damage beyond the target's remaining Force,
without adding retaliation or police damage (RULE-COMBAT-003).

The final reports of EXP-TURN-023 and EXP-TURN-024 include Crackdowns sent to
players who raised no Chaos there; EXP-TURN-024 includes a recipient with no
gang left in the sector at the end. Recipients follow the gangs present when
resolution began (RULE-POLICE-004).

The computer planning and hire outcomes agree with the sector danger and
hostility refresh (RULE-AI-003) and encoded hire destinations (RULE-AI-012).
The unused random destination modes are not exercised. EXP-TURN-017's
computer equipment choices require the danger scan to include the gang's
own sector (RULE-AI-005). EXP-TURN-031's human Equip and Sell do not establish
a computer upgrade choice.

Attitudes after takeovers in EXP-TURN-011, EXP-TURN-017 and EXP-TURN-018 agree
with the takeover reaction (RULE-AI-017); none reaches the -10 floor.
The family-0 sequences in EXP-TURN-010 and EXP-TURN-017 agree with RULE-AI-019
in the previous-action branches reached. No recorded run reaches its
previous-Snitch branch, Control after a failed draw following Attack, or a
failed draw after Heal, Hide or Move.

## Results

The source runs' replay comparisons agree on the extracted running totals
and computer players' current and previous hire roles, as well as the draws,
final gang and sector records, attitudes and Last Turn reports above.
The human's current hire role differs (-1 in the original, 0 in the replay);
no rule reads it. That unused field is excluded from the comparison rather
than claimed as agreement.

## Conclusion

The supplemental observations agree with the static readings of
RULE-COMBAT-003, RULE-POLICE-004, RULE-AI-003, RULE-AI-005, RULE-AI-012,
RULE-AI-017 and RULE-AI-019 in the cases reached. They support established
status without claiming coverage of the unobserved branches.
