# DEV-EQUIP-001

- Departs from: RULE-EQUIP-001, RULE-EQUIP-002, RULE-SELL-001
- Reason: The original carries out Equip and Sell in a scan by player slot and roster slot, so a
  Sell in an earlier slot can pay for an Equip in a later one whatever order the player gave them
  in. The rebuild debits and credits cash in the order the player last submitted Equip and Sell
  orders, a replaced order moving to the end. Players still resolve by slot, Give keeps its
  deferred deliveries in roster order, and a buyer whose cash exactly equals the price still buys.
- Setting: None
- Default: mandatory
- Justification: The roster-slot order has no meaning in play. The player never sees a gang's
  roster slot while giving orders, so whether a Sell pays for an Equip is decided by a number the
  player cannot read, and an order that looks affordable fails for no visible reason. The rebuild
  resolves Equip and Sell in the order the player scheduled them, which the player controls and
  the cash row of the console shows, so this is strictly better and needs no setting to restore
  the original. Every outcome of the original stays reachable, since giving Sell and Equip in slot
  order reproduces the original's scan.
- Dropped: no

Kept mandatory on 2026-09-26, after a proposal to put it behind a setting that starts off.
