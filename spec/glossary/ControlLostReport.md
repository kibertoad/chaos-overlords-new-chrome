# ControlLostReport

An event: a player has lost a sector, to a third Crackdown within five turns
or to another player's Control. It carries `player`, `sector` and `taker`, in
that order: `taker` is the player whose Control took the sector, or 0 for a
Crackdown [FND-EVENT-004]. Its handler is RULE-EVENT-013, which records a
type-3 Last Turn report, run at once [FND-POLICE-002, FND-EVENT-001].
