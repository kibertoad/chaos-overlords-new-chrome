# DEV-ATTACK-002

- Departs from: RULE-ATTACK-002
- Reason: The rebuild also refuses an Attack on a gang the player has not detected when the order
  is submitted, so an order from the network or a replay cannot target one. The original only
  offers detected gangs in the picker.
- Setting: None
- Default: mandatory
- Justification: No input the original accepts reaches the refused case, so no player can tell the
  difference. It closes a path that only the network or a replay could use.
- Dropped: no
