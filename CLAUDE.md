# CLAUDE.md — OVERDRAFT

> **Read this file in full at the start of every session. It overrides your defaults.**
> If anything you are about to do conflicts with this file, stop and ask.

---

## 1. What this project is

**OVERDRAFT** is a 6-minute-per-run top-down twin-stick roguelite for Steam (Windows + macOS), built solo in 21 calendar days at ~5 hours/day. Unity 2022.3 LTS, URP, C#, New Input System, Steamworks.NET.

**The core loop, in one paragraph:** The player auto-fires at enemies. Each kill adds `enemyValue × MULT` to an unbanked pool called **CHARGE**, and raises **MULT**. MULT decays if the player stops killing. CHARGE is worth **zero** at end of run. To convert CHARGE into permanent **SCORE**, the player must hold the BANK button for 1.2 seconds while completely immobile. Taking a hit costs 1 HP *and 50% of unbanked CHARGE*. The entire game is the tension in that 1.2-second window.

**Everything you build must serve that tension.** When a design question is ambiguous, choose the answer that makes the bank decision harder or more dramatic.

---

## 2. Ship date and why it constrains you

| Date | Event |
|---|---|
| 2026-08-07 | Day 1 |
| 2026-08-09 | **M1 gate** — the loop must be fun or the project pivots |
| 2026-08-27 | Day 21 — feature freeze, `1.0.0-rc1` |
| 2026-09-17 | Steam launch |

There are **105 developer hours total.** Time you spend on architecture the game doesn't need is time taken directly from tuning, polish, and the store page. Optimise for *shipped*, not for *elegant*.

---

## 3. Non-negotiable constraints

| # | Constraint |
|---|---|
| C1 | Unity **2022.3 LTS**. Do not use APIs added in Unity 6 / 2023. If unsure of an API's version, say so rather than guessing. |
| C2 | **URP**, unlit materials only. No realtime lights, no shadows, no lightmaps, no reflection probes. |
| C3 | **Zero `Instantiate`/`Destroy` at runtime** after warm-up. Everything pools. |
| C4 | **Zero GC allocations per frame** in steady-state gameplay. |
| C5 | **No physics.** No `Rigidbody`, no `Collider`-based movement. Movement is `transform.position` on a fixed timestep; collision is squared-distance circle overlap. |
| C6 | Both **keyboard+mouse and gamepad** must work at all times, with no mode switch and no settings toggle. |
| C7 | Every Steam call is guarded by `Steam.Ready`. The game is **fully playable with Steam offline or absent.** |
| C8 | All gameplay logic runs on a **fixed 60 Hz timestep** and is **deterministic from a seed**. |
| C9 | Every tunable number lives in `TuningConfig` (a ScriptableObject). **No magic numbers in gameplay code.** |
| C10 | Target **60 fps** on Apple M4 at 1440p and Intel UHD 620 at 1080p. Frame budget 8 ms. |

---

## 4. ⛔ Anti-patterns — do not do these

These are the specific failure modes for this project. Each one has cost real projects real weeks.

**Architecture**
- ❌ Do **not** introduce a DI container, ECS, DOTS, a state-machine framework, UniRx, or any architectural package. A static event bus + ScriptableObjects + pools is the whole architecture. It is deliberate.
- ❌ Do **not** create an interface with exactly one implementation. Concrete classes until a second implementation actually exists.
- ❌ Do **not** create abstract base classes "for future flexibility." There is no future; there is 2026-08-27.
- ❌ Do **not** add `[SerializeField]` tuning values to individual MonoBehaviours. They belong in `TuningConfig` (see C9). Prefab-local values fragment the tuning pass and you will lose an evening to it.
- ❌ Do **not** split a file "for cleanliness" if it is under 300 lines.
- ❌ Do **not** use `GameObject.Find`, `FindObjectOfType`, or `SendMessage` in gameplay code. Ever.

**Performance**
- ❌ No LINQ in anything that runs per-frame or per-enemy. `Where`/`Select`/`OrderBy` allocate.
- ❌ No `foreach` over an interface or `IEnumerable` in hot paths (boxes the enumerator). `for` over `List<T>` or arrays.
- ❌ No string concatenation or interpolation in `Update`. Cache formatted strings; only rebuild when the underlying value changes. HUD numbers use a cached `char[]` or a pre-baked lookup for 0–9.
- ❌ No `new WaitForSeconds(...)` inside a loop. Cache it.
- ❌ No `Camera.main` in `Update`. Cache the reference.
- ❌ No `GetComponent` in `Update`. Cache in `Awake`.

**Scope**
- ❌ Do **not** implement anything not listed in `PRD.md`. If you think of a good feature, append it to `POST_LAUNCH.md` and move on.
- ❌ Do **not** add online multiplayer, procedural room geometry, mid-run saving, animated sprite sheets, a dialogue system, mod support, or console platform code. See PRD §Non-Goals.
- ❌ Do **not** write a `README.md`, a `CONTRIBUTING.md`, or extra documentation unless a task explicitly asks for it.
- ❌ Do **not** refactor working code that is not part of your current task.

**Process**
- ❌ Do **not** mark a task complete while any test fails, any compile error exists, or any acceptance criterion is unmet.
- ❌ Do **not** invent tuning values. They are all in `docs/00_TUNING.md`. If a value you need is missing there, **add it to that file first**, then reference it.
- ❌ Do **not** silently change a value in `docs/00_TUNING.md`. If tuning needs to change, say so explicitly in your summary.

---

## 5. Coding standards

