# TEST RUN — OVERDRAFT M0 + M1

From a clean machine to a playable build of the core loop. Budget **~25 minutes**, most of it
Unity downloading.

> **What you are testing:** one endless arena, one enemy type, auto-fire, dash, and the BANK
> hold. That is deliberately all of M1. Waves, upgrades, the boss, menus, audio, Steam and the
> art pass are M2–M5 and are not in this build.

---

## 0 · Prerequisites

Unity is not currently installed on this machine. Install it first.

1. Download **Unity Hub** → <https://unity.com/download>
2. In Hub → **Installs** → **Install Editor** → **2022.3.62f3 (LTS)**
   - Any `2022.3.x` works. Unity will offer to upgrade the project version file; accept it.
   - **Do not** install 2023.x or Unity 6 — constraint C1, and several APIs here differ.
3. Modules to tick:

   | Module | Needed for |
   |---|---|
   | *(none)* | Playing in the editor — this test run |
   | **Mac Build Support (IL2CPP)** | Shipping macOS builds (M5) |
   | **Windows Build Support (IL2CPP)** | Shipping Windows builds (M5) |

   You can add modules later from Hub → Installs → ⚙ → Add modules.

---

## 1 · Open the project

Unity Hub → **Add** → **Add project from disk** → select:

```
<path-to-cloned-repo>/OVERDRAFT_Handover
```

Open it. First import takes 3–8 minutes — it is downloading URP, the Input System and
TextMeshPro from the package registry, then compiling shaders.

**Two dialogs will appear. Both answers matter:**

| Dialog | Answer |
|---|---|
| *"…new input system… requires a restart"* | **Yes** — the project is Input-System-only (C6) |
| *"Enter Safe Mode?"* | **Ignore / Exit Safe Mode** — see Troubleshooting below |

---

## 2 · Import TextMeshPro resources

The HUD is TMP, and TMP ships its font atlas separately.

```
Window > TextMeshPro > Import TMP Essential Resources
```

Click **Import** in the dialog that opens. This is a one-time, ~10-second step. The build
command in step 3 refuses to run without it and will tell you so.

---

## 3 · Generate the game

```
Overdraft > Build Project (M0 + M1)
```

One menu item. It generates, in order: project settings (Linear colour space, URP pipeline
asset, 60 Hz fixed timestep, no shadows) → `TuningConfig.asset` with every value from
`docs/00_TUNING.md` → the Drifter definition → all meshes → all materials → the
`.inputactions` asset → the Drifter and projectile prefabs → the three scenes, fully wired.

It takes about 15 seconds and ends by opening `00_Boot` and logging the control scheme.

**It is idempotent.** Run it again any time — after a tuning change, or if a scene reference
ever looks wrong. `Overdraft > Rebuild Scenes Only` skips asset regeneration.

---

## 4 · Play

Make sure `Assets/_Project/Scenes/00_Boot.unity` is the open scene, then press **Play**.

> `00_Boot` sets the 60 Hz timestep, installs services, and loads `02_Arena` additively.
> `02_Arena` also plays standalone — every reference in it is wired — but it will run at Unity's
> default 50 Hz timestep instead of 60, so always test from `00_Boot`.

### Controls

| Action | Keyboard + mouse | Gamepad |
|---|---|---|
| Move | `WASD` / arrows | Left stick |
| Aim | Mouse position | Right stick |
| **Fire** | — auto-fires, always — | — |
| Dash | `Shift` or right mouse | `RB` / `R1` |
| **BANK** | **hold** `Space` or **hold** left mouse | **hold** `A` / `Cross` |
| Restart after death | any key | any face button |

Both device types are live simultaneously. There is no mode toggle and there never will be
(C6, FR-541) — pick up a controller mid-run and it just works.

### The loop, in one sentence

Kill things to raise the gold **CHARGE** number, then stand *completely still* for 1.2 seconds
holding BANK to turn it into permanent **SCORE**. Get hit and you lose half the gold.
Die and you lose all of it.

---

## 5 · What "working" looks like

Watch for these on the first run. Each one maps to a requirement.

| # | Look for | Requirement |
|---|---|---|
| 1 | Enemies drift in from a ring and clump toward you | FR-301 |
| 2 | Killing one **freezes the frame for ~45 ms** and kicks the camera | M1-T06 |
| 3 | The gold CHARGE number is the biggest thing on screen and **pulses faster as MULT rises** | FR-210 |
| 4 | The MULT decay bar (top left) empties, then MULT ticks down, and the bar turns magenta first | FR-203 |
| 5 | Holding BANK freezes you in place and draws a **gold ring filling around your character** — not in a corner | FR-206, docs/04 §6 |
| 6 | While banking, you keep firing at half rate | FR-206 |
| 7 | Completing a bank punches the camera, shakes the screen, zeroes CHARGE and resets MULT to ×1.0 | FR-205 |
| 8 | Taking a hit halves CHARGE, resets MULT to ×1.0, and blinks you for one second | FR-105 |
| 9 | Dash has **two pips** that refill on independent timers | FR-106 |
| 10 | Dying shows a banner, and any key restarts in well under 2 seconds | G1, M1-T14 |

