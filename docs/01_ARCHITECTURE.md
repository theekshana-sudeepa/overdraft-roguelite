# 01 — ARCHITECTURE

The whole architecture is four ideas: **a static event bus**, **ScriptableObject data**, **object pools**, and **one pure-C# generation assembly**. Nothing else. No DI, no ECS, no frameworks.

---

## 1. Assembly layout

| Asmdef | Path | References | Rule |
|---|---|---|---|
| `Overdraft.Generation` | `Scripts/Generation/` | **nothing** | ⚠️ Must NOT reference `UnityEngine`. Pure C#. This is what makes the game deterministic and testable. |
| `Overdraft.Runtime` | `Scripts/` (all other folders) | `Overdraft.Generation`, Unity modules, TMP, Input System | The game |
| `Overdraft.Steam` | `Scripts/Steam/` | `Overdraft.Runtime`, Steamworks.NET | Isolated so a Steam-free build is a one-line change |
| `Overdraft.Editor` | `Editor/` | `Overdraft.Runtime` | Editor-only: build script, data validators |
| `Overdraft.Tests` | `Tests/EditMode/` | `Overdraft.Generation`, `Overdraft.Runtime`, NUnit | EditMode only. No PlayMode tests — they're too slow for this timeline. |

Verify the `Generation` isolation in CI:
```bash
! grep -rn "UnityEngine" Assets/_Project/Scripts/Generation/ || (echo "FAIL: UnityEngine in Generation/" && exit 1)
```

---

## 2. Frame model

| Callback | Runs | Contains |
|---|---|---|
| `FixedUpdate` @ **60 Hz** | All gameplay: movement, AI, projectiles, collision, economy timers, boss state | Deterministic. Uses `Time.fixedDeltaTime` only. |
| `Update` | Input polling into a buffer, HUD, VFX, camera, audio, hitstop timer | Never mutates gameplay state |
| `LateUpdate` | Camera follow + shake application | — |

`Time.fixedDeltaTime = 1f/60f` is set once in `Bootstrap`. `Time.maximumDeltaTime = 0.1f` so a hitch never causes a death-spiral of catch-up physics steps.

**Hitstop** sets `Time.timeScale` to `hitstopScale` for an *unscaled* duration, then restores. Because gameplay is in `FixedUpdate`, hitstop naturally freezes gameplay while `Update`-driven VFX and audio keep running — which is exactly the feel you want and is free.

---

## 3. The event bus

```csharp
// Scripts/Core/GameEvents.cs
namespace Overdraft.Core
{
    /// Static, allocation-free game event bus. Subscribe in OnEnable, unsubscribe in OnDisable.
    /// Handlers must not throw. Handlers must not subscribe/unsubscribe during dispatch.
    public static class GameEvents
    {
        // --- Combat ---
        public static event System.Action<EnemyKilledArgs> EnemyKilled;
        public static event System.Action<PlayerHitArgs>   PlayerHit;
        public static event System.Action                  PlayerDashed;
        public static event System.Action                  PerfectDash;

        // --- Economy ---
        public static event System.Action<float>  MultiplierChanged;   // new value
        public static event System.Action<long>   ChargeChanged;       // new value
        public static event System.Action<long>   ScoreChanged;        // new banked total
        public static event System.Action         BankStarted;
        public static event System.Action<float>  BankProgress;        // 0..1, per FixedUpdate while holding
        public static event System.Action<long>   BankCompleted;       // amount banked (post-risk-multiplier)
        public static event System.Action<bool>   BankCancelled;       // true = interrupted by damage

        // --- Run flow ---
        public static event System.Action<int>    WaveStarted;         // wave index 1..6
        public static event System.Action<int>    WaveCleared;
        public static event System.Action<EnvironmentId> EnvironmentChanged;
        public static event System.Action         BossSpawned;
        public static event System.Action<int>    BossPhaseChanged;    // 1,2,3
        public static event System.Action         BossDefeated;
        public static event System.Action<RunResult> RunEnded;
        public static event System.Action<UpgradeId> UpgradeTaken;

        // Raise methods are the ONLY way to fire. Null-conditional, no allocation.
        public static void RaiseEnemyKilled(in EnemyKilledArgs a) => EnemyKilled?.Invoke(a);
        // ... one per event
    }

    public readonly struct EnemyKilledArgs
    {
        public readonly EnemyType Type;
        public readonly bool      IsElite;
        public readonly int       BaseValue;
        public readonly UnityEngine.Vector3 Position;
        public readonly bool      FromExplosion;   // prevents infinite VOLATILE recursion accounting
        public EnemyKilledArgs(EnemyType t, bool elite, int value, UnityEngine.Vector3 pos, bool fromExplosion)
            { Type = t; IsElite = elite; BaseValue = value; Position = pos; FromExplosion = fromExplosion; }
    }
}
```

