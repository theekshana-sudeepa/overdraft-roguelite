# 03 — STEAM INTEGRATION

Library: **Steamworks.NET** (MIT). Assembly: `Overdraft.Steam`, isolated so the game builds and runs without it.

---

## 1. The guard — every Steam call goes through this

```csharp
// Scripts/Steam/Steam.cs
namespace Overdraft.SteamIntegration
{
    public static class Steam
    {
        public static bool Ready => SteamManager.Initialized;

        public static void Try(System.Action call)
        {
            if (!Ready) { return; }
            try { call(); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[Steam] {e.Message}"); }
        }

        public static T Try<T>(System.Func<T> call, T fallback)
        {
            if (!Ready) { return fallback; }
            try { return call(); }
            catch (System.Exception e) { UnityEngine.Debug.LogWarning($"[Steam] {e.Message}"); return fallback; }
        }
    }
}
```

**FR-506 is absolute:** every feature degrades silently. Steam offline means no leaderboards and no cloud — it never means a blocked menu, an error popup, or a disabled Play button. Test this by killing `steam.exe` mid-session.

---

## 2. Initialisation and ownership (FR-501)

```csharp
// Scripts/Steam/SteamManager.cs — runs in 00_Boot, DontDestroyOnLoad
const uint AppId = 0; // TODO: real AppID once created in Steamworks

void Awake()
{
    if (instance != null) { Destroy(gameObject); return; }
    instance = this; DontDestroyOnLoad(gameObject);

#if !UNITY_EDITOR
    // Relaunches through the Steam client if the exe was started directly.
    // Returns true → we must quit immediately; Steam restarts us.
    if (SteamAPI.RestartAppIfNecessary(new AppId_t(AppId)))
    {
        Application.Quit();
        return;
    }
#endif

    Initialized = SteamAPI.Init();
    if (!Initialized) { Debug.LogWarning("[Steam] Init failed — running offline."); }
}

void Update()      { if (Initialized) { SteamAPI.RunCallbacks(); } }
void OnDestroy()   { if (Initialized) { SteamAPI.Shutdown(); Initialized = false; } }
```

Soft ownership check, **for feature-gating only**:
```csharp
bool owned = Steam.Try(() => SteamApps.BIsSubscribed(), true);   // fallback TRUE, deliberately
if (!owned) { leaderboardsEnabled = false; achievementsEnabled = false; }
```

> **State this plainly and never overclaim:** `RestartAppIfNecessary` is a launcher convenience, not protection. It is trivially bypassed. There is no DRM in this game and none should be added. Anti-piracy here is a $6.99 price, working leaderboards, cloud saves, and shipping patches. Do **not** build key checks, server pings, integrity scans, or anything that could false-positive on a legitimate offline or Family-Sharing user. The fallback for the ownership check is `true` on purpose — failing open is correct.

**`steam_appid.txt`** goes in the project root for editor testing and must be excluded from both shipping depots (`verify.sh` checks this before a release build).

---

## 3. Achievements (FR-502)

| # | API name | Display name | Condition | Design target % |
|---|---|---|---|---|
| 0 | `FIRST_DEPOSIT` | First Deposit | Complete any bank | ~92% |
| 1 | `IN_THE_BLACK` | In The Black | End a run with score > 0 | ~78% |
| 2 | `LIQUIDATED` | Liquidated | Lose ≥5,000 unbanked CHARGE to a single hit | ~61% |
| 3 | `AUDITED` | Audited | Defeat THE AUDITOR | ~34% |
| 4 | `COMPOUND_INTEREST` | Compound Interest | Reach MULT ×25 | ~28% |
| 5 | `CLEAN_BOOKS` | Clean Books | Clear a full wave without being hit, RISK III+ | ~19% |
| 6 | `DAY_TRADER` | Day Trader | Complete 7 Daily Challenges | ~14% |
| 7 | `MARGIN_CALL` | Margin Call | Bank ≥20,000 in one deposit | ~8% |
| 8 | `THE_LEDGER` | The Ledger | Defeat THE AUDITOR on RISK V | ~2.5% |
| 9 | `NEVER_BANKED` | Never Banked | Defeat THE AUDITOR having banked exactly once, in P3 | ~0.9% |

The first three are calibrated to fire inside a new player's first two runs. That early unlock cadence is a retention mechanic, not decoration — do not make them harder.

### Implementation
```csharp
public sealed class SteamAchievements : MonoBehaviour
{
    bool dirty;

    public void Unlock(AchievementId id)
    {
        var save = ServiceLocator.Save.Data;
        if (save.achievementsUnlocked[(int)id]) { return; }   // local mirror first
        save.achievementsUnlocked[(int)id] = true;
        dirty = true;
        Steam.Try(() => SteamUserStats.SetAchievement(Names[(int)id]));
        GameEvents.RaiseAchievementUnlocked(id);              // drives an in-game toast for offline players
    }

    /// Called ONCE at run end. Never per-unlock — hammering StoreStats gets rate-limited.
    public void Flush()
    {
        if (!dirty) { return; }
        dirty = false;
        Steam.Try(() => SteamUserStats.StoreStats());
    }
}
```

**On boot:** `RequestCurrentStats`, then reconcile — the union of local and Steam. If Steam has an achievement the local save doesn't, set it locally; if local has one Steam doesn't (unlocked offline), push it. This is what makes offline play work correctly.

