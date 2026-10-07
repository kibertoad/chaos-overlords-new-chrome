# display_depth

The colour depth in bits, 8 or 16, that the surfaces are created at, or 0
when no depth could be set. Any other value the game keeps: a DWORD at
`0x0048787C`, holding the depth asked for and then the depth the display setup
returned [FND-PLATFORM-009, FND-GFX-004].
