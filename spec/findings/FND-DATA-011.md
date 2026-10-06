---
id: FND-DATA-011
title: The site table keeps the two research specials out of one sector and every sector sum far inside a signed byte
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: DATA/SITES
    offset: 0x00..0x553
tool: the rebuild's loader of the 22 records of FMT-DATA-001, read with a test-scoped script
environment: null
---

## Observation

Read over all 22 records of `DATA/SITES` (FMT-DATA-001):

- The record with `special` 1 and the record with `special` 2 have `research`
  (`0x30`) values that add up to 7. No record has a negative `research`.
- The largest value of any one record, and the sum of the three largest, are
  4 and 9 for `support` (`0x18`), 2 and 5 for `tolerance` (`0x1C`) and 6 and
  14 for `cash` (`0x1E`). The smallest value and the sum of the three
  smallest are -2 and -3 for `support`, -2 and -5 for `tolerance` and -2 and
  -3 for `cash`.
- Every one of the fourteen modifiers (`0x20` to `0x3A`) lies between 0 and
  6 in every record.

## Interpretation

City generation redraws a sector's site whenever one of the fourteen modifier
totals of the sites drawn so far leaves -6..6 (RULE-CITY-002). The two
research specials already total 7 in `research`, and a third site cannot
lower it, so no sector is generated with both, and RULE-SITE-001 never meets
a Science Center and a Research Lab completed in one sector. The headquarters
site that RULE-CITY-003 puts in place of a sector's first site has `special`
0, so it adds no research special either.

The same check keeps each modifier sum of a sector's sites within -6..6. The
three fields it does not check give at most 9 Support, 5 Tolerance and 14
Cash above the sector's base values, and at least -3, -5 and -3; with a base
Tolerance clamped to 1..40 (RULE-TOLERANCE-002) and a `cash_yield` starting
at 1, no sum that RULE-SITE-001 stores in a signed byte can leave -128..127.

## Alternatives

- The bounds hold for the shipped table only. A modified `DATA/SITES` could
  reach both cases.
- Whether the headquarters site replaces a site after the balance check could
  let a sector's totals exceed 6; its own values are inside the bounds above,
  so the sums still stay far inside a signed byte.

## How to reproduce

Read the 22 records of 62 bytes each as signed 16-bit little-endian fields at
the offsets of FMT-DATA-001, take the `research` of the records whose
`special` is 1 and 2, and the per-field extremes and sums of the three
largest and three smallest values.
