# EquipCashShort

An event: an Equip failed because its player could not pay. It carries
`player`, `sector` and `definition`, in that order: the gang's sector and its
`definition` byte, which the Last Turn report stores in place of the item
[FND-EVENT-004]. Its handler is
RULE-EVENT-014, run at once [FND-EVENT-001].
