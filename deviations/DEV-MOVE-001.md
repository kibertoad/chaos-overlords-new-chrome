# DEV-MOVE-001

- Departs from: RULE-MOVE-001, SCR-MOVE-001
- Reason: The rebuild refuses a Move, when it is ordered, into a sector that already holds six
  of the player's gangs. The original's panel has no capacity test (FND-MOVE-007): it accepts the
  order, and RULE-MOVE-002 sends the gang back at resolution.
- Setting: None
- Default: mandatory
- Justification: The player learns at once that the gang cannot enter, where the original accepts
  the order and then sends the gang back at resolution with its turn spent. A gang the player could
  have given a useful order instead is no longer wasted on a Move that cannot happen.
- Dropped: no

The refusal applies to the orders a person gives only. A Move the computer planner plans into a
full sector, for a computer seat or for a human seat a simulation hands to the planner, is planned
as in the original and left to the Move repair, which EXP-TURN-010 depends on.
Whether the original's panel refuses the order too is in `manual_validation_plan.md`. The rebuild
counts only the gangs already in the destination, so moving one gang out and another in to a full
sector takes two turns where the original allows one; counting gangs ordered out of the
destination, as DEV-HIRE-001 does, would remove that difference.