**Rules for the bus:**
- Args are `readonly struct` passed by `in`. Zero allocation.
- Subscribe in `OnEnable`, unsubscribe in `OnDisable`. **Always both.** A static event holding a destroyed MonoBehaviour is the #1 source of leaks in this pattern.
- `Bootstrap` calls `GameEvents.ClearAll()` on scene teardown as a safety net (an `#if UNITY_EDITOR` domain-reload guard too, since Unity 2022 defaults to fast enter-playmode with domain reload disabled — static events survive play sessions and *will* bite you).
- Handlers must not raise other events synchronously more than one level deep.

---

## 4. Service locator (deliberately minimal)

```csharp
// Scripts/Core/ServiceLocator.cs
public static class ServiceLocator
{
    public static TuningConfig   Tuning   { get; private set; }
    public static SaveSystem     Save     { get; private set; }
    public static AudioDirector  Audio    { get; private set; }
    public static RunController  Run      { get; internal set; }   // null outside a run

    public static void Install(TuningConfig t, SaveSystem s, AudioDirector a) { ... }
}
```

That's the entire "DI". Four properties, set once at boot. Do not generalise it.

---

## 5. Run state machine

`RunController` is the single source of truth for "what is happening." One enum, one switch, no framework.

```
Intro (1.5s)
  ↓
WaveActive(n)  ──cleared or timeout──▶  WaveCleared(n)
  ↑                                          │
  └──────── UpgradeChoice ◀──────────────────┘   (n < 6)
                                               │  (n == 6)
                                               ▼
                                        BossIntro (2.0s)
                                               ↓
                                          BossActive
                                               ↓
                                        BossDefeated → ForcedFreeBank (1.5s slow-mo)
                                               ↓
                                           Results
Any state ──player HP reaches 0──▶ Death (1.0s) ─▶ Results
```

```csharp
public enum RunState { Intro, WaveActive, WaveCleared, UpgradeChoice,
                       BossIntro, BossActive, BossDefeated, Death, Results }
```

Environment swap happens on entry to `WaveActive(4)`: `SetActive` the Vault root, disable the Floor root, lerp the colour-grading volume weight over `environmentLerpTime`. No scene load.

---

## 6. Script contracts

Only the non-obvious ones. Everything else follows from these.

### `ChargeSystem`
```csharp
public sealed class ChargeSystem : MonoBehaviour
{
    public long Current { get; private set; }

    /// Adds floor(baseValue * multiplier) to CHARGE. Called from the EnemyKilled handler.
    public void AddFromKill(int baseValue, float multiplier);

    /// Applies the hit penalty. Returns the amount destroyed.
    public long ApplyHitPenalty(float lossFraction);

    /// Boss audit drain. Returns the amount drained this step.
    public long Drain(float fractionPerSecond, float deltaTime);

    /// Zeroes CHARGE and returns what it was. Only BankSystem may call this.
    internal long Consume();
}
```

### `MultiplierSystem`
```csharp
public sealed class MultiplierSystem : MonoBehaviour
{
    public float Current   { get; private set; }   // >= multMin
    public float GraceLeft { get; private set; }   // seconds until decay begins
    public bool  IsWarning => GraceLeft > 0f && GraceLeft <= tuning.Economy.multWarnThreshold;

    public void AddKill();                 // += multPerKill (+ GREED bonus), refresh grace
    public void AddFlat(float amount);     // Perfect Dash
    public void ResetToBase();             // on hit and on bank completion
    public void RemoveCap();               // GREED T2
}
```

### `BankSystem`
```csharp
public sealed class BankSystem : MonoBehaviour
{
    public bool  IsBanking  { get; private set; }
    public float Progress01 { get; private set; }
    public bool  IsLockedOut => lockoutTimer > 0f;

    public void OnBankInputChanged(bool held);   // called from input layer
    public void Interrupt();                     // called by PlayerHealth on damage
    public void ForceComplete();                 // boss-defeat free bank; no vulnerability
    float EffectiveDuration => /* base, reduced by LEECH tier */;
}
```

