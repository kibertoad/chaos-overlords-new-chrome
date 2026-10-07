# rng

The game's one random number generator, the C runtime's `rand`. A random
number generator, specified by RULE-RNG-001; its state is `rng_state`. Rules
draw from it with the built-in `draw()`, which gives 0 to 32767, or through
`roll`.
