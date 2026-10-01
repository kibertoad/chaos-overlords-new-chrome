---
id: BUG-AI-007
title: Five attack draws test the strength of the record whose slot number is the gang's sector
status: established
builds: [BLD-GOG-EN-1.1]
superseded_by: []
impact: rules
intent: unintended
player_reliance: unknown
evidence: [FND-AI-072, EXP-TURN-022]
conflicting: []
split_with: []
related: [RULE-AI-004, RULE-AI-019, RULE-AI-020, RULE-AI-022, RULE-AI-023, RULE-AI-024, FMT-STATE-001]
---

## Symptom

A computer gang of family 0, 1, 3, 4 or 5 that sees a hostile human gang may
decline or make an attack for reasons unrelated to its own strength or its
target's. The strength test before the attack reads the player's gang record
in the roster slot whose number is the gang's sector, and compares it with a
gang from that record's sector.

## Trigger conditions

A family-0 gang whose previous action was Heal, Hide or Move, or a family-1,
-3, -4 or -5 gang whose previous action was Attack, Hide or Move, stands in a
sector of weight 10 and passes its Heal gate. The single target draw then runs
the strength test.

## Mechanism

The handler passes the local holding the gang's sector where selector `0x2B`
expects a roster slot (FND-AI-072). The selector takes the sector byte of the
record in that slot and looks the drawn ordinal up among the visible gangs of
other players in that sector. When the slot holds no gang, its sector byte is
100, and the gangs found there are gone gangs whose `visible_to` bytes were
still set when they died; their Force byte can be below 0 after combat
(FMT-STATE-001). When there are fewer such gangs than the ordinal, the test
compares with the zero record before the first gang record, and a zero
attacker then passes, since 0 is at most 0.

## Frequency

Every single draw on these branches. In EXP-TURN-022 player 2's family-3 gang
in sector 30 drew the human's gang and tested slot 30, an unused record, against
player 3's gang in slot 1, which had died with Force -4, Combat 8, Defense 5 and
its `visible_to` byte for player 2 set: `(-4 + 8) / 4 - 0 <= 0 + 0 - 5` fails,
so the gang did nothing that turn.

## Player reliance

Unknown. Players cannot see the test, only its result.

## Fixes elsewhere

None known.

## Differences between builds

None known.

## Open questions

The 32 bytes before the first gang record were zero in every recorded run;
what else can write them is not recorded.
