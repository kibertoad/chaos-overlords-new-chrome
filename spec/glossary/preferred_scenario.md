# preferred_scenario

The scenario a fresh local setup starts on: 0 (Greed) in the executable's
data, replaced at startup by the registry value `prefsObjective`, which GOG's
installer writes as 4 (Kill 'Em All), and set to the scenario chosen on the
setup screen. Any other value the game keeps: `INT8` at `0x00487858`
[FND-SETUP-009, FND-SETUP-012, FND-SETUP-013, SRC-INSTALLER-GOG].
