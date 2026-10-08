# HEAL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HEAL-001` | Heal rolls four dice plus the gang's Heal and adds each success to Force, up to 10 | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.MatchState.cs | None | validated | EXP-TURN-059, EXP-TURN-062 and EXP-TURN-063 reach the three bands and a Heal capped at 10, and EXP-TURN-068 a pool of 0 or less. The menus, the group bar and the computer never order a Heal at Force 10 (FND-HEAL-002, FND-UI-021); EXP-TURN-110 reaches it by writing the gang's Force after the order, and the Heal rolls its pool and leaves the Force at 10. |
