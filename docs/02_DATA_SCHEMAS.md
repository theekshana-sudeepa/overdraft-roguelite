# 02 — DATA SCHEMAS

Every schema here is versioned. **Never change a serialised field name** once it holds player data; add a new field and migrate.

---

## 1. `SaveData`

```csharp
// Scripts/Progression/SaveData.cs
using System;

namespace Overdraft.Progression
{
    [Serializable]
    public sealed class SaveData
    {
        public const int CurrentVersion = 1;

        public int    version = CurrentVersion;

        // --- Progression ---
        public long   lifetimeBankedScore;
        public int    creditRank = 1;
        public long   highScoreEndless;
        public long[] highScorePerRisk = new long[5];
        public long   bestBossTimeMs = 0;              // 0 = never beaten

        // --- Steam achievement mirror (so offline unlocks sync later) ---
        public bool[] achievementsUnlocked = new bool[10];

        // --- Daily ---
        public int    lastDailySeedPlayed = -1;
        public long   lastDailyScore;

        // --- Stats (drive achievements and the results screen) ---
        public int    runsStarted, runsCompleted, runsDied, bossKills;
        public int    totalKills, totalBanks;
        public long   largestSingleBank;
        public float  highestMultiplier;
        public int    dailiesCompleted;

        // --- Cosmetics / loadout ---
        public string selectedLoadout  = "default";
        public string selectedCosmetic = "default";
        public bool   glassModifierOn;

        // --- Settings ---
        public Settings settings = new Settings();

        // --- Integrity ---
        public string checksum;        // salted SHA-256 of everything above; see §4
        public bool   tampered;        // runtime-only, never trusted from disk
    }

    [Serializable]
    public sealed class Settings
    {
        public float masterVolume = 1.00f;
        public float musicVolume  = 0.70f;
        public float sfxVolume    = 0.85f;
        public float screenShake  = 1.00f;   // 0..1 user scale on FeelTuning shake values
        public int   colorblindMode;         // 0 none, 1 deuter/prot, 2 tritan
        public int   qualityPreset = -1;     // -1 auto-detect on first launch, 0 low, 1 high
        public int   fullscreenMode = 1;     // 0 windowed, 1 borderless (DEFAULT), 2 exclusive
        public string locale = "";           // "" = auto-detect from Steam/OS on first launch
    }
}
```

### Migration
```csharp
static void Migrate(SaveData d)
{
    if (d.version < 1) { /* nothing before v1 */ }
    // Future: if (d.version < 2) { d.newField = default; }
    // A save with version > CurrentVersion is NOT loaded — see FR-524.
    d.version = SaveData.CurrentVersion;
}
```

### Failure handling (FR-524)

| Condition | Behaviour |
|---|---|
| File missing | New `SaveData` with defaults. Silent. |
| File empty / zero bytes | Defaults. Rename original to `save.corrupt.json`. Log warning. |
| JSON parse failure | Defaults. Rename to `save.corrupt-<unixtime>.json`. **Never delete the user's file.** |
| `version > CurrentVersion` | Do **not** load. Defaults in memory, do **not** overwrite the file, show a non-blocking "save from a newer version" notice. |
| Checksum mismatch | Load normally, set `tampered = true` in memory only. Progress preserved; leaderboard submission suppressed (FR-525). |

### Atomic write (FR-522)
```csharp
string tmp   = path + ".tmp";
File.WriteAllText(tmp, json);
if (File.Exists(path)) { File.Replace(tmp, path, null); }   // atomic on Win + macOS
else                   { File.Move(tmp, path); }
```

---

## 2. `EnemyDefinition` (ScriptableObject)

```csharp
[CreateAssetMenu(menuName = "Overdraft/Enemy Definition")]
public sealed class EnemyDefinition : ScriptableObject
{
    public EnemyType type;
    public int   maxHP;
    public float moveSpeed;
    public float hitboxRadius;
    public int   scoreValue;
    public int   budgetCost;
    public int   contactDamage;
    public Color bodyColor;
    public Mesh  mesh;

    [Header("Spitter")]
    public float preferredRange;
    public float fireInterval;
    public float projectileSpeed;
    public float puddleDuration;
    public float puddleRadius;

    [Header("Splitter")]
    public float approachSpeed;      // added M2-T01: the enemy table lists two speeds for
                                      // Splitter ("6.5 charge / 1.5 approach"); moveSpeed above
                                      // is now specifically the charge speed
    public float windupDuration;
    public float chargeDuration;
    public float chargeCooldown;     // added M2-T01: pause between charge attempts
    public int   splitCount;
    public float splitScatterRadius;
}
```

Assets: `Data/Enemies/Drifter.asset`, `Spitter.asset`, `Splitter.asset`. Values from `docs/00_TUNING.md`.

**Elite is not an asset.** It is a `bool isElite` on the pooled instance; multipliers come from `TuningConfig`.

---

