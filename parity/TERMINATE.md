# TERMINATE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-TERMINATE-001` | Terminate pass retires every gang ordered to Terminate, before any Move | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | Terminate retires the gang through the same step as RULE-GANG-002, before any Move, with no casualty and no Last Turn report. Force 0, the cleared orders and Hidden are the representation of the inactive slot (2026-09-26 decision). EXP-TURN-019 replays the Terminate of the human's only gang. |
