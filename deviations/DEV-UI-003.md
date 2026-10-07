# DEV-UI-003

- Departs from: SCR-UI-004, SCR-MOVE-001, RULE-TURN-005
- Reason: A ctrl-click picks several gang cards, and Attack, Control, Heal, Hide, Influence or
  Move is then given to all of them at once. Each gang is validated on its own, and a bulk Move
  counts the whole selection against the destination's room before queueing. The original's group
  order strip goes through the same checks, so a gang that could not take the order on its own
  keeps its previous one where the original would write the order anyway.
- Setting: None
- Default: mandatory
- Justification: Each gang receives the order the player could give it on its own, validated on its
  own, so the selection only saves clicks.
- Dropped: no
