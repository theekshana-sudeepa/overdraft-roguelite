# 00 — TUNING (single source of truth)

> **Every number in this file must exist in `TuningConfig` and nowhere else.**
> If gameplay code contains a numeric literal other than `0`, `1`, `-1`, or an array index, it is a bug.
>
> These are **starting values**, chosen to be roughly right. Expect to change 15–20 of them during the Day-3/Day-7 tuning passes. Changing them is *the job*, not a failure — but change them **here**, and say so.

---

## The config asset

```csharp
// Assets/_Project/Scripts/Core/TuningConfig.cs
using UnityEngine;

namespace Overdraft.Core
{
    [CreateAssetMenu(fileName = "TuningConfig", menuName = "Overdraft/Tuning Config")]
    public sealed class TuningConfig : ScriptableObject
    {
        public PlayerTuning     Player;
        public WeaponTuning     Weapon;
        public DashTuning       Dash;
        public EconomyTuning    Economy;
        public BankTuning       Bank;
        public WaveTuning       Wave;
        public BossTuning       Boss;
        public ArenaTuning      Arena;
        public FeelTuning       Feel;
        public RiskTier[]       RiskTiers;      // length 5
        public long[]           CreditRankThresholds; // length 12
    }
}
```

Exactly one asset instance exists: `Assets/_Project/Data/TuningConfig.asset`. It is injected via `ServiceLocator` at boot and referenced by every system that needs numbers. Nothing else may be a `[SerializeField] float`.

---

## Player

```csharp
[System.Serializable] public struct PlayerTuning
{
    public float moveSpeed;            // 8.0    world units / second
    public float acceleration;         // 60.0   units/s^2  -> ~0.13s to full speed
    public float deceleration;         // 80.0   units/s^2  -> ~0.10s to stop
    public float facingTurnSpeed;      // 900.0  degrees / second
    public float hitboxRadius;         // 0.35   DELIBERATELY smaller than visual (0.50)
    public float visualRadius;         // 0.50
    public int   maxHP;                // 3
    public float hitInvulnDuration;    // 1.00   seconds, blinks at 12 Hz
    public float chargeLossOnHit;      // 0.50   fraction of unbanked CHARGE destroyed
}
```

> `hitboxRadius < visualRadius` is intentional player-favouring generosity, standard in bullet-hell design. **Do not "correct" it.**

## Weapon

```csharp
[System.Serializable] public struct WeaponTuning
{
    public float fireInterval;         // 0.12   seconds  (8.33 shots/s)
    public float projectileSpeed;      // 22.0   units/s
    public float projectileRadius;     // 0.15
    public float projectileLifetime;   // 1.60   seconds
    public int   projectileDamage;     // 1
    public float baseSpreadDegrees;    // 3.0    tiny inaccuracy so the stream reads as a stream
    public int   poolSize;             // 512
}
```

## Dash

```csharp
[System.Serializable] public struct DashTuning
{
    public float distance;             // 4.50   world units
    public float duration;             // 0.18   seconds (ease-out)
    public float iframeDuration;       // 0.13   seconds (~8 frames @60Hz)
    public float chargeCooldown;       // 1.50   seconds, per charge, independent timers
    public int   maxCharges;           // 2
    public float perfectWindow;        // 0.10   seconds (~6 frames) before impact
    public float perfectMultBonus;     // 2.00   flat MULT added
    public float perfectHitstop;       // 0.15   seconds
}
```

## Economy — CHARGE and MULT

```csharp
[System.Serializable] public struct EconomyTuning
{
    public float multStart;            // 1.00
    public float multPerKill;          // 0.50
    public float multMax;              // 50.0   removed by GREED T2
    public float multDecayGrace;       // 1.50   seconds without a kill before decay begins
    public float multDecayRate;        // 1.00   MULT lost per second
    public float multWarnThreshold;    // 0.40   seconds of grace left -> warning audio + HUD pulse
    public float multMin;              // 1.00   floor
}
```

**The three numbers that define the feel of the whole game** are `multDecayGrace`, `chargeLossOnHit`, and `bankDuration`. If the Day-3 gate fails, these are the first three to move.

## Bank