**Bank completion order is fixed and must not be reordered** — the HUD and audio depend on it:
1. `charge.Consume()` → amount
2. `score.Add(amount * riskMultiplier)`
3. `multiplier.ResetToBase()`
4. `GameEvents.RaiseBankCompleted(banked)`
5. Hitstop, screen shake, VFX, audio (all driven by the event, not called directly)

### `WaveGenerator` — pure, no Unity
```csharp
// Scripts/Generation/WaveGenerator.cs  (NO using UnityEngine)
namespace Overdraft.Generation
{
    public static class WaveGenerator
    {
        /// Deterministic. Same inputs always produce an identical list on every platform.
        public static void Generate(
            int seed, int waveIndex, float riskBudgetMultiplier,
            WaveRules rules, System.Collections.Generic.List<SpawnEvent> output);
    }

    public readonly struct SpawnEvent
    {
        public readonly EnemyType Type;
        public readonly bool      IsElite;
        public readonly float     DelaySeconds;   // from wave start
        public readonly float     RingAngleRad;   // position on the spawn ring
        public readonly float     RingRadius;
    }
}
```

`output` is a caller-owned `List<SpawnEvent>` that is cleared and refilled — no allocation per wave after warm-up.

### `RunSeed` — independent random streams
```csharp
public sealed class RunSeed
{
    readonly int master;
    public RunSeed(int master) => this.master = master;

    /// Channels keep systems independent: adding a new system later cannot shift
    /// the rolls of an existing one, so old daily seeds stay reproducible.
    /// 0 = wave composition, 1 = spawn timing, 2 = upgrade offers,
    /// 3 = hazard/boss patterns, 4 = cosmetic flavour
    public System.Random Stream(int channel, int index)
        => new System.Random(Hash(master, channel, index));

    static int Hash(int a, int b, int c)   // FNV-1a, explicit and stable across runtimes
    {
        unchecked
        {
            const int prime = 16777619;
            int h = unchecked((int)2166136261);
            h = (h ^ a) * prime;
            h = (h ^ b) * prime;
            h = (h ^ c) * prime;
            return h;
        }
    }
}
```

> ⚠️ `System.Random`'s internal algorithm is stable across .NET runtimes for a given seed, but **`string.GetHashCode()` is not** — it is randomised per process. Never derive a seed from a string hash. The Custom Seed field parses to an `int` directly.

### `ObjectPool<T>`
```csharp
public sealed class ObjectPool<T> where T : Component
{
    public ObjectPool(T prefab, Transform parent, int capacity);
    public T    Get();          // returns null if exhausted — CALLERS MUST NULL-CHECK
    public void Return(T item); // safe against double-return
    public int  ActiveCount { get; }
    public void ReturnAll();
}
```

Pool exhaustion returns `null` rather than growing. This is deliberate: a silently growing pool hides a spawn bug until it becomes a frame-rate bug. Splitter splits, explosions, and VFX all check for null and skip.

---

## 7. Collision — no physics

```csharp
// Scripts/Utility/CircleOverlap.cs
public static bool Overlaps(in Vector3 a, float ra, in Vector3 b, float rb)
{
    float dx = a.x - b.x, dz = a.z - b.z;
    float r  = ra + rb;
    return dx * dx + dz * dz <= r * r;      // squared, no sqrt, XZ plane only
}
```

Broad phase: a **uniform spatial grid** over the arena, cell size `2.0` world units, rebuilt each `FixedUpdate` into pre-allocated `int` buckets. At 300 enemies and 500 projectiles this is under 0.4 ms and allocates nothing.

Do not use `Physics.OverlapSphere`, triggers, layers, or `OnCollisionEnter`. There are no colliders in this project.

---

## 8. Scenes

| Scene | Loaded | Contains |
|---|---|---|
| `00_Boot` | First, always | `Bootstrap`, `SteamManager`, `SaveSystem`, `AudioDirector`, `Localizer`. All `DontDestroyOnLoad`. Immediately loads `01_Menu` additively. |
| `01_Menu` | Additive | Main menu, mode select, Credit Ranks, options, leaderboards |
| `02_Arena` | Additive, unloaded on run end | Arena roots (Floor + Vault, one active), `RunController`, spawners, pools, HUD |

Both environments are **prefabs inside `02_Arena`**, not separate scenes.

