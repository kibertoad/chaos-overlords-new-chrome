# HIDE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-HIDE-001` | A gang hides while its action is Hide, and each Hide carried out is counted for its player | supported | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.MatchState.cs | None | validated | EXP-TURN-057 reaches a one-off Hide and EXP-TURN-010 a recurring one; the replays compare every player's Hide count. The entry stays supported: no run replaces or cancels a Hide during planning (DECISIONS.md, 2026-10-06). |
