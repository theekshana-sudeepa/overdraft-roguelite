# OVERDRAFT — Product Requirements Document
**v1.0 · 2026-08-06 · Owner: Thikka (solo) · Target ship: 2026-09-17**

Requirement IDs are stable. Never renumber them — task files, tests, and the QA matrix reference them.

- `FR-xxx` functional requirement
- `NFR-xxx` non-functional requirement
- `AC` acceptance criterion (testable, binary)

---

## 1. Product summary

A 6-minute-per-run top-down twin-stick roguelite about **push-your-luck score banking**. Kills produce **CHARGE**, which is worth nothing until the player stands perfectly still for 1.2 seconds to convert it into **SCORE**. Taking a hit destroys half of it.

**Positioning:** $6.99 impulse-priced arcade roguelite for the Brotato / Nova Drift / Vampire Survivors audience, differentiated by a single novel verb (BANK) that turns every second of play into a risk decision.

**Platforms:** Windows x64, macOS Universal (Apple Silicon + Intel). Steam only at launch.

---

## 2. Goals

| ID | Goal | Measure |
|---|---|---|
| G1 | A player who dies immediately presses Retry | Median time from death to next run start **< 2.0s**; ≥70% of deaths in playtest followed by an immediate retry |
| G2 | The bank decision is genuinely tense | In playtest, ≥1 instance per session of a player verbally reacting to a failed or narrowly-saved bank |
| G3 | A single run is short enough to be repeatable | Full run (6 waves + boss) completes in **330–420 seconds** at RISK I |
| G4 | Looks premium in a 5-second store-page glance | A still frame is readable, high-contrast, and free of untextured-grey-primitive appearance |
| G5 | Runs everywhere | 60 fps sustained on M4 @1440p and Intel UHD 620 @1080p |
| G6 | Ships on time | `1.0.0-rc1` tagged by 2026-08-27 |

---

## 3. Non-goals — out of scope, permanently

Anything in this list must not be built, prototyped, stubbed, or designed for.

| ID | Non-goal |
|---|---|
| NG1 | Online multiplayer, co-op, PvP, or any networking beyond Steam's leaderboard/cloud APIs |
| NG2 | Procedurally generated level geometry. Two hand-authored arenas exist. |
| NG3 | Mid-run save/resume |
| NG4 | Story, lore, named characters, cutscenes, dialogue system |
| NG5 | Animated sprite sheets or skeletal animation. All motion is transform, scale, colour, and shader. |
| NG6 | Physics-driven movement (`Rigidbody`, `CharacterController`) |
| NG7 | More than 3 base enemy archetypes (recolour/scale variants are permitted) |
| NG8 | More than 1 boss |
| NG9 | Shops, currency drops, item rarity tiers, inventory UI |
| NG10 | Mod support, Steam Workshop, user-generated content |
| NG11 | Console platforms, mobile, Linux native build (Proton is fine and free) |
| NG12 | Analytics/telemetry SDKs of any kind (triggers GDPR obligations for zero launch benefit) |
| NG13 | Custom key-rebinding UI beyond the New Input System's stock rebind prefab |
| NG14 | Any achievement beyond the 10 specified in FR-500 |

---

## 4. Functional requirements

### 4.1 Player (FR-100)

| ID | Requirement | AC |
|---|---|---|
| FR-101 | Player moves in 8+ directions from left stick or WASD, with acceleration and deceleration per `Tuning.Player`. | Holding a direction reaches full speed within 0.15s; releasing stops within 0.12s. Diagonal speed equals cardinal speed (normalised input). |
| FR-102 | Player aims independently of movement, via right stick or mouse position. Aim direction drives the weapon and the character's facing rotation. | Facing rotation lerps to aim at ≥720°/s. Mouse aim uses a ray to the arena plane, not screen delta. |
| FR-103 | Weapon **auto-fires** continuously at `Tuning.Weapon.fireInterval`. There is no fire button. | No input binding exists for firing. Firing continues while banking at `bankFireRateMultiplier`. |
| FR-104 | Player has `maxHP` (3) hit points shown as discrete pips. Contact with an enemy or enemy projectile costs 1 HP. | Damage applies at most once per `hitInvulnDuration` regardless of overlap count. |
| FR-105 | Taking damage removes `chargeLossOnHit` (50%) of current unbanked CHARGE and resets MULT to 1. | Verified by test `PlayerHit_HalvesCharge_AndResetsMultiplier`. |
| FR-106 | Player has a dash: fixed distance, i-frames, 2 charges, independent cooldowns. | Dash cannot be used while banking. Charges refill independently, not as a shared timer. |
| FR-107 | **Perfect Dash:** dashing such that i-frames overlap an incoming projectile within `perfectDashWindow` grants `+perfectDashMultBonus` MULT and triggers hitstop. | Fires at most once per dash regardless of projectile count. Test: `PerfectDash_AwardsBonusOnce`. |
| FR-108 | Player hitbox radius is smaller than the player's visual radius (0.35 vs 0.50). | Documented in tuning; deliberate generosity. Do not "fix" this. |

