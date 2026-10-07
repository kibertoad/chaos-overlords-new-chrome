# gang

A gang a player has hired. A structure the game keeps: FMT-STATE-001, kept as
one element of `gangs` [FND-HIRE-002, FND-UI-036]. A roster slot holds a gang
while the record's `sector` is not 100 (`GANG_INACTIVE`). The statistics the
game shows as Force, Combat, Defense, Stealth, Detect, Chaos, Control, Heal,
Influence, Research, Strength, Blade, Ranged, Fighting and Martial Arts are
the fields `force`, `combat`, `defense`, `stealth`, `detect`, `chaos`,
`control`, `heal`, `influence`, `research`, `strength`, `blade`, `ranged`,
`fighting` and `martial_arts`. All but `force` hold the effective value, with
equipment and completed sites added.
