# DEV-TIMER-001

- Departs from: RULE-TIMER-004, RULE-UI-008, RULE-UI-003
- Reason: The panel slide takes its step from a fixed benchmark of 84 copies a second where the
  original measures the machine for one second at startup. Presentation ticks are counted from the
  game clock, so a tick that falls during a long frame is counted rather than lost. The ticks a
  pointer hold or a soundtrack fade keeps from the event pump, or a hold keeps from a panel's own
  loop, are dropped as in the original (FND-UI-046, FND-UI-047, FND-AUDIO-017).
- Setting: None
- Default: mandatory
- Justification: The original's slide speed depends on the machine it runs on, which AGENTS.md
  lets the rebuild fix; 84 copies a second gives the original's 16-pixel step. Apart from a
  pointer hold and a soundtrack fade, which the rebuild reproduces, a tick is lost in the original
  only when the machine stalls, and no rule reads the ticks.
- Dropped: no
