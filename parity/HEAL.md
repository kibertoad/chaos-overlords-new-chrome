# HEAL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HEAL-001` | Heal rolls four dice plus the gang's Heal and adds each success to Force, up to 10 | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.MatchState.cs | None | validated | EXP-TURN-059, EXP-TURN-062 and EXP-TURN-063 reach the three bands and a Heal capped at 10, and EXP-TURN-068 a pool of 0 or less. No recorded run reaches a Heal at Force 10, which the menus, the group bar and the computer never order (FND-HEAL-002, FND-UI-021). |