```csharp
namespace Overdraft.Economy;          // file-scoped, matches folder under Scripts/

public sealed class BankSystem : MonoBehaviour   // sealed by default
{
    [SerializeField] TuningConfig tuning;        // config refs only, never tuning values
    float holdTimer;                             // private fields: no underscore, camelCase
    public bool IsBanking { get; private set; }  // public surface: PascalCase, minimal
}
```

- `sealed` on every class unless it is genuinely inherited from (`EnemyBase` is the only expected exception).
- `readonly` on every field that can be.
- Explicit access modifiers everywhere. No implicit `private`.
- Braces on every `if`, even single-line ones.
- One public class per file; filename == class name.
- XML doc comments **only** on public members of `Core/`, `Economy/`, and `Generation/`. Nowhere else — inline comments explain *why*, never *what*.
- Serialised field names never change after they hold data. Use `[FormerlySerializedAs]` if you must rename.
- `float` for gameplay, `long` for score (a RISK V run can exceed `int.MaxValue`).
- Time: `Time.fixedDeltaTime` in `FixedUpdate` for gameplay, `Time.unscaledDeltaTime` for UI and hitstop.

**Assembly definitions:** four asmdefs — `Overdraft.Runtime`, `Overdraft.Steam`, `Overdraft.Editor`, `Overdraft.Tests`. This keeps compile times under 3 seconds, which over 21 days is worth roughly a full working day.

---

## 6. Where things live

```
Assets/_Project/Scripts/
  Core/        GameEvents, RunController, ServiceLocator, TuningConfig, Bootstrap
  Player/      PlayerController, PlayerHealth, DashModule, Weapon
  Enemies/     EnemyBase, DrifterAI, SpitterAI, SplitterAI, AuditorBoss, EnemySpawner
  Economy/     ChargeSystem, MultiplierSystem, BankSystem, ScoreSystem
  Generation/  RunSeed, WaveGenerator, UpgradeOfferGenerator, SpawnEvent   ← PURE C#, no UnityEngine
  Progression/ SaveSystem, SaveData, CreditRankSystem
  Steam/       Steam, SteamManager, SteamAchievements, SteamLeaderboards, SteamCloud, SteamPresence
  UI/          HUDController, MenuController, ResultsScreen, Localizer
  Audio/       AudioDirector, SfxPlayer, MusicLayerController
  Utility/     ObjectPool, ScreenShake, Hitstop, MathX, CircleOverlap
Assets/_Project/Data/     ScriptableObject assets (enemies, waves, upgrades, ranks, tuning)
Assets/Tests/EditMode/    Test assembly
```

Full detail: `docs/01_ARCHITECTURE.md`.

**`Generation/` is pure C#** — it must not reference `UnityEngine` at all. This is what makes the seeded generator unit-testable and cross-platform deterministic. Treat any `using UnityEngine;` appearing in that folder as a bug.

---

## 7. How to verify your work — run this, always

Before you report a task complete:

```bash
./tools/verify.sh          # macOS/Linux
tools\verify.bat           # Windows
```

That script does three things and **all three must pass**:
1. Compiles the project in batch mode, failing on any error *or warning* in `_Project/`
2. Runs the EditMode test suite
3. Greps for banned patterns (`FindObjectOfType`, LINQ in `Scripts/Player|Enemies|Economy`, `Instantiate(` outside pool/bootstrap code)

If you cannot run Unity in batch mode in your environment, say so explicitly in your summary rather than claiming verification passed. Never claim a test passed that you did not run.

**Every pure-logic change needs a test.** `Generation/`, `Economy/` maths, and `Progression/` serialisation are all pure and all testable. UI, VFX, and feel are verified by the human.

---

## 8. Definition of Done

A task is done when **every** box is true:

- [ ] Compiles with zero errors and zero new warnings
- [ ] `verify.sh` passes
- [ ] Every acceptance criterion in the task file is demonstrably met
- [ ] No new `TODO`, no commented-out code, no debug `Debug.Log` left in
- [ ] Zero per-frame GC allocations introduced (check the Profiler if the change touches Update/FixedUpdate)
- [ ] No values hard-coded that belong in `TuningConfig`
- [ ] Any new tunable was added to `docs/00_TUNING.md` in the same change

---

## 9. Session protocol

**At session start:**
1. Read `CLAUDE.md` (this file), `PRD.md`, and the specific `tasks/M*.md` you are working on.
2. Read `docs/00_TUNING.md` before writing any gameplay code.
3. State which task IDs you intend to complete this session, then start. Do not ask permission for tasks already in the milestone file.

**During:**
- One task at a time, in the order listed. The order encodes dependencies.
- If a task turns out to be materially bigger than its estimate, stop and say so rather than half-finishing it.
- If a design question isn't answered by the docs, **make the call that best serves the bank-tension** (§1), implement it, and flag the decision in your summary. Do not block on it.

**At session end, report exactly:**
1. Task IDs completed
2. Files added/modified
3. Any decision you made that wasn't in the spec
4. Anything that regressed or is now at risk
5. The single next task

Do not write a narrative summary of what you did. The diff shows that.

---

## 10. Git

- `main` is always buildable. Never commit a non-compiling state.
- One commit per completed task: `M1-T04: bank hold, interrupt, and charge transfer`
- Tag each milestone: `m1-core-loop`, `m2-content`, …
- Unity `.gitignore` + **Git LFS** for `*.wav`, `*.ogg`, `*.png`, `*.psd`, `*.mp4`
- Force text serialisation and visible meta files (`Edit > Project Settings > Editor`) — non-negotiable or merges become impossible.

---

## 11. The one-line summary of your job

> Build the smallest possible thing that makes a player refuse to bank because they want a bigger number, then ship it on 17 September 2026.
