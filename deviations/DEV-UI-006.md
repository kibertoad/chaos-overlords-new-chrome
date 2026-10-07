# DEV-UI-006

- Departs from: SCR-UI-003, SCR-UI-004, SCR-FINANCE-001
- Reason: The city console shows next turn's projected cash beside the current Cash, as
  `CASH 20 [18] (+1)`: cash, the cash left after queued Bribe and Equip prices, and the change
  over the whole cycle, with a breakdown on hover.
- Setting: None
- Default: mandatory
- Justification: A quality-of-life improvement that is strictly better. Every figure is one the
  player could work out from the Finance panel and the orders already queued, and the original makes
  the player do that sum by hand before each purchase. Showing it at a glance helps the player avoid
  Equips that fail for lack of cash, and it is the display that makes the order of purchases
  (DEV-EQUIP-001) readable. It removes no control and changes no rule.
- Dropped: no
