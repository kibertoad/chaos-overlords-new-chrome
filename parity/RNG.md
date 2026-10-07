# RNG

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-RNG-001` | The generator, its step, and its seed at process start | supported | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.Rolls.cs | `DEV-RNG-001` | validated | The rebuild seeds the run from the low 16 bits of a process-uptime clock when the game object is created, and takes an explicit full-width seed for replays, tests and online matches (DEV-RNG-001). Each later New Game and each load draws on from where the run's sequence stands, as the original never reseeds. The entry stays supported: no run reaches a second match or a load in the same process (DECISIONS.md, 2026-10-06). |
| `RULE-RNG-002` | roll(n) gives a whole number from 1 to n from three draws | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.Rolls.cs | None | validated | None |
