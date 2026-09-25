---
id: SRC-RECHAOS-3561D41
title: "RE: Chaos Overlords (1996)", reverse-engineering notes by wfr, commit 3561d41
superseded_by: []
author: wfr
date: "2024-11-21"
location: https://github.com/wfr/re-chaos/tree/3561d4122b4a7670105366b3edb16ddd74c2b932
xxh3: null
licence: null
---

## Use

Layouts of the `SITES`, `Gangs` and `ITEMS` tables, a C header of the save file
and in-memory structures (`structs.h`), a list of image dimensions, and the save
file's magic values and sizes. It was made from a Windows 95 executable whose
SHA-256 is `0791e6209d573a79882675d1236737f5c9b369ea4af541a7dbd03cbadf4493d5`,
which is not the executable of BLD-GOG-EN-1.1, so its addresses do not apply and
its layouts are leads to check against this build's files.

## Known errors

- It gives the number of `ITEMS` records as 160. The file is 10,624 bytes of
  166-byte records, so there are 64.
- Its dimension list gives 242 by 157 for the 76,526-byte `PX06xxx` images. The
  size of the header and pixel data, and the row count of the paired 8-bit
  image, both give 242 by 158. 242 by 157 is right only for the 76,042-byte
  images.