```csharp
[System.Serializable] public struct BankTuning
{
    public float duration;                 // 1.20   seconds of hold
    public float fireRateMultiplier;       // 0.50   weapon fires at half rate while banking
    public float interruptCooldown;        // 2.00   seconds locked out after being hit mid-bank
    public float completeHitstop;          // 0.12   seconds
    public float leechT1Duration;          // 0.95
    public float leechT2Duration;          // 0.70
    public float leechT3Duration;          // 0.50
    public float leechT3IframeDuration;    // 0.50   i-frames granted on bank completion
    public float leechT2DashRefund;        // 0.30   seconds of bank progress refunded per dash
}
```

## Enemies (in `EnemyDefinition` assets, not `TuningConfig`)

| Field | DRIFTER | SPITTER | SPLITTER |
|---|---|---|---|
| `maxHP` | 1 | 3 | 2 |
| `moveSpeed` | 3.2 | 2.0 (strafe) | 6.5 (charge) / 1.5 (approach) |
| `hitboxRadius` | 0.40 | 0.45 | 0.40 |
| `scoreValue` | 10 | 40 | 25 |
| `budgetCost` | 10 | 40 | 25 |
| `contactDamage` | 1 | 1 | 1 |
| `colorKey` | Threat `#FF3B5C` | Threat `#FF3B5C` | Splitter `#FF7A3B` |
| `meshKey` | Triangle | Hollow square | Diamond |
| Special A | — | `preferredRange` 9.0 | `windupDuration` 0.40 |
| Special B | — | `fireInterval` 2.20 | `chargeDuration` 0.90 |
| Special C | — | `projectileSpeed` 6.0 | `splitCount` 2 |
| Special D | — | `puddleDuration` 2.0, `puddleRadius` 1.2 | `splitScatterRadius` 1.0 |

**Elite modifiers** (a flag on the pooled instance, never a separate prefab):

```
hpMultiplier      2.50
speedMultiplier   1.25
valueMultiplier   2.50
budgetMultiplier  2.50
scaleMultiplier   1.40
outlineWidth      0.06   (elites get a visible outline; that IS the tell)
```

Enemy pool sizes: `drifter 220`, `spitter 40`, `splitter 60`, `enemyProjectile 256`, `puddle 24`.

## Waves

```csharp
[System.Serializable] public struct WaveTuning
{
    public float budgetBase;           // 60.0
    public float budgetExponent;       // 1.60
    public float budgetScale;          // 22.0   budget = (base + pow(wave, exp) * scale) * riskMult
    public float waveTimeout;          // 45.0   seconds max per wave
    public float spawnRingMin;         // 12.0   spawn radius from arena centre
    public float spawnRingMax;         // 16.0
    public float interWaveDelay;       // 1.20   seconds before the upgrade screen
    public int   upgradeOffersShown;   // 3
}
```

Resulting budgets at RISK I:

| Wave | Budget | Typical composition |
|---|---|---|
| 1 | 82 | 8 Drifters |
| 2 | 127 | 10 Drifters, 1 Spitter |
| 3 | 190 | 12 Drifters, 2 Spitters, 1 Splitter |
| 4 | 270 | 14 Drifters, 3 Spitters, 2 Splitters |
| 5 | 367 | 16 Drifters, 4 Spitters, 3 Splitters, 1 Elite |
| 6 | 482 | 18 Drifters, 5 Spitters, 4 Splitters, 2 Elites |

Allowed enemy sets per wave: W1 `{Drifter}`, W2 `{Drifter, Spitter}`, W3+ `{Drifter, Spitter, Splitter}`, W5+ elites permitted at 20% weight.

**Clump patterns** (seed picks one per wave; uniform spawn distribution is forbidden — clumps create the multiplier spikes that make the game):

| Pattern | Shape |
|---|---|
| `Steady` | Even spawns across 70% of wave duration, then a 30% pause |
| `FrontLoad` | 60% of budget in the first 25% of duration |
| `Crescendo` | Spawn rate ramps continuously; final 20% carries 45% of budget |
| `TwinPeak` | Two dense bursts at 15% and 65%, quiet between |

## Boss — THE AUDITOR