### 4.2 Economy — the core loop (FR-200)

| ID | Requirement | AC |
|---|---|---|
| FR-201 | Each enemy killed adds `enemyValue × MULT` to **CHARGE**, rounded down to a whole number. | Test: `Kill_AddsValueTimesMultiplier`. |
| FR-202 | Each kill adds `multPerKill` to **MULT**. MULT is capped at `multMax` unless the GREED T2 upgrade removes the cap. | Test: `Multiplier_RespectsCap_UnlessUncapped`. |
| FR-203 | MULT decays at `multDecayRate` per second after `multDecayGrace` seconds elapse without a kill. It never falls below 1.0. | Test: `Multiplier_DecaysAfterGrace_FloorsAtOne`. |
| FR-204 | CHARGE contributes **nothing** to final score unless banked. | Test: `RunEnd_UnbankedCharge_ContributesZero`. |
| FR-205 | **BANK:** holding the bank input for `bankDuration` transfers all CHARGE to SCORE, sets CHARGE to 0, and resets MULT to 1. | Test: `Bank_TransfersFullChargeAndResetsMultiplier`. |
| FR-206 | While banking, the player cannot move and cannot dash. The weapon fires at `bankFireRateMultiplier` of normal rate. | Movement input is consumed but produces zero velocity. |
| FR-207 | Releasing the bank input before completion cancels it with **no penalty and no partial credit**. Progress resets to zero. | — |
| FR-208 | Taking damage during a bank interrupts it, applies the normal hit penalty, and starts `bankInterruptCooldown` during which banking cannot begin. | Test: `BankInterrupted_AppliesHitPenalty_AndLocksOut`. |
| FR-209 | On successful bank, SCORE increases by the banked amount multiplied by the RISK tier multiplier. | Score is stored as `long`. Test: `Bank_AppliesRiskMultiplier`. |
| FR-210 | The HUD displays CHARGE as the single largest element on screen, in the BANK gold colour, pulsing at a rate proportional to MULT. | Verified visually against `docs/04_ART_AUDIO_SPEC.md`. |

### 4.3 Enemies (FR-300)

| ID | Requirement | AC |
|---|---|---|
| FR-301 | **DRIFTER** — moves directly toward the player at constant speed. 1 HP. Value 10. | No pathfinding, no avoidance, no separation steering beyond a cheap overlap push. |
| FR-302 | **SPITTER** — maintains a preferred distance band from the player, strafing; fires an aimed slow projectile every `fireInterval`. 3 HP. Value 40. On death leaves a damaging puddle for 2s. | Puddle is pooled, not instantiated. |
| FR-303 | **SPLITTER** — charges the player fast after a `windupDuration` telegraph. 2 HP. Value 25. On death spawns `splitCount` (2) Drifters at its position. | Split Drifters do not themselves split. Split spawn respects the enemy pool cap; if the pool is exhausted, the split is skipped silently. |
| FR-304 | **Elite variants** exist for all three types: stat multipliers + 1.4× scale + a distinct outline colour. No new prefabs, no new AI. | Elites are a flag on the pooled instance, not a separate prefab. |
| FR-305 | All enemy stats come from an `EnemyDefinition` ScriptableObject. | No stat is hard-coded in an AI script. |
| FR-306 | Enemies collide with the player via squared-distance circle overlap only. No Unity physics. | `CircleOverlap` utility used everywhere. |

