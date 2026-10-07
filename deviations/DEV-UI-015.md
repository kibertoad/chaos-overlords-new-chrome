# DEV-UI-015

- Departs from: RULE-UI-013
- Reason: A second copy of the rebuild starts and runs beside the first. The original refuses a
  second copy and brings the running one to the front.
- Setting: None
- Default: mandatory
- Justification: Refusing a second copy only takes a choice from the player. Nothing in a match
  depends on there being one copy: the copies share only the rolling autosave, which a file guard
  keeps one writer at a time, and each keeps its own match. A second copy is also how one computer
  holds two seats of an online match. A setting that restored the refusal would offer nothing
  but the loss of that choice.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.Persistence.cs, tests/Rechaos.Tests/RollingAutoSaveTests.cs
- Dropped: no
