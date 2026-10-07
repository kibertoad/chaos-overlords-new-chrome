# CONTROL

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-CONTROL-001` | Control pools each player's strength per sector and settles contested sectors in ascending order, with the owner's defense added to its own pool and a neutral candidate at a zero margin | supported | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | `DEV-AI-002`, `DEV-CONTROL-001`, `DEV-CONTROL-002` | validated | A sector under police records a failed result for each Control gang (DEV-CONTROL-002); only players with an order and the owner compete (DEV-CONTROL-001). |
