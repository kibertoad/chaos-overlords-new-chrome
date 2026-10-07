# HELP

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-HELP-001` | WinHelp container HELP/Chaos.hlp | supported | complete | tests/Rechaos.Tests/OriginalHelpFileTests.cs | `DEV-HELP-001`, `DEV-HELP-002` | validated | None |
| `FMT-HELP-002` | Help contents file HELP/CHAOS.CNT | supported | complete | tests/Rechaos.Tests/OriginalHelpFileTests.cs | None | validated | None |
| `RULE-HELP-001` | Help Topics does nothing, and no key opens the help file | supported | complete | None | `DEV-HELP-001` | deviated | F1 and the Escape menu open the rebuild's own help viewer on the player's help file, where the original's Help Topics does nothing (DEV-HELP-001). |
