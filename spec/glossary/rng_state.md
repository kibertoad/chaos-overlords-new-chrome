# rng_state

The generator's 32-bit state. Any other value the game keeps: `UINT32LE`,
kept in the C runtime's per-thread data, which the runtime allocates, so it
has no fixed address [FND-RNG-001, FND-RNG-002].
