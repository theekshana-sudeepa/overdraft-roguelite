# M2 — Content: enemies, waves, upgrades, boss (Days 4–7)

**Gate:** a full 6-wave + boss run is completable, and the same seed reproduces the same run on both platforms.
**Precondition:** M1 gate passed 4/4. If it didn't, go back.

Still no art pass, no audio, no menus. Primitives and raw text.

---

## Day 4 — Enemies & generation (~5h)

### M2-T01 · `SpitterAI` (FR-302)
Maintains `preferredRange` (9.0) from the player, strafing perpendicular. Fires an aimed projectile every `fireInterval`. On death spawns a pooled damaging puddle for `puddleDuration`.

**AC:** the spitter never closes to melee; the puddle is pooled (no `Instantiate`); puddle damage respects player invuln.

### M2-T02 · `SplitterAI` (FR-303)
Approaches slowly, then a `windupDuration` (0.40s) **visible telegraph** — scale pulse + colour flash — then charges at `moveSpeed` for `chargeDuration`. On death spawns `splitCount` (2) Drifters scattered within `splitScatterRadius`.

⚠️ Split Drifters must **not** themselves split. If the drifter pool is exhausted, skip the split **silently** — never grow the pool (`docs/01_ARCHITECTURE.md` §6).

**AC:** the telegraph is readable enough to dodge on first encounter; QA case 40 (pool exhaustion) produces no null-ref.

### M2-T03 · Elite variants (FR-304)
A `bool isElite` flag on the pooled instance applying the multipliers from `docs/00_TUNING.md`, plus `M_Outline` at `outlineWidth` 0.06. **No new prefabs, no new AI.**

**AC:** an elite is instantly distinguishable at a glance, and no new prefab asset was created.

### M2-T04 · `WaveGenerator` — pure & deterministic (FR-452, FR-453, FR-454)
Implement in `Generation/` per the contract in `docs/01_ARCHITECTURE.md` §6. Budget spend loop, weighted selection, 4 clump patterns.

⚠️ **No `using UnityEngine`. No `UnityEngine.Random`. No seed from `string.GetHashCode()`.** Fill a caller-owned `List<SpawnEvent>` — allocate nothing per wave.

**AC:** every test in `GenerationTests` green, including `Generate_AllocatesNothing_AfterWarmup`.

### M2-T05 · `RunSeed` channels
FNV-1a hash combine, 5 channels per `docs/01_ARCHITECTURE.md` §6. Channel independence is what lets you add systems later without invalidating old daily seeds.

**AC:** `RunSeed_Channels_AreIndependent` green.

### M2-T06 · Golden determinism test
Generate 1000 runs, hash to one SHA-256, commit as `Tests/EditMode/golden_determinism.txt`. **Run it on macOS and Windows and confirm they match.**

**AC:** `Determinism_HashOf1000Runs_MatchesGolden` green on both platforms. If it diverges, stop everything — the daily leaderboard is the game's retention feature and this is a P0.

---

## Day 5 — Run structure & upgrades (~5h)

### M2-T07 · `RunController` state machine (FR-451)
Implement the state machine in `docs/01_ARCHITECTURE.md` §5. Owns the `EnemyKilled` handler that calls multiplier-then-charge in order.

**AC:** a run progresses Intro → 6 waves → boss → results with no stuck states; wave timeout works.

### M2-T08 · THE VAULT environment (FR-451, FR-457)
Second arena prefab in the **same scene**: `vaultRadius` 14, `vaultOrthoSize` 10, warm palette, 3 rotating hazard bars on a fixed 4s cycle with `hazardTelegraphTime` (0.8s) warning.

⚠️ Hazard timing is **deliberately not seeded** — players must be able to learn it.
⚠️ Environment swap is `SetActive` + colour-grade lerp. **No scene load, no loading screen.**

**AC:** transition into wave 4 is seamless and under one frame of hitch.

### M2-T09 · Upgrade system (FR-471 – FR-474)
4 lines × 3 tiers, tiers taken in order, maxed lines excluded from offers. Offers generated from seed channel 2. Between-wave choice screen — functional, ugly, keyboard + gamepad navigable.

**AC:** a full run offers 5 upgrade choices; an already-maxed line never appears; offers are reproducible from the seed.

### M2-T10 · Implement the 4 upgrade effects
Values in `docs/00_TUNING.md`.
- **SPREAD:** extra projectiles in a cone, T3 applies ×0.80 damage
- **VOLATILE:** explosion on kill; chain depth capped at 40 iterations/frame; `FromExplosion` flag prevents accounting recursion
- **LEECH:** shorter `EffectiveDuration`, T2 dash refund, T3 bank i-frames
- **GREED:** mult gain/decay changes, T2 removes the cap, T3 sets `chargeLossOnHit` to 1.00

