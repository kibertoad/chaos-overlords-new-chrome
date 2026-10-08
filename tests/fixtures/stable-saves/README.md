# Saves from stable releases

From 1.0.0 on, this directory holds a save of every save format a stable release
has written, named after the first release that wrote it (`1.0.0.rchsave`).
The change that moves the save format adds the new one; the 1.0.0 save is added
before the release workflow moves `version.txt` to 1.0.0.
`SaveCompatibilityPolicyTests` loads every file here with the current build,
and once `version.txt` reaches 1.0.0 it fails while no file here has the
current save format. The policy is in `docs/NATIVE-SAVE-FORMAT.md` under
"Compatibility policy".

Before 1.0.0 the directory stays empty: development formats carry no guarantee,
and a fixture here would have to keep loading through every format change.
