# DEV-FINANCE-001

- Departs from: SCR-FINANCE-001, RULE-FINANCE-001
- Reason: The Equipment row credits a Sell order with half the Cost of the one item the resolver
  pays for (RULE-SELL-001, BUG-SELL-001). The original adds half the Cost of every item the order
  selects (FND-FINANCE-002). The other seven rows follow FND-FINANCE-002.
- Setting: None
- Default: mandatory
- Justification: The panel is a forecast of next turn's cash, and on a multi-item Sell the
  original's forecast names cash the resolver never pays. Showing the amount that will arrive adds
  information and changes no order or result. A setting would only bring back a figure known to be
  wrong.
- Dropped: no
