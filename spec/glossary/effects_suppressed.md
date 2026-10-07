# effects_suppressed

Whether every sound effect is silenced. Any other value the game keeps: the
byte at `0x0048735C`, which the effect player tests before `PlaySoundA`; it is
0 in the executable's data and nothing writes it, so it never silences anything
[FND-AUDIO-003, FND-AUDIO-006]. Earlier versions of the spec called it
`sound_output_available` and read it as the presence of a wave output device.