```csharp
[System.Serializable] public struct BossTuning
{
    public int   maxHP;                 // 900
    public float phase2Threshold;       // 0.66
    public float phase3Threshold;       // 0.33
    public float phaseTransitionInvuln; // 1.00  seconds
    public float laserRotationSpeed;    // 40.0  degrees/second
    public int   laserSpokes;           // 6
    public float laserWidth;            // 0.55  world units
    public float drifterSpawnInterval;  // 4.00  seconds (P1)
    public float auditDrainRate;        // 0.08  fraction of CHARGE per second
    public float auditWindowDuration;   // 4.00  seconds
    public float auditInterval;         // 9.00  seconds between windows (P2)
    public int   fragmentCount;         // 6
    public int   fragmentHP;            // 60
    public float fragmentOrbitSpeed;    // 55.0  degrees/second
    public float fragmentOrbitRadius;   // 5.00
    public float victorySlowMoDuration; // 1.50  seconds at timeScale 0.25
}
```

## Arena

```csharp
[System.Serializable] public struct ArenaTuning
{
    public float floorRadius;          // 18.0
    public float vaultRadius;          // 14.0
    public float floorOrthoSize;       // 12.0
    public float vaultOrthoSize;       // 10.0
    public float environmentLerpTime;  //  1.50  seconds of colour-grade crossfade
    public float hazardCycleDuration;  //  4.00  seconds, NOT seeded (must be learnable)
    public int   hazardBarCount;       //  3
    public float hazardBarWidth;       //  0.70
    public float hazardTelegraphTime;  //  0.80  seconds of warning before a bar becomes lethal
}
```

## Feel — juice constants

```csharp
[System.Serializable] public struct FeelTuning
{
    // Hitstop (timeScale freeze; all durations are UNSCALED)
    public float hitstopOnKill;        // 0.045  at timeScale 0.02
    public float hitstopOnPlayerHit;   // 0.090
    public float hitstopScale;         // 0.02

    // Screen shake: shake = trauma^2
    public float traumaOnKill;         // 0.15
    public float traumaOnPlayerHit;    // 0.55
    public float traumaOnBankComplete; // 0.35
    public float traumaOnBossPhase;    // 0.70
    public float traumaDecayRate;      // 1.80  per second
    public float shakeMaxOffset;       // 0.60  world units
    public float shakeMaxRotation;     // 3.00  degrees
    public float shakeFrequency;       // 22.0  Hz (Perlin)

    // Camera
    public float zoomPunchAmount;      // 0.04  fraction of ortho size
    public float zoomPunchDuration;    // 0.08  seconds
    public float cameraFollowLerp;     // 6.00  units/s toward (player + aim offset * 0.25)

    // Post-processing reactive values
    public float caBase;               // 0.08  chromatic aberration
    public float caOnHit;              // 0.40
    public float caDuringBank;         // 0.25
    public float vignetteBase;         // 0.32
    public float vignetteDuringBank;   // 0.55
    public float bankDesaturation;     // 0.85  how grey the world goes during a bank
}
```

## RISK tiers

```csharp
[System.Serializable] public struct RiskTier
{
    public string id;                  // "RISK_I" ... "RISK_V"
    public float  enemySpeedMultiplier;
    public float  enemyBudgetMultiplier;
    public float  scoreMultiplier;
}
```

| Tier | Enemy speed | Budget | Score | Unlocked at Credit Rank |
|---|---|---|---|---|
| RISK I | 1.00 | 1.00 | 1.0 | 1 (default) |
| RISK II | 1.20 | 1.15 | 1.3 | 3 |
| RISK III | 1.35 | 1.30 | 1.7 | 6 |
| RISK IV | 1.50 | 1.50 | 2.2 | 9 |
| RISK V | 1.70 | 1.75 | 3.0 | 12 |

## Upgrades

| Line | T1 | T2 | T3 |
|---|---|---|---|
| **SPREAD** | `+1` projectile, cone `15°` | `+2` projectiles, cone `25°` | `+4` projectiles, cone `40°`, damage `×0.80` |
| **VOLATILE** | explosion on kill: radius `1.6`, damage `0.40×` | radius `2.6`, chain depth `1` | chain depth `∞`, chained kills grant full MULT |
| **LEECH** | bank `1.20 → 0.95` | bank `→ 0.70`, dash refunds `0.30s` of progress | bank `→ 0.50`, grants `0.50s` i-frames on completion |
| **GREED** | `multPerKill +0.25`, `multDecayRate +0.50` | `multPerKill +0.50`, `multDecayRate +1.00`, **cap removed** | enemy values `×1.50`, `chargeLossOnHit → 1.00` |

Explosion damage is applied once per enemy per explosion; chain depth `∞` is capped at 40 iterations per frame to bound the worst case.