### 4.4 Boss — THE AUDITOR (FR-400)

| ID | Requirement | AC |
|---|---|---|
| FR-401 | Spawns after wave 6 is cleared. `bossHP` total, three phases at 100–66%, 66–33%, 33–0%. | Phase transitions are instant and telegraphed with a 1.0s invulnerable flash. |
| FR-402 | **P1:** rotating 6-spoke laser sweep at `laserRotationSpeed`; spawns Drifters from its vertices on an interval. | Lasers damage on contact; the sweep direction is seed-determined so a daily run is identical for everyone. |
| FR-403 | **P2:** periodically opens its core and **AUDITS** — draining `auditDrainRate` (8%) of the player's unbanked CHARGE per second for `auditWindowDuration` (4s), every `auditInterval` (9s). Core is the only vulnerable point during a window. | Drain applies to CHARGE only, never to banked SCORE. Test: `Audit_DrainsChargeOnly`. |
| FR-404 | **P3:** boss shatters into 6 orbiting fragments, each with `fragmentHP`; auditing is continuous. | Destroying the final fragment triggers 1.5s of slow-motion and a **forced free bank** with no vulnerability window. |
| FR-405 | Boss defeat ends the run successfully and opens the results screen. | — |

### 4.5 Run structure and generation (FR-450)

| ID | Requirement | AC |
|---|---|---|
| FR-451 | A run is: intro → 6 waves (upgrade choice between each) → boss → results. Waves 1–3 in THE FLOOR, waves 4–6 + boss in THE VAULT. | Environment swap is `SetActive` + a colour-grading lerp. No scene load, no loading screen. |
| FR-452 | Wave contents are generated from a single 32-bit seed via a **pure, deterministic** function with no `UnityEngine.Random` anywhere in the path. | Test: `SameSeed_ProducesIdenticalWaves` across 1000 seeds; `Generation` asmdef does not reference `UnityEngine`. |
| FR-453 | Wave enemy composition is a budget spend: `budget = (60 + wave^1.6 × 22) × riskMultiplier`, filled by weighted selection from the wave's allowed enemy set. | Test: `WaveBudget_NeverExceeded_AndNearlyFullySpent`. |
| FR-454 | Spawn timing uses one of 4 hand-authored **clump patterns**, chosen by seed. Uniform distribution is explicitly forbidden — clumps create the multiplier spikes. | — |
| FR-455 | Three modes off one generator: **ENDLESS** (random seed), **DAILY** (seed = UTC days since 2026-01-01, one attempt per day), **CUSTOM SEED** (user-entered, unlocked at Credit Rank 2). | Daily seed uses `DateTime.UtcNow.Date` only. Test: `DailySeed_IsUtcStable`. |
| FR-456 | A wave ends when all its enemies are dead, or after `waveTimeout`, whichever is first. | — |
| FR-457 | THE VAULT contains 3 rotating laser hazard bars on a fixed, seed-independent 4s cycle. | Deliberately not seeded — players must be able to learn it. |

### 4.6 Upgrades (FR-470)

| ID | Requirement | AC |
|---|---|---|
| FR-471 | Between waves the player is offered **3 of the available upgrade tiers** and must pick 1. | Offers are seed-determined (channel 2), never `UnityEngine.Random`. |
| FR-472 | Four upgrade lines × 3 tiers each: SPREAD, VOLATILE, LEECH, GREED. Tiers must be taken in order. | An already-maxed line is excluded from offers. If fewer than 3 valid offers remain, offer what exists. |
| FR-473 | SPREAD: +projectiles and cone width. VOLATILE: kill explosions, then chaining. LEECH: shorter bank duration, then bank i-frames. GREED: faster MULT gain and higher ceiling at the cost of faster decay and double charge loss. | Exact values in `docs/00_TUNING.md`. |
| FR-474 | GREED T3 + LEECH T3 is a known degenerate combo. **Do not balance it away before launch.** | Explicit non-fix. |

### 4.7 Progression (FR-480)

