# Shared runtime migration

Status: complete for save writes, reads and repair, writer leases and PCM conversion

The rebuild takes these primitives from the published `11.0.0` toolkit packages: `RecoverableFile`
(`RefurbishedDinosaurs.Core`) writes, reads and repairs save and replay generations (`Write`
stages, validates and promotes a generation and renames the kept primary to `.bak`; `Read` falls
back to the backup without changing a file, for the save browser; `ReadAndRepair` also restores a
damaged primary from the backup and keeps the rejected file as `.corrupt`), `FileWriteLock`
(`RefurbishedDinosaurs.Core`) holds the autosave writer lease, and `Pcm16.FromUnsigned8`
(`RefurbishedDinosaurs.Media.Audio`) widens movie audio to signed 16-bit samples. Only packages
used by this restoration are referenced.

`AtomicGenerationRecovery` keeps the game's side of recovery: which failures a backup may stand
in for (damage, never a save `IncompatibleSave` recognises), and that callers see the primary's
own failure when no generation loads.

Game save payloads, version admission, slot naming, defaults, audio routing, fades, voice limits
and control policies remain local. The synthetic checks cover how the migrated code behaves; they
say nothing about parity with the original game or about live device behavior. No original assets
entered this change.
