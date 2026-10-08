# site_definitions

The site types, read from `DATA/SITES`. A list the game keeps, of
FMT-DATA-001, in file order, indexed by a site slot's `definition`. The
list is at `0x004AB668`, 22 entries of 62 bytes read whole from `data\Sites`,
so the `resistance` field of entry `d` is at `0x004AB67E + d × 0x3E` and the
`tolerance` field at `0x004AB684 + d × 0x3E` [FND-TURN-001, FND-TURN-006]. The
six 16-bit skill modifiers (Research, Strength, Blade, Range, Fighting,
Martial Arts) are at `0x004AB698` to `0x004AB6A2` `+ d × 0x3E` [FND-STATE-011].