| ID | Requirement | AC |
|---|---|---|
| FR-481 | **CREDIT RANKS:** one linear track, 12 nodes, advanced by lifetime banked score. | Thresholds in `docs/00_TUNING.md`. |
| FR-482 | Unlocks: 3 starting loadouts, 5 RISK difficulty tiers, 1 run modifier (Glass), 2 cosmetics, Custom Seed mode. | No branching, no respec, no currency spend. |
| FR-483 | RISK tiers apply an enemy-speed multiplier and a score multiplier, and have separate leaderboard visibility. | Test: `RiskTier_MultipliersApplyToScoreAndSpeed`. |

### 4.8 Steam (FR-500)

| ID | Requirement | AC |
|---|---|---|
| FR-501 | `SteamAPI.RestartAppIfNecessary` on launch in non-editor builds. | `steam_appid.txt` excluded from shipping depots. |
| FR-502 | 10 achievements exactly as specified in `docs/03_STEAM.md`. Cached locally; pushed with a single debounced `StoreStats()` at run end. | Test: `Achievements_BatchedToOneStoreCall`. |
| FR-503 | 4 leaderboards: `ENDLESS_ALLTIME`, `RISK_V`, `DAILY_<seed>`, `BOSS_TIME`. Friends filter shown first on the results screen. | — |
| FR-504 | Steam Cloud via Auto-Cloud on `save.json`, with a user-facing conflict prompt when the cloud copy is newer. | Never silently overwrites. |
| FR-505 | Rich Presence showing mode, wave, and unbanked charge. | Localisation token file provided. |
| FR-506 | **Every Steam call is a no-op when `Steam.Ready` is false, and the game remains fully playable.** | Test: `AllSteamCalls_NoOpWhenUninitialized`. Manual: play a full run with Steam killed. |
| FR-507 | Leaderboard submissions include a `RunProof` details blob (seed, duration, kills, upgrade flags). Client-side filtering hides entries with impossible score-per-second ratios. | This is integrity, not anti-cheat. Never block play or accuse a user. |

### 4.9 Save (FR-520)

| ID | Requirement | AC |
|---|---|---|
| FR-521 | Single JSON file `save.json` in `Application.persistentDataPath`, schema per `docs/02_DATA_SCHEMAS.md`. | — |
| FR-522 | **Atomic write:** write to `save.tmp`, then `File.Replace`. | Test: `Save_IsAtomic_UnderSimulatedFailure`. |
| FR-523 | Written on run end, settings change, focus loss, and quit. Never per-frame. | — |
| FR-524 | A corrupt, empty, truncated, or future-version save loads defaults without crashing and without deleting the user's file (rename it to `save.corrupt.json`). | Test: `Load_RecoversFrom_Corrupt_Empty_FutureVersion`. |
| FR-525 | Salted SHA-256 checksum. A mismatch suppresses **leaderboard submission only** — unlocks and progress are preserved. | Test: `TamperedSave_KeepsProgress_BlocksLeaderboard`. |

### 4.10 UI, input, localisation (FR-540)

| ID | Requirement | AC |
|---|---|---|
| FR-541 | Every menu is fully navigable by gamepad and by mouse, simultaneously, with no mode toggle. | Manual: complete a full session using only a gamepad, then only a mouse. |
| FR-542 | HUD: CHARGE (large, gold), MULT + decay bar, SCORE, wave counter, HP pips, dash charges, bank ring drawn around the player. | Per `docs/04_ART_AUDIO_SPEC.md`. |
| FR-543 | 8 languages: en, zh-Hans, ru, pt-BR, es-419, de, fr, ja, ko. Strings in a CSV; TMP fallback fonts for CJK and Cyrillic. | No tofu glyphs in any language. German and Russian must not overflow any button. |
| FR-544 | Options limited to: master/music/SFX volume, screen-shake scale, colourblind palette, quality preset, fullscreen mode, language. Nothing else. | — |
| FR-545 | Results screen shows banked score, run time, kills, friends leaderboard, global percentile, and a one-press retry. | Retry re-enters gameplay in **< 2.0s**. |
| FR-546 | Controller hot-plug and disconnect mid-run must not soft-lock the game. | Handle `InputSystem.onDeviceChange`. Manual test required. |

---

## 5. Non-functional requirements