Test with `SteamUserStats.ResetAllStats(true)` between verification passes.

---

## 4. Leaderboards (FR-503)

| Handle | Sort | Display | Notes |
|---|---|---|---|
| `ENDLESS_ALLTIME` | Descending | Numeric | All RISK tiers, score already includes the tier multiplier |
| `RISK_V` | Descending | Numeric | Prestige board |
| `DAILY_<seed>` | Descending | Numeric | Created lazily per seed. One entry per user, **first attempt only**. |
| `BOSS_TIME` | **Ascending** | TimeMilliSeconds | Fastest AUDITOR kill |

```csharp
public sealed class SteamLeaderboards : MonoBehaviour
{
    public void Submit(string boardName, int score, int[] proof, ELeaderboardUploadScoreMethod method)
    {
        if (!Steam.Ready) { return; }
        if (ServiceLocator.Save.Data.tampered) { return; }   // FR-525
        // FindOrCreateLeaderboard → CallResult → UploadLeaderboardScore(handle, method, score, proof, proof.Length)
    }

    public void RequestFriendEntries(string boardName, System.Action<Entry[]> onDone);
    public void RequestGlobalRank(string boardName, System.Action<int, int> onDone); // rank, totalEntries
}
```

Daily uses `k_ELeaderboardUploadScoreMethodKeepBest` **off** — first attempt only. Enforce locally with `SaveData.lastDailySeedPlayed`, and refuse a second submission client-side.

### Results screen ordering — this matters more than it looks

Show **friends first**, always:

```
  ┌─ TODAY'S DAILY ─────────────────┐
  │  1.  Mika        41,200         │
  │  2.  YOU         38,940   ◀     │   ← friends board, you highlighted
  │  3.  Alex        31,105         │
  └─────────────────────────────────┘
        GLOBAL: TOP 12%                 ← percentile, never a raw rank
```

"You beat Alex by 400" drives another run. "You are 8,412th of 40,000" drives quitting. Never show a raw global rank number outside the top 1,000.

---

## 5. Cloud save (FR-504)

Configure **Auto-Cloud** in Steamworks → App Admin → Cloud. No code required for the sync itself.

| Setting | Value |
|---|---|
| Windows root / path | `WinAppData` → `Unity/OVERDRAFT` |
| macOS root / path | `MacHome` → `Library/Application Support/OVERDRAFT` |
| Pattern | `save.json` |
| Excluded | `save.corrupt*.json`, `*.tmp` |
| Quota | 1 MB, 4 files |

Add only the conflict prompt (see `docs/02_DATA_SCHEMAS.md` §7). Never resolve silently.

---

## 6. Rich presence (FR-505)

```csharp
Steam.Try(() => {
    SteamFriends.SetRichPresence("steam_display", "#Status_Run");
    SteamFriends.SetRichPresence("mode",   modeLabel);      // "ENDLESS" | "DAILY #47" | "SEED 88213"
    SteamFriends.SetRichPresence("wave",   waveIndex.ToString());
    SteamFriends.SetRichPresence("charge", chargeShort);    // "8.4k"
});
```

Update on wave change and on bank completion only — **not per frame** (rate-limited to ~20 calls/minute by Steam).

Token file uploaded to Steamworks:
```
"#Status_Menu"    "In the menus"
"#Status_Run"     "%mode% · Wave %wave% · %charge% unbanked"
"#Status_Boss"    "%mode% · THE AUDITOR · %charge% unbanked"
```

A friends list showing *"DAILY #47 · Wave 6 · 41.2k unbanked"* is quietly one of the best organic marketing channels you have. It costs 20 lines.

---

## 7. Demo app (FR — see PRD §Demo)

A **separate AppID**, sharing the codebase behind a scripting define.

```
Scripting Define Symbols (Demo build): OVERDRAFT_DEMO
```

| Feature | Full | Demo |
|---|---|---|
| Waves | 6 + boss | 1–3, then hard stop |
| Environment | Floor + Vault | Floor only |
| Enemies | Drifter, Spitter, Splitter | Drifter, Spitter |
| Upgrades | All 4 lines | SPREAD, VOLATILE |
| Boss | Yes | No — 8s teaser video on the end screen |
| Meta progression | Yes | No |
| Leaderboards | Yes | **No** |
| Achievements | 10 | None |
| Save file | `save.json` | `save_demo.json` — **separate, no carry-over** |
| End screen | Results | Score + **"Wishlist to see THE VAULT"** button → `steam://url/StoreAppPage/<AppID>` |

Gate with `#if OVERDRAFT_DEMO` at the **run-configuration level** (a `RunConfig` asset), not scattered through gameplay code. One asset difference, one define.

---

## 8. Steamworks setup checklist (human task, not the agent's)

- [ ] App created; AppID recorded in `SteamManager.AppId` and `steam_appid.txt`
- [ ] Depots: `win64`, `macos`, plus demo depots under the demo AppID
- [ ] 10 achievements created with icons (locked + unlocked, 64×64)
- [ ] 4 leaderboards created (or left to `FindOrCreate` at runtime — either is fine)
- [ ] Auto-Cloud configured for both platforms
- [ ] Rich presence localisation token file uploaded
- [ ] `steam_appid.txt` excluded from both shipping depots
- [ ] Overlay tested: Shift+Tab opens, input focus returns correctly, F12 screenshot works
- [ ] Full run completed with the Steam client **killed**, verifying FR-506
