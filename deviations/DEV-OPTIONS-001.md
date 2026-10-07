# DEV-OPTIONS-001

- Departs from: RULE-OPTIONS-001, RULE-OPTIONS-002, BUG-OPTIONS-001, BUG-OPTIONS-002
- Replaces: RULE-OPTIONS-002
- Reason: The original opens its registry key read-only for loading and for writing, so no option
  is ever saved, and a missing value takes the previous value's data from a shared buffer. The
  rebuild writes a checked per-user file in one step. A file that is missing, unreadable or of an
  unknown version, or that lacks a field its version requires, is read as the defaults as a whole,
  so no option takes another's value. A field a later version added takes its own default when an
  older file is upgraded, and the lobby presentation, Intro only once and the preferred scenario,
  which a file of the current version may leave out, take their own defaults when it does.
- Setting: None
- Default: mandatory
- Justification: The Options menu was written to keep the player's choices, and the original loses
  them only because of the two bugs. Nobody gains from choosing the options again at every launch.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs, tests/Rechaos.Tests/GamePreferencesStoreTests.cs
- Dropped: no
