# BRIBE

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `RULE-BRIBE-001` | Bribe pays 3 cash to raise the gang's sector base Tolerance by 3 | established | complete | tests/Rechaos.Tests/OriginalNewGameExperimentTests.cs | None | validated | The Bribe changes the base Tolerance, which reaches the Chaos test at the next rebuild before planning. EXP-TURN-033 bribes nine times and then fails at 2 cash, EXP-TURN-042 pays with exactly 3 cash, and EXP-TURN-111 writes a base Tolerance that one Bribe takes past 127, which wraps to -127. |
