# CombatClip

An event: Detailed Combat plays one clip. It carries `focal`, the viewer's
gang as its element number in `gangs`; `other`, the other gang's element
number, or -2 for the police; `mirrored`, true when `other` attacks `focal`;
and `hold`, false when the next clip follows at tick 16 without the result
hold. It has no handlers; SCR-COMBAT-002 draws it [FND-COMBAT-005].
