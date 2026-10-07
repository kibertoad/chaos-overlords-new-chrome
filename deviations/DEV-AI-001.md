# DEV-AI-001

- Departs from: BUG-AI-001, RULE-AI-010
- Reason: In the guards against a second family-6 hire, the rebuild compares the previous hire
  role with role 4 in every scenario. The original compares it with the scenario's schedule slot
  number (6, 5, 2 or 10), which never matches a family-6 hire and in Dominance never matches
  anything. Schedule tables, quotas, ranking and draws are unchanged.
- Setting: None
- Default: mandatory
- Justification: The guard is written to stop a computer player hiring a second hunter straight
  after the first, and compares with a slot number that never matches, so in Dominance it never
  fires and elsewhere it fires after unrelated hires. Correcting the comparison makes the computer
  players follow their own schedule; no player strategy that depends on the original comparison is
  recorded.
- Dropped: 2026-09-26, the correction made no difference that could justify it. In 2,688 headless
  four-year matches of the rebuild, in the seven scenarios with a hunter guard, at every
  Mentality and under both AI policies, with a simulated human in seat 0, the original comparison
  and the corrected one produced identical matches: the hunter force reached its guard only in
  Armageddon, and the scenario's later adjustments overwrote every slot the two comparisons
  disagreed on. With no measured gain, the Fidelity rules keep the original, and the rebuild
  compares with the hunter slot number again (BUG-AI-001, RULE-AI-010). The measurements are in
  the 2026-09-26 entry "Keep the original hunter guard and drop DEV-AI-001" of docs/DECISIONS.md.

The correction applied under the Original AI policy as well. Decided 2026-09-17.
