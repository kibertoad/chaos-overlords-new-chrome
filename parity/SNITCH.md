# SNITCH

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-SNITCH-001` | Snitch lowers the gang's sector base Tolerance by 3, free and whatever the player's cash | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | The Snitch changes the base Tolerance, which reaches the Chaos test at the next rebuild before planning. EXP-TURN-032 takes a base below 1, EXP-TURN-024 has several Snitches in one sector in one turn, and EXP-TURN-114 writes a base Tolerance that one Snitch takes past -128, which wraps to 127. |
