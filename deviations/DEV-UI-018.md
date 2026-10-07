# DEV-UI-018

- Departs from: RULE-UI-014, FMT-STATE-009
- Replaces: FMT-STATE-009
- Reason: Keyboard and mouse state is read once per frame, 60 times a second, and each screen acts
  on what changed since the last frame. There is no event queue, accelerator table or menu command
  event; the options are on the Options screen. A double-click is two presses on the same target
  within 500 ms. Online text fields take characters from the platform keyboard layout.
- Setting: None
- Default: mandatory
- Justification: It changes how commands are reached and leaves what they do alone. Every option
  and command the original's event step handles stays reachable.
- Tests: tests/Rechaos.Tests/DeviationBehaviourTests.cs
- Dropped: no
