# DEV-UI-011

- Departs from: SCR-UI-009
- Reason: Saving and loading use nine named slots, and Escape opens a pause menu. The original
  saves and loads from its menu bar.
- Setting: None
- Default: mandatory
- Justification: Saving and loading stay available wherever the original allows them, and slots with
  names replace a file dialog that the original's menu bar opens.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs, tests/Rechaos.Tests/GameMenuLayoutTests.cs,
  tests/Rechaos.Tests/SaveSlotCatalogTests.cs
- Dropped: no