## Credit Rank thresholds (lifetime banked score)

| Rank | Threshold | Unlock |
|---|---|---|
| 1 | 0 | — |
| 2 | 25,000 | Custom Seed mode |
| 3 | 75,000 | RISK II |
| 4 | 150,000 | Loadout: *Scattergun* (start with SPREAD T1) |
| 5 | 280,000 | Cosmetic: Ion trail |
| 6 | 480,000 | RISK III |
| 7 | 800,000 | Loadout: *Hair Trigger* (start with LEECH T1) |
| 8 | 1,300,000 | Modifier: *Glass* (1 HP, score ×2) |
| 9 | 2,100,000 | RISK IV |
| 10 | 3,400,000 | Loadout: *Bankrupt* (start at MULT ×5, cannot bank until wave 3) |
| 11 | 5,500,000 | Cosmetic: Gold chassis |
| 12 | 9,000,000 | RISK V — THE LEDGER |

Curve is ~1.6× per rank, tuned so rank 12 lands around 15–20 hours of play.

---

## Added during M0/M1 implementation

These were required by code that the original document did not cover. Per CLAUDE.md §4 they are
recorded here first and referenced from `TuningConfig` second — none of them is a literal in
gameplay code.

### Structs promoted out of prose

`EliteTuning`, `PoolTuning` and `SimTuning` are now real `TuningConfig` blocks. The elite
modifiers and pool sizes above were previously prose; the values are unchanged.

```csharp
[System.Serializable] public struct EliteTuning
{
    public float hpMultiplier;      // 2.50   (as above)
    public float speedMultiplier;   // 1.25
    public float valueMultiplier;   // 2.50
    public float budgetMultiplier;  // 2.50
    public float scaleMultiplier;   // 1.40
    public float outlineWidth;      // 0.06
    public float emissionBoost;     // 1.60   NEW — elites also read hotter, not just bigger
}

[System.Serializable] public struct PoolTuning
{
    public int drifter;             // 220    (as above)
    public int spitter;             //  40
    public int splitter;            //  60
    public int enemyProjectile;     // 256
    public int puddle;              //  24
}

[System.Serializable] public struct SimTuning
{
    public float gridCellSize;         // 2.00  broad-phase cell (ARCH §7)
    public float separationRadius;     // 0.45  NEW — half the desired gap between two drifters
    public float separationStrength;   // 3.50  NEW — units/s of push at full overlap
    public float maxEnemyRadius;       // 0.70  NEW — grid query padding; must be >= the largest
                                       //       elite hitbox (spitter 0.45 x 1.40 = 0.63)
}
```

### New fields on existing structs

| Struct | Field | Value | Why |
|---|---|---|---|
| `PlayerTuning` | `invulnBlinkHz` | `12.0` | The 12 Hz blink was specified in prose in FR-104 but had no field |
| `PlayerTuning` | `deathBeatDuration` | `1.00` | The death beat before restart becomes available (M1-T14) |
| `WaveTuning` | `introDuration` | `1.50` | The pre-wave intro beat, specified in ARCH §5 but not tunable |
| `FeelTuning` | `cameraAimLead` | `0.25` | Was a literal `0.25` inside the camera formula in docs/04 §5 |

### M1-only scaffolding — REMOVED at M2-T07

`Wave.m1SpawnInterval`, `m1SpawnIntervalMin`, `m1SpawnRampPerSecond`, `m1MaxAlive` and the
`EnemySpawner` component that read them existed only so the Day-3 gate had something to fight.
Both were deleted the moment the real `WaveGenerator`-driven wave loop landed. If you still have
a `TuningConfig.asset` generated before this change, re-run `Overdraft > Build Project` — it
deletes and recreates the asset from scratch, so the stale fields simply disappear.

---

## Added during M2 Day-4 implementation (enemies + generation)

### `EnemyCombatTuning` — new struct

`docs/02`'s `EnemyDefinition` schema has `projectileSpeed` for SPITTER but no lifetime or hitbox
radius for the projectile it fires — both are required to pool one. These aren't per-enemy-type
(only one enemy fires projectiles today), so they live in `TuningConfig` directly rather than on
`EnemyDefinition`.

```csharp
[System.Serializable] public struct EnemyCombatTuning
{
    public float projectileLifetime;   // 3.00  seconds (9.0 preferredRange / 6.0 speed = 1.5s
                                        //       minimum flight time, tripled for margin)
    public float projectileRadius;     // 0.18  slightly larger than the player's own (0.15)
}
```

