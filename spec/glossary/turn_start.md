# turn_start

The work the game does at the start of every turn, before `planning_phase`,
in this order. It clears recurring actions that can no longer apply and
copies each gang's `repeat_action` and `repeat_target` into `action` and
`target` [FND-TURN-004, FND-HIDE-001], and runs `upkeep_phase`
[FND-UPKEEP-001]; the first pass of the turn loop, after a new game or a load,
skips both [FND-TURN-006]. It rebuilds every sector record from its
completed sites [FND-UPKEEP-001, FND-UI-035], and then every active gang's
effective statistics [FND-GANG-001].
