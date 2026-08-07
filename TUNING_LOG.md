# Tuning log

Every tuning change goes in `docs/00_TUNING.md`'s changelog. This file is for the *playtest*
record — what you felt, and what you changed because of it.

---

## Day 3 gate — 2026-08-07

1. Chose not to bank because I wanted a bigger number?   YES
2. Death felt like my fault?                             YES
3. Pressed Retry without deciding to?                    YES
4. Watched the gold CHARGE number, not my character?     YES

**Score: 4/4** → per PRD §6, proceed to M2.

Notes:

```
Subjective first impression was "boring" despite hitting all four gate markers. Diagnosed as
content thinness, not a broken loop: M1 ships one enemy type (Drifter only), no waves, no
upgrades, no boss, and almost no audio/juice by design (CLAUDE.md: "no art, no audio... resist
every urge to make it look nice — that is Day 8"). The bank-tension hook itself is confirmed
working. Not touching any tuning value — the gate passed and CLAUDE.md M1-T15 only prescribes
changes when the score is below 4/4. Proceeding to M2 (waves, Spitter, Splitter, elites,
upgrades, boss) rather than tuning further.
```

### If the score is below 4/4

Change **one lever at a time**, in this order (PRD §6 / M1-T15), replay for 10 minutes, and add
a row below. Do not change code.

| Order | Lever | From | To | Feel for |
|---|---|---|---|---|
| 1 | `Player.chargeLossOnHit` | 0.50 | 0.75 | Does a hit finally *hurt*? |
| 2 | `Bank.duration` | 1.20 | 1.50 | Is the window scary rather than routine? |
| 3 | `Economy.multDecayGrace` | 1.50 | 1.10 | Are you deciding faster? |
| 4 | If it now feels punishing: `Player.maxHP` 3 → 4, **or** `Bank.duration` → 1.00 | | | Adjust *forgiveness*, never *tension* |

---

## Playtest log

| Date | Session | Change made | Why | Result |
|---|---|---|---|---|
| | | | | |