### `WaveTuning` — weighted-selection inputs for `WaveGenerator` (FR-453)

The tuning document specified the budget FORMULA but never gave the actual per-type weights the
weighted-selection loop needs, or a home for `EliteWeightPercent`'s "20" value referenced in the
wave table footnote. Cost fields mirror the matching `EnemyDefinition.budgetCost` — keep both in
sync if either changes.

```csharp
public int drifterWeight;       // 70
public int spitterWeight;       // 20
public int splitterWeight;      // 10
public int drifterCost;         // 10   mirrors Enemies/Drifter.asset
public int spitterCost;         // 40   mirrors Enemies/Spitter.asset
public int splitterCost;        // 25   mirrors Enemies/Splitter.asset
public int eliteWeightPercent;  // 20   wave 5+ only
```

### `EnemyDefinition` — two new Splitter fields

The Splitter row in the enemy table lists **two** speeds ("6.5 (charge) / 1.5 (approach)"), but
`EnemyDefinition.moveSpeed` is a single field. `moveSpeed` is now specifically the CHARGE speed;
a new `approachSpeed` field carries the slower one. `chargeCooldown` (new) is the pause between
charge attempts, needed by the approach→windup→charge→approach state loop but not present in the
original schema.

```csharp
public float approachSpeed;    // 1.50   Splitter only
public float chargeCooldown;   // 1.60   Splitter only — pause after a charge before approaching again
```

### Generation algorithm notes (not new tunables, but decisions made while implementing FR-454)

The four clump patterns map an ordinal spawn fraction `t ∈ [0,1]` to a time fraction via a fixed
shape function per pattern (`WaveGenerator.Shape`, internal, unit-tested directly):

| Pattern | Shape |
|---|---|
| `Steady` | `f(t) = t × 0.70` |
| `FrontLoad` | piecewise: 60% of spawns → first 25% of duration |
| `Crescendo` | `f(t) = t^2.7` (continuous ramp) |
| `TwinPeak` | two bursts centred at `t=0.15` and `t=0.65`, width `0.12` |

A small ±2% jitter is layered on top so spawns inside a cluster don't land on a visibly perfect
curve. None of this is exposed as a tunable — it is generation logic, not a numeric dial — but is
recorded here since the source document only specified the four patterns qualitatively.

---

## Added during M2 Day-5 implementation (waves, upgrades, Vault)

### `UpgradeTuning` — new struct

FR-473 states each upgrade line's tier effects, but SPREAD and VOLATILE's actual numbers had no
`TuningConfig` home (LEECH already lives in `BankTuning`'s `leechT1/2/3Duration`; GREED's deltas
live on `MultiplierSystem.BonusPerKill/BonusDecayRate`, so only their source constants were
missing). **Each tier field is the ABSOLUTE value once that tier is owned, not a delta stacked
on the previous tier** — the same idiom `leechT1/2/3Duration` already uses.

```csharp
[System.Serializable] public struct UpgradeTuning
{
    public int   spreadT1Projectiles;       // 2      total shots, not "+1"
    public float spreadT1ConeDegrees;       // 15
    public int   spreadT2Projectiles;       // 3
    public float spreadT2ConeDegrees;       // 25
    public int   spreadT3Projectiles;       // 5
    public float spreadT3ConeDegrees;       // 40
    public float spreadT3DamageMultiplier;  // 0.80

    public float volatileT1Radius;          // 1.6
    public float volatileT1DamageFraction;  // 0.40  of the player's own projectileDamage
    public float volatileT2Radius;          // 2.6   (T3 reuses T2's radius/fraction)
    public int   volatileT2ChainDepth;      // 1     (T3 is unbounded, safety-capped at 40/step)

    public float greedT1MultPerKillBonus;   // 0.25
    public float greedT1DecayRateBonus;     // 0.50
    public float greedT2MultPerKillBonus;   // 0.50
    public float greedT2DecayRateBonus;     // 1.00
    public float greedT3ValueMultiplier;    // 1.50
}
```

**Judgment call:** VOLATILE's "damage 0.40×" doesn't state its base. Implemented as 0.40× the
player's own `Weapon.projectileDamage` (1), floored to a minimum of 1 — at these tuning values an
explosion always deals exactly 1, one-shotting a Drifter but not a Spitter. Revisit if playtesting
shows explosions are too strong or too weak once enemy HP pools grow.

