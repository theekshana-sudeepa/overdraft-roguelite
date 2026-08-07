# M0 + M1 — Setup & Core Loop (Days 1–3)

**Gate:** the Day-3 test in `PRD.md` §6 must pass **4/4**.
**Everything in M2–M5 is worthless if this gate fails.** Build only what is here.

---

## M0 — Project setup (Day 1, ~2h)

### M0-T01 · Create the Unity project
Unity **2022.3 LTS**, 3D (URP) template.
- Player Settings → **Color Space: Linear**, API Compatibility **.NET Standard 2.1**
- Windows: **IL2CPP**, x86_64 only, **DX11 only** (remove Vulkan)
- macOS: **IL2CPP**, Architecture **Intel 64-bit + Apple Silicon**, **Metal only** (remove OpenGL)
- Editor → **Force Text** serialisation, **Visible Meta Files**
- Delete the default Directional Light and disable shadows in both URP assets
- Fixed timestep `0.01666667`, `Time.maximumDeltaTime = 0.1`

**AC:** empty project builds for both targets with zero errors.

### M0-T02 · Assembly definitions
Create four asmdefs per `docs/01_ARCHITECTURE.md` §1. `Overdraft.Generation` must have **no** Unity references — uncheck "Override References" and clear the assembly reference list entirely.

**AC:** `grep -rn "UnityEngine" Assets/_Project/Scripts/Generation/` returns nothing, and the project compiles.

### M0-T03 · Folder structure + git
Create the tree from `CLAUDE.md` §6. Unity `.gitignore` + Git LFS for `*.wav *.ogg *.png *.psd *.mp4`. Initial commit.

**AC:** `git status` clean after a fresh editor open (no untracked `Library/`).

### M0-T04 · `TuningConfig` + `Palette`
Implement `TuningConfig` exactly as specified in `docs/00_TUNING.md`, create the single asset at `Data/TuningConfig.asset`, populate **every** field with the documented starting values. Implement `Palette` from `docs/04_ART_AUDIO_SPEC.md` §1.

**AC:** every value in `docs/00_TUNING.md` appears in the asset; no gameplay script contains a numeric literal other than 0/1/-1.

### M0-T05 · `GameEvents` + `ServiceLocator` + `Bootstrap`
Per `docs/01_ARCHITECTURE.md` §3–4. `Bootstrap` lives in `00_Boot`, sets fixed timestep, installs services, loads `01_Menu` additively.

⚠️ Include `GameEvents.ClearAll()` and an editor domain-reload guard — Unity 2022 defaults to disabled domain reload and static events **will** survive play sessions and fire into destroyed objects.

**AC:** entering and exiting play mode 5× in a row produces no null-reference warnings.

### M0-T06 · Input actions
One `.inputactions` asset with the `Gameplay` and `UI` maps from `docs/01_ARCHITECTURE.md` §9. Implement last-used-device tracking via `InputSystem.onEvent`.

**AC:** WASD+mouse and a gamepad both drive `Move` and `Aim` with no mode toggle, and switching device mid-session works instantly.

### M0-T07 · `ObjectPool<T>` + tests
Per the contract in `docs/01_ARCHITECTURE.md` §6. `Get()` returns `null` on exhaustion — do **not** grow the pool.

**AC:** `PoolTests` green — pre-warm allocates once, `Get`/`Return` allocate nothing, double-`Return` is safe, exhaustion returns null.

### M0-T08 · `verify.sh` / `verify.bat`
Create `tools/verify.sh` exactly as in `docs/05_QA_AND_VERIFY.md` §1, plus the Windows mirror. `chmod +x`.

**AC:** `./tools/verify.sh` runs end-to-end and prints ✅ PASS on the empty project.

---

## M1 — The core loop (Days 1–3, ~13h)

> Build order below encodes dependencies. Do not reorder.
> **No art. No audio. No menus. No Steam. No saving.** Untextured primitives and raw `TextMeshPro` only. Resist every urge to make it look nice — that is Day 8.

### M1-T01 · Arena + camera
Flat quad `floorRadius` (18) with `M_Grid_Floor`, emissive boundary ring. Orthographic camera, `floorOrthoSize` (12), top-down, follows `player + aimDirection * 0.25` at `cameraFollowLerp`.

**AC:** the arena fills the frame at 16:9 and 21:9 with no dead space; the player cannot leave the boundary.

### M1-T02 · `PlayerController` — move & aim (FR-101, FR-102)
`FixedUpdate` movement with accel/decel from `Tuning.Player`. Normalised input so diagonals are not faster. Aim from mouse (screen→arena-plane ray) or right stick; facing lerps at `facingTurnSpeed`.

**AC:** full speed within 0.15s, stop within 0.12s, diagonal speed == cardinal speed, both input methods feel equivalent.

### M1-T03 · `Weapon` — auto-fire (FR-103)
Fires every `fireInterval` from a pooled projectile set. `baseSpreadDegrees` jitter. Projectiles travel, expire at `projectileLifetime`, return to pool.

**AC:** no `Instantiate` at runtime; 512 projectiles alive allocates zero bytes per frame.

### M1-T04 · Spatial grid + `CircleOverlap` (FR-306)
Uniform grid, cell size 2.0, rebuilt each `FixedUpdate` into pre-allocated int buckets. **No Unity physics, no colliders.**

**AC:** 300 enemies × 500 projectiles resolves in < 0.4 ms with zero allocation (Profiler).

