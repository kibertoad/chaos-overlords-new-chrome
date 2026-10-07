# UPKEEP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-UPKEEP-001` | Upkeep charges each active gang its Upkeep and pays each owned sector's Cash byte, player by player | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | EXP-TURN-059 reaches a negative Upkeep, sectors whose Cash is negative, 0 and positive, and cash left below 0; the replays compare cash, cash earned and cash spent. |