### The eight core-loop QA cases (docs/05 §3)

Run these deliberately. Cases 1 and 2 concern waves and the boss and are **not testable until
M2** — they are listed so the numbering stays stable.

| # | Do this | Expected |
|---|---|---|
| 1 | *(waves — M2)* | — |
| 2 | *(boss — M2)* | — |
| 3 | Release BANK at ~99% | No score, no penalty, ring vanishes, progress resets to zero |
| 4 | Let a drifter touch you at ~99% of a bank | Half your CHARGE is gone, the bank fails, **and you cannot re-bank for 2 seconds** |
| 5 | Press dash while banking | Nothing happens — the input is ignored |
| 6 | Bank with CHARGE at 0 | Completes silently. No error, no divide-by-zero |
| 7 | Push MULT to the ×50 cap and keep killing | Stops at ×50, no visual glitch |
| 8 | Die while holding BANK | Death wins; the run ends and the unbanked gold is simply gone |

---

## 6 · Run the tests

**In the editor:** `Window > General > Test Runner` → **EditMode** tab → **Run All**.

Expect **38 tests, all green**, across three fixtures:

| Fixture | Covers |
|---|---|
| `EconomyTests` | FR-105, FR-201–209, FR-403 — the core loop, driven through the real components at the real 60 Hz step |
| `PoolTests` | C3 and NFR-02 — exhaustion returns null, double-return is safe, Get/Return allocate nothing |
| `UtilityTests` | Broad-phase grid, XZ circle overlap, deterministic RNG, zero-allocation number formatting |

**From the command line:**

```bash
UNITY_PATH="/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity" ./tools/verify.sh
```

`verify.sh` does three things and all three must pass: the static bans (no `FindObjectOfType`,
no LINQ in hot folders, no runtime `Instantiate` outside the pool, no engine dependency in
`Generation/`), then compile, then the EditMode suite.

To run only the static bans without Unity:

```bash
./tools/verify.sh --static-only
```

That prints `SKIPPED` for the compile and test stage — it is not a pass. If Unity is missing
entirely, the script says so and **fails** rather than reporting green.

---

## 7 · Profiler check (NFR-02)

Once the arena is busy — around 60 seconds in:

1. `Window > Analysis > Profiler`
2. Select the **Memory** module
3. **GC Allocated In Frame** must read **0 B** in steady state

Non-zero here means something in a per-frame path is allocating; the usual suspects are string
formatting in the HUD and `foreach` over an interface. Both are avoided in this build, so a
regression is a real finding.

---

## 8 · Then: the Day-3 gate

This is the only thing that matters this week.

Play for **10 minutes without touching the editor**, then answer the four questions in
`TUNING_LOG.md` honestly and write the answers down. 4/4 proceeds to M2. Fewer than 4/4 means
Day 4 is a tuning day, changing one lever at a time in the documented order — and that is a
normal outcome, not a failure.

---

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| **Unity opens in Safe Mode** | Packages had not finished resolving when scripts first compiled. Click *Exit Safe Mode*, wait for the package manager to settle, then `Assets > Reimport All`. |
| `Overdraft` menu is missing | Scripts did not compile. Open the Console, fix the first `error CS…`, and the menu returns. |
| "TextMeshPro resources missing" dialog | You skipped step 2. |
| Everything is magenta | Shader compile failure. Console will name the shader and line. The four shaders are the least-verified part of this build — see the honesty note below. |
| Shader errors mentioning `Core.hlsl` | URP did not install. `Window > Package Manager > In Project` — confirm *Universal RP 14.0.11* is present. |
| Nothing renders, camera is grey | The URP asset was not assigned. Re-run `Overdraft > Build Project`, then check `Project Settings > Graphics > Scriptable Render Pipeline Settings`. |
| Gamepad does nothing | `Project Settings > Player > Active Input Handling` must be **Input System Package (New)**. Changing it needs an editor restart. |
| `NullReferenceException` on Play | A scene reference did not wire. Re-run `Overdraft > Rebuild Scenes Only` — the console will name any field that failed. |
| Enemies pass through you unharmed | Expected during the 1-second invulnerability blink after a hit. |

### Honest note on what has and has not been verified

Unity is not installed on this machine, so **nothing here has been compiled or executed**. What
*was* run is the static-analysis stage of `verify.sh`, which passes.

Ranked by likelihood of needing a small fix on first compile:

1. **The four shaders** (`Assets/_Project/Art/Shaders/`) — hand-written HLSL, never compiled.
2. **`ProjectConfigurator.ApplyRenderPipeline`** — URP asset creation via `UniversalRenderPipelineAsset.Create`. It logs a clear warning and continues if it fails; you can create the asset by hand via `Assets > Create > Rendering > URP Asset`.
3. **`PoolTests.GetAndReturn_DoNotAllocate`** — depends on `GameObject.SetActive` not allocating internally. If it fails, that is Unity's allocation, not the pool's.
4. **Everything else** — plain C# against APIs that have been stable across the whole 2022.3 line.
