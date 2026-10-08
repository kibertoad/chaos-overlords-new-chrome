---
id: SRC-INSTALLER-GOG
title: GOG's offline installer for Chaos Overlords, which writes the game's registry preferences
superseded_by: []
author: GOG.com
date: "2015"
location: setup_chaos_overlords_2.1.0.17.exe, the offline installer GOG.com sells with product ID 1207659228 (BLD-GOG-EN-1.1); 85,307,496 bytes, SHA-256 73ac35b075ae16c8a8add4290e6b8beb516cf25468242201e129c57170a31e7b
xxh3: f03388775137c86a96cd85a36f4dae9d
licence: All rights reserved by GOG.com; only the names and numbers of registry values are used.
---

## Use

The installer that puts BLD-GOG-EN-1.1 on disk. It is an Inno Setup 5.5.0
(Unicode) installer, and its setup script, kept compressed in the file, creates
the key `Software\Stick Man Games\Chaos Overlords\1.0` under
`HKEY_LOCAL_MACHINE` with these values, in this order, each a DWORD unless
noted:

| Value | Data |
|---|---|
| `prefsVidDeep` | 1 |
| `prefsSlide` | 1 |
| `prefsBaseStats` | 0 |
| `prefsCombat` | 1 |
| `prefsFreeGang` | 1 |
| `commType` | 1 |
| `prefsVolumeSFX` | 6 |
| `prefsVolumeCD` | 5 |
| `prefsDiff` | 0 |
| `prefsTimeLimit` | 0 |
| `prefsObjective` | 4 |
| `prefsFullScreen` | 1 |
| `serialNum` | `0x1C962015` (479600661) |
| `AppPath` | the installation directory, a string |

These are the thirteen values the preference loader reads (RULE-OPTIONS-001),
all present, so after a GOG installation the key opens and every option takes
the installer's value rather than its initialized value or another value's
data. The installer's data differs from the initialized values in three places:
`commType` (1 against 0), `prefsDiff` (0, Goon, against 1, Criminal) and
`prefsObjective` (4, Kill 'Em All, against 0, Greed). Its `serialNum` is not 0,
so the loader makes no draws for a new one.

The script was read without running the installer: the setup loader's offset
table, marked `rDlPtS`, gives the setup data at file offset `0x050C68F1`, whose
first block, after the 64-byte `Inno Setup Setup Data (5.5.0) (u)` signature,
is an LZMA stream in 4,096-byte chunks that decompresses to 790,903 bytes. The
registry entries are the length-prefixed UTF-16 strings of each entry's key,
value name and data, where data written `$4` is the DWORD 4.

The uninstaller this installer leaves, `unins000.dat` (BLD-GOG-EN-1.1, Other
files), lists eleven entries under the same key without their data, and an
installation of BLD-GOG-EN-1.1 examined on 2026-10-08 held all fourteen values
under `HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node`, the 32-bit view, with the data
above except `prefsDiff`, which held 2. Something other than the installer had
written it; the game itself cannot (RULE-OPTIONS-002).

## Known errors

None known. The script writes the preferences once, at installation; it says
nothing about how the game reads or changes them.