| ID | Requirement | Verification |
|---|---|---|
| NFR-01 | 60 fps sustained, frame time ≤ 8 ms, on M4 @1440p and Intel UHD 620 @1080p | Profiler capture on both, 5-minute sample |
| NFR-02 | **Zero** GC allocation per frame in steady-state gameplay | Profiler, 60-second capture at peak enemy count |
| NFR-03 | **Zero** runtime `Instantiate`/`Destroy` after warm-up | Static grep in `verify.sh` + Profiler |
| NFR-04 | Draw calls < 150; SetPass calls < 20; ≤ 12 materials in the project | Frame Debugger |
| NFR-05 | Peak RAM < 500 MB; build size < 120 MB | Build report |
| NFR-06 | Cold start to main menu < 4 s | Stopwatch on both platforms |
| NFR-07 | Gameplay logic deterministic at fixed 60 Hz; identical seed → identical wave data on Windows and macOS | Cross-platform test: hash 1000 generated runs, compare files |
| NFR-08 | No exception in the player log across 10 consecutive full runs on each OS | Manual, per-milestone |
| NFR-09 | macOS build is Universal, Metal-only, signed with Developer ID, notarized and **stapled** | `spctl -a -vvv -t install` returns *Notarized Developer ID* |
| NFR-10 | Windows build is IL2CPP x86_64, DX11-only, Master configuration | Build settings audit |
| NFR-11 | Compile time after a single-script change < 5 s | Asmdef structure per CLAUDE.md §5 |

---

## 6. Milestone gates

Each gate is binary. Do not proceed past a failed gate.

| Milestone | Day | Gate condition |
|---|---|---|
| **M0** Setup | 1 | Project compiles, input works on both device types, `verify.sh` runs green |
| **M1** Core loop | 3 | ★ **The Day-3 test** (below) passes 4/4 |
| **M2** Content | 7 | Full 6-wave + boss run completable; same seed reproduces the same run |
| **M3** Juice | 10 | A single screenshot is store-page quality; both environments visually distinct |
| **M4** Meta + Steam | 14 | Achievements, leaderboards, and cloud save all verified in a real Steam client |
| **M5** Ship | 21 | `1.0.0-rc1` on a Steam beta branch, both platforms, notarized Mac |

### ★ The Day-3 gate (M1)

Play the build for 10 minutes without touching the editor, then answer honestly:

1. Did you at any point **choose not to bank** because you wanted a bigger number?
2. Did a death feel like **your fault** rather than unfair?
3. Did you press Retry **without deciding to**?
4. Did you catch yourself watching the **gold CHARGE number** instead of your character?

**4/4 → proceed to M2.** Fewer → spend Day 4 tuning `bankDuration`, `chargeLossOnHit`, `multDecayGrace`, and `multDecayRate` until it's 4/4. Nothing in M2–M5 can rescue a loop that fails this gate.

---

## 7. Risk register

| Risk | Impact | Mitigation |
|---|---|---|
| The bank loop isn't fun | Project-fatal | The Day-3 gate exists precisely to surface this on day 3, not day 18. Tuning levers pre-identified in §6. |
| macOS notarization fails late | Cannot ship Mac | Do a full sign+notarize dry run on **Day 17**, not Day 21. Apple Developer enrolment can take 48h — start it Day 1. |
| Determinism breaks between platforms | Daily leaderboard is invalid | `Generation/` asmdef forbids `UnityEngine`; cross-platform hash test in the suite from M2 onward. |
| Scope creep during M3 juice work | Misses feature freeze | `NG` list is enforceable; `POST_LAUNCH.md` absorbs every idea. |
| CJK font atlas balloons build size | Fails NFR-05 | Dynamic SDF atlases for CJK, static for Latin. Budget 25 MB, measure at M5. |
| Steam build review rejects | Slips launch | Submit build for review by **2026-09-08**, giving 7 days of slack before the 09-17 date. |

---

## 8. Open decisions (owner: Thikka, not the agent)

| # | Decision | Deadline |
|---|---|---|
| D1 | Participate in the next Steam Next Fest (delays launch ~6 weeks, likely 3–8× wishlists) or ship 2026-09-17 | Before store page submission, 2026-08-20 |
| D2 | Music: licensed royalty-free pack (~$25) vs CC-BY with attribution ($0) | Day 15 |
| D3 | Final launch price if regional feedback suggests $4.99 | Day 19 |

The agent must not act on these. Flag them if they become blocking.