⚠️ **GREED T3 + LEECH T3 is a known degenerate combo. Do not balance it away (FR-474).** The community discovering it is free marketing.

**AC:** each tier is individually verifiable; `LeechTiers_ReduceBankDuration_Correctly` and `GreedT3_DoublesChargeLossToFull` green.

---

## Day 6 — THE AUDITOR (~5h)

### M2-T11 · Boss shell & phases (FR-401)
Hexagonal prism in ProBuilder (~200 tris). 900 HP, phases at 66%/33%, `phaseTransitionInvuln` 1.0s with a telegraph flash.

**AC:** phases transition cleanly; QA case 2 (bank at the exact phase-change frame) passes.

### M2-T12 · P1 — laser sweep (FR-402)
6 spokes rotating at `laserRotationSpeed`, damaging on contact. Spawns drifters from vertices every `drifterSpawnInterval`. **Sweep direction comes from seed channel 3** so a daily run is identical for everyone.

**AC:** the pattern is learnable within two attempts; direction is seed-reproducible.

### M2-T13 · ★ P2 — THE AUDIT (FR-403)
Core opens every `auditInterval` (9s) for `auditWindowDuration` (4s), draining `auditDrainRate` (8%) of unbanked CHARGE **per second**. Core is the only vulnerable point during a window.

⚠️ Drains **CHARGE only, never banked SCORE.** This is the boss's whole design idea: it attacks the resource, not the health bar, forcing the player to bank during the hardest fight in the game.

**AC:** `Audit_DrainsChargeOnly_NeverScore` green; the drain visibly and audibly panics the player.

### M2-T14 · P3 — fragments & the free bank (FR-404, FR-405)
Shatter into 6 orbiting fragments (60 HP each) with continuous auditing. Final fragment → `victorySlowMoDuration` (1.5s at timeScale 0.25) → **forced free bank with no vulnerability window** → results.

That forced bank is the exhale at the end of the run. Get the timing right; it's the emotional payoff of the whole design.

**AC:** boss defeat always banks remaining charge; run ends successfully.

---

## Day 7 — Modes, tuning, buffer (~5h)

### M2-T15 · Three run modes (FR-455)
- **ENDLESS** — random seed
- **DAILY** — `seed = (int)(DateTime.UtcNow.Date - new DateTime(2026,1,1)).TotalDays`, one attempt, enforced by `SaveData.lastDailySeedPlayed`
- **CUSTOM SEED** — user-entered, parsed as `int`, gated behind Credit Rank 2

⚠️ `DateTime.UtcNow.Date` **only**. Local time splits the leaderboard across timezones and you will discover it on launch day.
⚠️ Custom seed parses to `int` directly — **never** `string.GetHashCode()` (process-randomised, non-reproducible).

**AC:** all `DailyTests` green; QA cases 11–13 pass.

### M2-T16 · RISK tiers (FR-483)
Enemy speed, budget, and score multipliers from the `RiskTier[]` table. Tiers are selectable but gated by Credit Rank (gating lands in M4).

**AC:** `RiskTier_MultipliersApplyToScoreAndSpeed` green.

### M2-T17 · Balance pass — the important one
Play 10+ full runs. Target a run of **330–420 seconds** at RISK I (G3). Tune wave budgets, enemy values, and boss HP.

Log every change in the `docs/00_TUNING.md` changelog. **Never change a value silently.**

**AC:** 5 consecutive runs land in the 330–420s window; the changelog has rows.

### M2-T18 · Buffer
Fix whatever M2 broke. If nothing broke, do QA cases 9–13 and 35–40 early.

---

## Definition of Done for M2

- [ ] `./tools/verify.sh` ✅ PASS
- [ ] `GenerationTests`, `EconomyTests`, `DailyTests` fully green
- [ ] Golden determinism test matches on **both** macOS and Windows
- [ ] A full run (6 waves + boss) is completable end to end
- [ ] Runs land in 330–420s at RISK I
- [ ] QA cases 1–13 pass
- [ ] Zero GC allocation per frame at peak enemy count
- [ ] Tagged `m2-content`

## Explicitly NOT in M2

Post-processing · shaders · audio · menus · results screen UI · saving · Steam · localisation · meta progression. Those are M3 and M4. The game should still look like a programmer-art prototype at the end of Day 7 — and it should already be fun.
