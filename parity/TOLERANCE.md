# TOLERANCE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TOLERANCE-001` | At the start of each resolution a sector's base Tolerance moves one point toward 17 minus its base Income | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | The replays compare every sector's base Tolerance after up to twenty-five resolutions. The base Tolerance moves at the start of the instant phase; the sites' part is added only by the rebuild before planning. |
| `RULE-TOLERANCE-002` | After the instant phase every sector's base Tolerance is clamped to 1..40 | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-032 takes a base Tolerance to 0 and to -1 by Snitch, and both end at 1. No run reaches the upper bound. |
