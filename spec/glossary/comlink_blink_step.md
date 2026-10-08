# comlink_blink_step

The eight-step animation counter the event pump advances once per
`presentation_tick`, from 0 to 7 and back to 0; on even values it blinks the
Events and Comlink lights, and it times the alert repeat and the selected
sector frame. Any other value the game keeps: a DWORD at `0x00487804`
[FND-AUDIO-012, FND-EVENT-006, FND-UI-023].