**Judgment call:** "chained kills grant full MULT" (VOLATILE T3) is implemented as the *default*
behaviour at every tier — `RunController.OnEnemyKilled` already applies full MULT/CHARGE credit to
every kill regardless of source, so T3's real differentiator is its unbounded chain depth, not a
MULT bonus that T1/T2 lack. No number in the source doc specified a T1/T2 penalty to implement.

### Environment & hazard notes (FR-457) — not new tunables, but decisions made while implementing

`ArenaTuning` already had every number `HazardBar` needed (`hazardCycleDuration`, `hazardBarWidth`,
`hazardTelegraphTime`, `hazardBarCount`). The only undocumented behaviour was the shape of the
cycle itself: for the first `hazardTelegraphTime` seconds of each `hazardCycleDuration` window the
bar is a dim, harmless telegraph; for the remainder it is lethal. The bar rotates continuously
(one full revolution per cycle) through both phases — continuity is what makes the pattern
learnable, per FR-457's explicit "not seeded, must be learnable" requirement.

### `RunController` boss placeholder

Wave 6 clearing currently transitions straight to `Results`. THE AUDITOR (M2-T11–T14, Day 6)
inserts `BossIntro → BossActive → BossDefeated → forced free bank` at that exact point. This is a
sequencing decision — building a throwaway boss placeholder now would just be rewritten on Day 6 —
not a scope cut. Flagged again in the M2-T07 task's completion note.

---

## Tuning changelog

Append a row every time a value changes. Never edit silently.

| Date | Field | From | To | Reason |
|---|---|---|---|---|
| 2026-08-06 | — | — | — | Initial values |
| 2026-08-07 | `Elite.emissionBoost` | — | `1.60` | New. Elites need a colour tell as well as a scale tell before the outline shader exists (M3) |
| 2026-08-07 | `Sim.separationRadius` | — | `0.45` | New. Required by the FR-301 "cheap overlap push" |
| 2026-08-07 | `Sim.separationStrength` | — | `3.50` | New. Same |
| 2026-08-07 | `Sim.maxEnemyRadius` | — | `0.70` | New. Grid query padding; a value below the largest elite hitbox drops contacts |
| 2026-08-07 | `Player.invulnBlinkHz` | — | `12.0` | New. Was prose in FR-104 |
| 2026-08-07 | `Player.deathBeatDuration` | — | `1.00` | New. M1-T14 |
| 2026-08-07 | `Wave.introDuration` | — | `1.50` | New. Was prose in ARCH §5 |
| 2026-08-07 | `Feel.cameraAimLead` | — | `0.25` | New. Was a literal in the docs/04 §5 formula |
| 2026-08-07 | `Wave.m1*` | — | *(removed)* | M2-T07 landed; the M1 endless spawner and its fields were deleted together |
| 2026-08-07 | `EnemyCombat.projectileLifetime` | — | `3.00` | New. Needed to pool SPITTER's projectile (M2-T01) |
| 2026-08-07 | `EnemyCombat.projectileRadius` | — | `0.18` | New. Same |
| 2026-08-07 | `Wave.drifterWeight/spitterWeight/splitterWeight` | — | `70/20/10` | New. WaveGenerator's weighted-selection input (M2-T04) |
| 2026-08-07 | `Wave.drifterCost/spitterCost/splitterCost` | — | `10/40/25` | New. Mirrors EnemyDefinition.budgetCost |
| 2026-08-07 | `Wave.eliteWeightPercent` | — | `20` | New. Was a footnote ("20% weight"), had no field |
| 2026-08-07 | `EnemyDefinition(Splitter).approachSpeed` | — | `1.50` | New. The table listed two speeds, the schema had one field |
| 2026-08-07 | `EnemyDefinition(Splitter).chargeCooldown` | — | `1.60` | New. Needed by the approach/windup/charge state loop |
| 2026-08-07 | `Upgrades.spread*` | — | see above | New. SPREAD tier numbers had no TuningConfig home (M2-T10) |
| 2026-08-07 | `Upgrades.volatile*` | — | see above | New. Same, VOLATILE |
| 2026-08-07 | `Upgrades.greed*` | — | see above | New. Same, GREED's deltas |
