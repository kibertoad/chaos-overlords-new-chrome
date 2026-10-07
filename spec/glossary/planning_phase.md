# planning_phase

The part of a turn in which each player gives orders. Players plan one after
another in `turn_order`: a computer player's slot runs the computer planner, a
human's slot the human handler, and an eliminated slot is skipped
[FND-TURN-005]. Visibility is rebuilt once, before the first player plans
[FND-DETECT-001, FND-TURN-006], and each planning entry refills vacant
`hire_offers` [FND-HIRE-001]. It runs after `turn_start` and before
`resolution` [FND-TURN-005].