---

## 9. Input

New Input System, one `.inputactions` asset, one action map `Gameplay` + one `UI`.

| Action | Type | Keyboard/Mouse | Gamepad |
|---|---|---|---|
| `Move` | Value / Vector2 | WASD | Left Stick |
| `Aim` | Value / Vector2 | Mouse position (screen → arena-plane ray) | Right Stick |
| `Dash` | Button | Shift / RMB | RB / R1 |
| `Bank` | Button (hold) | Space / LMB-hold | A / Cross |
| `Pause` | Button | Esc | Start |

- `Aim` has **two bindings feeding one action** with a processor that flags the source. If the last input device was a mouse, aim is absolute-position; if a gamepad, it's stick direction. Track "last used device" via `InputSystem.onEvent` and switch prompt glyphs accordingly — never require the player to pick a mode (FR-541, C6).
- `Bank` is polled as `held`, not as a performed interaction, so the hold timer stays in `BankSystem` where it can be tuned and interrupted.
- Handle `InputSystem.onDeviceChange` for hot-plug: on `Removed` during a run, pause and show a reconnect prompt (FR-546).

---

## 10. Data flow, one kill end to end

```
Projectile.FixedUpdate
  → grid query → CircleOverlap hit
  → EnemyBase.TakeDamage(1)
      → hp <= 0
      → GameEvents.RaiseEnemyKilled(args)
      → pool.Return(this)

Subscribers of EnemyKilled (order is irrelevant; none may depend on another):
  MultiplierSystem  → AddKill()
  ChargeSystem      → AddFromKill(args.BaseValue, multiplier.Current)
  Hitstop           → Freeze(feel.hitstopOnKill)
  ScreenShake       → AddTrauma(feel.traumaOnKill)
  VfxDirector       → pooled burst at args.Position
  AudioDirector     → SfxPlayer.Play(kill, pitchJitter)   [voice-limited]
  VolatileUpgrade   → if owned && !args.FromExplosion → explode at Position
  RunController     → decrement alive count; if zero → WaveCleared
```

Note `ChargeSystem` reads `multiplier.Current` **after** `MultiplierSystem` has updated it only if subscription order guarantees it — it doesn't. So: **`ChargeSystem` and `MultiplierSystem` are the two exceptions to the event pattern.** `RunController` owns both and calls them explicitly in a fixed order (multiplier first, then charge) inside a single `OnEnemyKilled` handler. Everything else stays on the bus.

---

## 11. Performance rules with teeth

| Rule | Enforced by |
|---|---|
| No runtime `Instantiate` outside `ObjectPool` and `Bootstrap` | `verify.sh` grep |
| No LINQ under `Scripts/{Player,Enemies,Economy,Generation}` | `verify.sh` grep |
| No `FindObjectOfType` / `GameObject.Find` anywhere in `Scripts/` | `verify.sh` grep |
| Zero per-frame allocation | Profiler check at each milestone gate |
| HUD numbers never allocate | Pre-baked digit strings + a `char[]` buffer written into a `TMP_Text` via `SetCharArray` |
| One material for all enemies | Per-instance colour via `MaterialPropertyBlock`; SRP Batcher on |

**HUD number formatting** is the sneakiest allocation source in a game whose main UI element is a number that changes every frame. Implementation:

```csharp
// Scripts/UI/FastNumberText.cs — writes a long into a TMP_Text with zero allocation
readonly char[] buffer = new char[24];
public void SetValue(long v)
{
    if (v == lastValue) { return; }        // early out — most frames change nothing
    lastValue = v;
    int len = FormatWithCommas(v, buffer); // manual digit extraction, no ToString()
    text.SetCharArray(buffer, 0, len);
}
```

---

## 12. Determinism checklist

Anything that breaks one of these breaks the daily leaderboard, which is a core retention feature.

- [ ] `Generation/` contains no `using UnityEngine`
- [ ] No `UnityEngine.Random` anywhere in a generation path
- [ ] No seed derived from `string.GetHashCode()`
- [ ] No `float`-of-frame-time branching in generation
- [ ] No `Dictionary`/`HashSet` iteration order relied upon for generation output
- [ ] `DateTime.UtcNow.Date` only for the daily seed — never `Now`, never local
- [ ] Cross-platform hash test in the suite (`Determinism_HashOf1000Runs_MatchesGolden`) with the golden file committed
