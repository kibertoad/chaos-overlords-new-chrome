# ControlGainedReport

An event: a player's Control order has taken a sector. It carries `player`,
`sector` and `previous`, the sector's owner before or -1, in that order
[FND-EVENT-004]. Its handler is
RULE-EVENT-012, run at once [FND-EVENT-001].
