---
id: FND-AI-007
title: A per-player difficulty band set from the Mentality changes the dice of several resolved actions
status: recorded
builds: [BLD-GOG-EN-1.1]
superseded_by: []
recorded_by: kibertoad
reproduced_by: []
method: static
locations:
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x004A2570..0x004A2588
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x0046DC10
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00472775
  - build: BLD-GOG-EN-1.1
    file: Chaos Overlords.exe
    address: 0x00475F70
tool: Ghidra 12.1.3
environment: null
---

## Observation

New-match initialization fills a six-entry integer table at `0x004A2570`. Every
entry starts at 1. At Mentality 0 (Goon) the entries of computer-controlled
players become 0; at 1 (Criminal) all stay 1; at 2 (Crime Lord) and 3
(Homicidal Maniac) the entries of computer-controlled players become 2. Human
players stay at 1 at every Mentality.

The whole-turn resolver `0x00472775` reads the table nine times. Its helper
`0x00475F70(pool, threshold)` rolls `pool` dice with `0x0045D227(6)` and counts
the results at or above the threshold. The nine reads:

- Heal rolls `Heal + 4` dice; bands 0 and 1 succeed on 5 or more, band 2 on 4
  or more. Successes add to Force, capped at 10.
- Influence rolls `Force + Influence` dice. Band 0 removes `pool / 5`
  (truncated) dice and succeeds on 5 or more; band 1 uses the full pool at 5
  or more; band 2 the full pool at 4 or more.
- Research rolls `Force + Research` dice. Band 0 removes `pool / 5` dice and
  succeeds on 6; band 1 uses the full pool at 6; band 2 the full pool at 5 or
  more.
- Each Chaos gang rolls `Income + Force + Chaos` dice, where Income is the
  sector's. Band 0 removes a fifth of the pool and succeeds on 5 or more, band
  1 uses the full pool at 5 or more, band 2 the full pool at 4 or more. When a
  band-2 player owns that sector, only `successes - successes / 4` counts
  toward the Crackdown comparison.
- A hidden target evades when one wrapper roll with bound 20 is below
  `Stealth + 14 - Detect` for attacker bands 0 and 1, or
  `Stealth + 10 - Detect` for band 2.
- The main attack lowers a band-0 defender's Defense by a quarter, then rolls
  `Force + Combat - adjusted Defense` dice at 6, 5 or 4 or more for attacker
  bands 0, 1 and 2. A positive pool deals at least `pool / 4` damage.
- Retaliation first requires the target's action byte not to be 8 (Hide). It
  is then allowed when the attacker's effective Martial Arts is 0, when the
  attacker has a weapon, or when the defender has positive effective Martial
  Arts and no weapon. Allowed retaliation rolls its pool at 5 or more for
  defender bands 0 and 1 and 4 or more for band 2, and halves the successes
  (truncated).

The operands are gang record fields: +7 is the action byte, selector `0x39`
reads +4 (the weapon, -1 for none) and selector `0x58` reads +31, the last of
the fourteen effective statistics, Martial Arts. The attack block uses the
same offsets on its copy of the attacker and on the live target record.

## Interpretation

The band is a per-player handicap or bonus to the dice: Goon computers roll
worse than humans, Crime Lord and Homicidal Maniac computers roll better, and
Criminal computers roll like humans. It changes resolved outcomes, not only
planning.

## Alternatives

None of the nine reads carries an instruction address in the older notes; they
are described by their place in the resolver. Whether "removes a fifth" in the
Chaos case truncates like the Influence and Research cases is assumed.

## How to reproduce

Find the references to `0x004A2570`: the writes are in the new-match
initializer `0x0046DC10`, the reads in `0x00472775`. The dice helper
`0x00475F70` calls `0x0045D227` with 6 in a loop.