## 3. `WaveRules` (pure struct, lives in `Generation/`)

```csharp
// NO UnityEngine
namespace Overdraft.Generation
{
    public readonly struct WaveRules
    {
        public readonly int   WaveIndex;          // 1..6
        public readonly EnemyType[] AllowedTypes;
        public readonly int[] Weights;            // parallel to AllowedTypes
        public readonly int[] Costs;              // parallel to AllowedTypes
        public readonly bool  ElitesAllowed;
        public readonly int   EliteWeightPercent; // 20
        public readonly float DurationSeconds;
        public readonly float RingMin, RingMax;
        public readonly float EliteCostMultiplier;
    }

    public enum ClumpPattern { Steady, FrontLoad, Crescendo, TwinPeak }
    public enum EnemyType    { Drifter, Spitter, Splitter }
}
```

`WaveRules` is built by `RunController` from the `TuningConfig` and passed in. `Generation/` never reads a ScriptableObject.

---

## 4. Checksum

```csharp
static string ComputeChecksum(SaveData d)
{
    string saved = d.checksum;
    d.checksum = null;                              // exclude the field from its own hash
    string json = JsonUtility.ToJson(d, false);
    d.checksum = saved;

    using var sha = System.Security.Cryptography.SHA256.Create();
    byte[] bytes = System.Text.Encoding.UTF8.GetBytes(json + Salt);
    return System.Convert.ToHexString(sha.ComputeHash(bytes));
}
```

`Salt` is a `const string` in a partial class excluded from the public repo. **Be honest about what this is:** it stops casual JSON editing from polluting the global leaderboard. It does not stop anyone determined, and it is not presented to the player as anti-cheat. It never blocks play and never shows an accusatory message.

---

## 5. `RunProof` — leaderboard details blob

Steam leaderboards accept up to 64 `int32` details. Layout is fixed; **never reorder** or historical entries become unreadable.

| Index | Field |
|---|---|
| 0 | schema version (currently 1) |
| 1 | run seed |
| 2 | run duration, milliseconds |
| 3 | total kills |
| 4 | total banks |
| 5 | largest single bank (clamped to `int.MaxValue`) |
| 6 | highest multiplier × 100 |
| 7 | risk tier index 0–4 |
| 8 | loadout id hash (stable, from a fixed enum — not a string hash) |
| 9 | flags bitfield: bit0 glass, bit1 boss killed, bit2 daily, bit3 tampered-save |
| 10–21 | upgrade tier taken per line: `[spread, volatile, leech, greed]` × 3 slots |
| 22 | client build number |
| 23–63 | reserved, zero |

### Plausibility filter (client-side, display only)
```
maxPlausibleScorePerSecond(risk) = 2600 * riskScoreMultiplier
entry is hidden from the displayed board if:
    score / (durationMs/1000) > maxPlausibleScorePerSecond
 OR durationMs < 60_000                       (no legitimate run is under a minute)
 OR flags has bit3 set                        (self-reported tampered save)
```

Filtering is presentational only. Never report a user, never show "cheater," never block their client.

---

## 6. Localisation CSV

`Assets/_Project/Localization/strings.csv`, UTF-8 with BOM, comma-delimited, quoted fields.

```csv
key,en,zh-Hans,ru,pt-BR,es-419,de,fr,ja,ko
hud.unbanked,UNBANKED,未存入,НЕ ЗАЧИСЛЕНО,NÃO DEPOSITADO,SIN DEPOSITAR,NICHT GEBUCHT,NON ENCAISSÉ,未入金,미입금
hud.wave,WAVE {0} / {1},...
menu.play,PLAY,...
```

Rules:
- Keys are `screen.element`, lowercase, dot-separated. Never reuse a key with different meaning.
- `{0}`-style placeholders only. Never concatenate translated fragments — word order differs by language.
- **German and Russian run ~35% longer than English.** Every button auto-sizes or the string is capped at the key's declared max length; a validator in `Overdraft.Editor` fails the build on overflow.
- CJK requires TMP fallback assets (`Noto Sans SC/JP/KR`) assigned on the primary font asset. Use **dynamic SDF** for CJK — a static 3,000-glyph atlas blows the build-size budget.
- Target ~120 keys total. If you're past 200, the game has too much text for its own good.

---

## 7. Steam Cloud file map

| Platform | Root | Path | Pattern |
|---|---|---|---|
| Windows | `WinAppData` | `Unity/OVERDRAFT` | `save.json` |
| macOS | `MacHome` | `Library/Application Support/OVERDRAFT` | `save.json` |

Quota: 1 MB, 4 files. `save.corrupt*.json` is **not** synced (excluded by pattern) — never upload a broken file to the cloud.

**Conflict resolution:** on boot, compare cloud vs local mtime. If cloud is newer by > 5 seconds, show a modal: *"A newer save was found in Steam Cloud. Use cloud save / Use local save."* Default focus on **Use cloud**. Never resolve silently in either direction.
