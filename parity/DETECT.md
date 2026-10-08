# DETECT

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-DETECT-001` | A player sees an enemy gang when its Stealth is at most the player's detection strength in that sector | supported | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.MatchState.cs | None | validated | The replays compare every active gang's `visible_to` bytes. EXP-SETUP-004 reaches the visibility name modifier, EXP-TURN-025 an observer out of the match, and EXP-TURN-062 helpers with Detect above and at most 9, a tie for the base, and a Stealth equal to the strength. No replay compares the `visible_to` bytes an inactive gang keeps (FND-DETECT-002). The entry stays supported: no run reaches a helper with negative Detect, an empty observer slot, or the inactive gangs' bytes (DECISIONS.md, 2026-10-06). |
