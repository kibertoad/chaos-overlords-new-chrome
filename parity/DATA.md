# DATA

| Spec ID | Title | Spec status | Code | Tests | Deviations | Status | Notes |
|---|---|---|---|---|---|---|---|
| `FMT-DATA-001` | Site definition records in DATA/SITES | supported | complete | tests/Rechaos.Tests/OriginalDataTableFileTests.cs | None | validated | None |
| `FMT-DATA-002` | Gang definition records in DATA/Gangs | supported | complete | tests/Rechaos.Tests/OriginalDataTableFileTests.cs | None | validated | None |
| `FMT-DATA-003` | Item definition records in DATA/ITEMS | supported | complete | tests/Rechaos.Tests/OriginalDataTableFileTests.cs | None | validated | None |
| `FMT-DATA-004` | Colour list in DATA/CLT00002 | supported | complete | None | `DEV-UI-016` | deviated | The rebuild draws only the 16-bit image set, for which the original never loads this palette either (DEV-UI-016); the extractor copies the file unread. |
| `FMT-DATA-005` | Compressed archive DATA/DATA.Z | supported | complete | tests/Rechaos.Tests/InstallShieldArchiveTests.cs | None | validated | The game never reads the file (FND-DATA-008), and the rebuild copies it unread into its asset pack. InstallShieldArchive reads the header and both entry tables and expands every block; the test expands the shipped archive and finds the 449 equal files, the nine that differ and README.DOC as FND-DATA-010 does. |
