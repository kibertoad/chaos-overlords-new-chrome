# Shared runtime migration

Status: complete for save promotion, writer leases and PCM conversion

The rebuild takes three primitives from the published `1.3.0` toolkit packages:
`RecoverableFile.Write` (`RefurbishedDinosaurs.Core`) stages, validates and promotes save and
replay generations, `FileWriteLock` (`RefurbishedDinosaurs.Core`) holds the autosave writer
lease, and `Pcm16.FromUnsigned8` (`RefurbishedDinosaurs.Media.Audio`) widens movie audio to
signed 16-bit samples. Only packages used by this restoration are referenced.

Game save payloads, version admission, slot naming, defaults, audio routing, fades, voice limits
and control policies remain local. The synthetic checks cover how the migrated code behaves; they
say nothing about parity with the original game or about live device behavior. No original assets
entered this change.