### M1-T05 · `DrifterAI` + `EnemySpawner` (FR-301)
Drifter walks straight at the player. `EnemyDefinition` asset drives all stats. Spawner emits on a ring at `spawnRingMin..Max`, endless stream for now (waves come in M2).

**AC:** killing a drifter returns it to the pool and raises `GameEvents.EnemyKilled` exactly once.

### M1-T06 · Kill feedback: hitstop + screen shake
`Hitstop.Freeze` and `ScreenShake.AddTrauma` from `docs/04_ART_AUDIO_SPEC.md` §5. Shake applied in `LateUpdate` to a **camera child**, driven by `trauma²`.

⚠️ Do this **now, not in M3.** Without hitstop the Day-3 gate is untestable — killing must feel good before the greed loop can be judged.

**AC:** killing feels impactful with untextured primitives and no sound.

### M1-T07 · `MultiplierSystem` (FR-202, FR-203)
Per the contract in `docs/01_ARCHITECTURE.md` §6. Gain on kill, grace period, linear decay, floor at 1, cap at `multMax`.

**AC:** `EconomyTests.Multiplier_*` all green.

### M1-T08 · `ChargeSystem` (FR-201, FR-105)
`AddFromKill` uses `floor(baseValue × multiplier)`. `ApplyHitPenalty`. `Consume()` is `internal` — only `BankSystem` may call it.

⚠️ `RunController` owns the kill handler and calls **multiplier first, then charge**, in one method. Do not put these two on the event bus independently — subscription order is not guaranteed and the multiplier must be current. This is the one documented exception to the bus pattern (`docs/01_ARCHITECTURE.md` §10).

**AC:** `Kill_AddsValueTimesMultiplier` and `PlayerHit_HalvesCharge_AndResetsMultiplier` green.

### M1-T09 · `PlayerHealth` (FR-104, FR-105)
3 HP, `hitInvulnDuration` with a 12 Hz blink, one damage event per invuln window regardless of overlap count. Damage triggers the charge penalty and resets MULT.

**AC:** standing inside 20 drifters costs exactly 1 HP per `hitInvulnDuration`.

### M1-T10 · `DashModule` (FR-106)
Fixed `distance` over `duration` with ease-out, `iframeDuration`, 2 charges with **independent** cooldown timers.

**AC:** both charges usable back-to-back; refills are independent, not a shared timer; dash cannot start while banking.

### M1-T11 · ★ `BankSystem` (FR-205 – FR-209) — the whole game
Hold-to-bank per the contract in `docs/01_ARCHITECTURE.md` §6.
- Immobile while holding; weapon at `bankFireRateMultiplier`
- Early release: **no credit, no penalty**, progress to zero
- Damage: interrupt + normal hit penalty + `interruptCooldown` lockout
- **Completion order is fixed:** consume charge → add to score → reset multiplier → raise `BankCompleted` → juice

**AC:** all `EconomyTests` bank cases green; manual QA cases 1–8 in `docs/05_QA_AND_VERIFY.md` pass.

### M1-T12 · `ScoreSystem` (FR-204, FR-209)
`long` score. Risk multiplier applied on bank. Unbanked CHARGE contributes **zero** at run end.

**AC:** `RunEnd_UnbankedCharge_ContributesZero` and `Score_UsesLong_DoesNotOverflowAtRiskV` green.

### M1-T13 · Minimum HUD (FR-210)
Raw TMP, no styling. **But:** CHARGE must already be the largest element and gold, with the pulse tied to MULT, and the **bank ring must draw around the player** — not in a corner. Those two are load-bearing for the Day-3 test, everything else is placeholder.

Also show: MULT + decay bar, SCORE, HP pips, dash charges.

**AC:** during a 10-minute play session your eye is drawn to the gold number, not to your character.

### M1-T14 · Death & instant restart (G1)
Player at 0 HP → 1.0s death beat → **restart on any key in under 2.0 seconds total**. No menu, no confirmation, no results screen yet.

**AC:** measured death→playing time < 2.0s. This number is a core design requirement, not polish.

### M1-T15 · ★ Run the Day-3 gate
Play 10 minutes without touching the editor. Answer the 4 questions in `PRD.md` §6 honestly and **write the answers into `TUNING_LOG.md`**.

If < 4/4, spend the next session adjusting **only** these, in this order:
1. `chargeLossOnHit` 0.50 → 0.75 (the loss must hurt)
2. `bankDuration` 1.20 → 1.50 (make the window scarier)
3. `multDecayGrace` 1.50 → 1.10 (force faster decisions)
4. If too punishing: `maxHP` 3 → 4, or `bankDuration` → 1.00 — adjust *forgiveness*, never *tension*

**AC:** 4/4, with the answers recorded.

---

## Definition of Done for M1

- [ ] `./tools/verify.sh` prints ✅ PASS
- [ ] `EconomyTests` and `PoolTests` fully green
- [ ] Manual QA cases 1–8 pass
- [ ] Zero GC allocation per frame at 200 enemies (Profiler, 60s)
- [ ] Zero runtime `Instantiate`
- [ ] Day-3 gate answered 4/4 and logged
- [ ] Tagged `m1-core-loop`

## Explicitly NOT in M1

Spitter · Splitter · boss · waves · upgrades · Vault · menus · results screen · saving · Steam · audio · post-processing · localisation · seeds. All of it comes later. If you build any of it now you will have less time to fix the loop, which is the only thing that matters this week.
