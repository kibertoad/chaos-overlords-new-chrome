---
id: BUG-AI-008
title: Family 2's late Control gates test the sector numbered like the item of a planned Equip
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-077, EXP-TURN-055, EXP-TURN-056]
conflicting: []
split_with: []
related: [RULE-AI-021, RULE-AI-003, FMT-STATE-001]
---

## Symptom

A family-2 computer gang that plans an Equip in a sector held by a player its
owner is hostile to keeps the Equip where the late Control gates would have
replaced it with Control, or loses it to Control in a sector where they would
not have fired.

## Trigger conditions

A family-2 gang whose previous action is not Attack plans an armor or weapon
Equip. The late gates then run with the item number in place of the sector.

## Mechanism

The handler keeps the upgrade selector's result in the local it later passes
to the late gates as the sector, and only the paths that equip nothing
overwrite it with the gang's sector (FND-AI-077). The owner query, the
attitude toward that owner, the count of the owner's visible gangs and the
combat-advantage flag are then read for the sector numbered like the item.
The count of visible human gangs and the human-owner test use the gang's own
sector.

## Frequency

Every Equip family 2 plans. In EXP-TURN-055 player 4's family-2 gang in
sector 51, held by the human and made hostile by the combat-advantage test
(RULE-AI-003), planned the weapon numbered 3 in the pass after the
fifteenth Done press. Sector 3 belonged to player 1, toward whom player 4 was
not hostile, so neither gate fired and the gang equipped. Tested on sector
51, the second gate fires, since the human's only gang there was hidden from
player 4, and the gang takes Control instead.

## Player reliance

Unknown. Players cannot see which test kept or replaced the Equip.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

None.
