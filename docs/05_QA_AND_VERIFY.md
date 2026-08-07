# 05 — QA, TESTS & THE VERIFICATION LOOP

The agent must be able to check its own work without a human. That is what this file sets up.

---

## 1. `tools/verify.sh` — run before reporting any task complete

```bash
#!/usr/bin/env bash
# tools/verify.sh — OVERDRAFT verification loop
set -uo pipefail
FAIL=0
UNITY="${UNITY_PATH:-/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity}"
PROJ="$(cd "$(dirname "$0")/.." && pwd)"
mkdir -p "$PROJ/build/logs"

say() { printf "\n\033[1m== %s\033[0m\n" "$1"; }
bad() { printf "\033[31mFAIL: %s\033[0m\n" "$1"; FAIL=1; }

# ---------- 1. Static bans (fast, run first) ----------
say "Static analysis"
S="$PROJ/Assets/_Project/Scripts"

grep -rn "UnityEngine" "$S/Generation/" 2>/dev/null \
  && bad "UnityEngine referenced in Generation/ — breaks determinism (ARCH §1)"

grep -rnE "FindObjectOfType|GameObject\.Find|SendMessage" "$S" 2>/dev/null \
  && bad "Banned lookup API in Scripts/ (CLAUDE.md §4)"

grep -rnE "using System\.Linq" "$S/Player/" "$S/Enemies/" "$S/Economy/" "$S/Generation/" 2>/dev/null \
  && bad "LINQ in a hot-path folder (CLAUDE.md §4)"

grep -rn "Instantiate(" "$S" 2>/dev/null | grep -vE "ObjectPool|Bootstrap|Editor" \
  && bad "Runtime Instantiate outside the pool (C3)"

grep -rn "UnityEngine.Random" "$S/Generation/" "$S/Core/RunController.cs" 2>/dev/null \
  && bad "UnityEngine.Random in a generation path (NFR-07)"

grep -rn "Camera.main" "$S" 2>/dev/null | grep -v "Awake\|Start" \
  && bad "Camera.main outside Awake/Start"

# ---------- 2. Compile + EditMode tests ----------
say "Compile + EditMode tests"
"$UNITY" -batchmode -nographics -projectPath "$PROJ" \
  -runTests -testPlatform EditMode \
  -testResults "$PROJ/build/logs/results.xml" \
  -logFile "$PROJ/build/logs/tests.log" -quit
[ $? -ne 0 ] && bad "Unity tests failed — see build/logs/tests.log"

grep -qE 'result="Failed"' "$PROJ/build/logs/results.xml" 2>/dev/null \
  && bad "One or more EditMode tests failed"

grep -E "error CS[0-9]+" "$PROJ/build/logs/tests.log" 2>/dev/null \
  && bad "Compile errors"

# ---------- 3. Release-only checks ----------
if [ "${1:-}" = "--release" ]; then
  say "Release checks"
  [ -f "$PROJ/steam_appid.txt" ] && bad "steam_appid.txt must not ship (STEAM §2)"
  grep -rn "Debug.Log(" "$S" 2>/dev/null && bad "Debug.Log left in shipping code"
  grep -rn "TODO\|FIXME" "$S" 2>/dev/null && bad "TODO/FIXME left in code"
fi

say "RESULT"
[ $FAIL -eq 0 ] && echo "✅ PASS" || { echo "❌ FAIL"; exit 1; }
```

A `tools/verify.bat` mirror exists for Windows. If Unity cannot be launched in the agent's environment, the agent must **say so explicitly** rather than claiming verification passed.

---

## 2. EditMode test suite

Only pure logic is unit-tested. UI, VFX and feel are verified by a human — trying to automate them wastes days you don't have.

### `Tests/EditMode/GenerationTests.cs`
```csharp
[Test] public void SameSeed_ProducesIdenticalWaves()            // 1000 seeds × 6 waves, deep compare
[Test] public void DifferentSeeds_ProduceDifferentWaves()       // <1% collision rate over 1000 pairs
[Test] public void WaveBudget_NeverExceeded()                   // sum(cost) <= budget, all waves, all risks
[Test] public void WaveBudget_NearlyFullySpent()                // sum(cost) >= budget - maxEnemyCost
[Test] public void Wave1_ContainsOnlyDrifters()                 // FR-453 allowed sets
[Test] public void Elites_OnlyAppearFromWave5()
[Test] public void SpawnDelays_AreWithinWaveDuration()
[Test] public void ClumpPattern_IsSeedDetermined_NotUniform()   // variance of inter-spawn gaps > threshold
[Test] public void Generate_AllocatesNothing_AfterWarmup()      // Assert.That(() => ..., Is.Not.AllocatingGCMemory())
[Test] public void RunSeed_Channels_AreIndependent()            // changing channel 2 rolls leaves channel 0 identical
[Test] public void Determinism_HashOf1000Runs_MatchesGolden()   // ← the cross-platform contract
```

`Determinism_HashOf1000Runs_MatchesGolden` hashes 1000 generated runs into one SHA-256 and compares against `Tests/EditMode/golden_determinism.txt`, committed to the repo. **Run this on both macOS and Windows.** If it ever diverges, the daily leaderboard is invalid and that is a P0 bug.

### `Tests/EditMode/EconomyTests.cs`
```csharp
[Test] public void Kill_AddsValueTimesMultiplier()                     // FR-201, floor()
[Test] public void Multiplier_RespectsCap_UnlessUncapped()             // FR-202
[Test] public void Multiplier_DecaysAfterGrace_FloorsAtOne()           // FR-203
[Test] public void Multiplier_DecayTimerResetsOnKill()
[Test] public void PlayerHit_HalvesCharge_AndResetsMultiplier()        // FR-105, FR-202
[Test] public void GreedT3_DoublesChargeLossToFull()                   // chargeLossOnHit -> 1.0
[Test] public void Bank_TransfersFullChargeAndResetsMultiplier()       // FR-205
[Test] public void Bank_AppliesRiskMultiplier()                        // FR-209
[Test] public void BankCancelled_GivesNoPartialCredit()                // FR-207
[Test] public void BankInterrupted_AppliesHitPenalty_AndLocksOut()     // FR-208
[Test] public void RunEnd_UnbankedCharge_ContributesZero()             // FR-204
[Test] public void Score_UsesLong_DoesNotOverflowAtRiskV()             // 10h of RISK V
[Test] public void Audit_DrainsChargeOnly_NeverScore()                 // FR-403
[Test] public void PerfectDash_AwardsBonusOnce_PerDash()               // FR-107
[Test] public void LeechTiers_ReduceBankDuration_Correctly()
```

### `Tests/EditMode/SaveTests.cs`
```csharp
[Test] public void Save_RoundTrips_AllFields()
[Test] public void Save_IsAtomic_UnderSimulatedFailure()               // FR-522
[Test] public void Load_RecoversFrom_MissingFile()                     // FR-524
[Test] public void Load_RecoversFrom_EmptyFile()
[Test] public void Load_RecoversFrom_TruncatedJson()
[Test] public void Load_RecoversFrom_FutureVersion_WithoutOverwriting()
[Test] public void Load_PreservesCorruptFile_AsBackup()                // never delete user data
[Test] public void TamperedSave_KeepsProgress_BlocksLeaderboard()      // FR-525
[Test] public void CreditRank_AdvancesAtCorrectThresholds()            // FR-481
```

### `Tests/EditMode/DailyTests.cs`
```csharp
[Test] public void DailySeed_IsUtcStable_AcrossTimezones()             // FR-455 — set TZ, assert equal
[Test] public void DailySeed_ChangesAtUtcMidnight_Exactly()
[Test] public void Daily_SecondAttempt_IsRefused()
[Test] public void CustomSeed_ParsesInt_NeverStringHashCode()          // ARCH §6 warning
```

### `Tests/EditMode/SteamOfflineTests.cs`
```csharp
[Test] public void AllSteamCalls_NoOpWhenUninitialized()               // FR-506
[Test] public void Achievements_BatchedToOneStoreCall()                // FR-502
[Test] public void OwnershipCheck_FailsOpen()                          // fallback must be TRUE
```

---

## 3. Manual QA matrix

Run the relevant block at each milestone gate. ⛔ = has caused a shipped bug in a comparable project.

### Core loop
| # | Case | Expected |
|---|---|---|
| 1 | Bank while the last enemy of a wave dies | ⛔ Wave must not advance mid-bank; bank completes, *then* wave clears |
| 2 | Bank at the exact frame the boss changes phase | ⛔ No double-transition, no lost charge |
| 3 | Release bank at 99% | No credit, no penalty, progress resets |
| 4 | Take a hit at 99% of a bank | Hit penalty applies, bank fails, lockout starts |
| 5 | Dash during a bank | Input ignored (or refunds progress if LEECH T2) |
| 6 | Bank with CHARGE = 0 | Completes harmlessly, no divide-by-zero, no stinger |
| 7 | MULT at exactly the cap, kill one more | No overflow, no visual glitch |
| 8 | Die during a bank | Death takes priority; results screen shows unbanked as lost |

### Generation & daily
| # | Case | Expected |
|---|---|---|
| 9 | Same seed twice, deep-compare wave lists | Identical |
| 10 | Same seed on Mac and Windows | ⛔ Identical (this is the leaderboard's whole validity) |
| 11 | UTC day rolls over **mid-run** during a daily | ⛔ Run completes and submits to the *original* seed's board |
| 12 | Play daily, quit mid-run, relaunch | Attempt is consumed; retry refused with a clear message |
| 13 | Custom seed: paste `0`, `-1`, `2147483647`, `abc`, empty | All handled; no crash, no `GetHashCode` path |

### Input
| # | Case | Expected |
|---|---|---|
| 14 | Unplug the controller mid-run | ⛔ Pause + reconnect prompt, no soft-lock |
| 15 | Unplug during a bank hold | Bank cancels cleanly |
| 16 | Plug in a controller while using mouse | Prompts swap glyphs; no mode dialog |
| 17 | DualSense over **Bluetooth** on Windows | ⛔ Enumerates and binds correctly (most common controller bug) |
| 18 | Both mouse and gamepad used within one second | No thrash, last-device-wins |
| 19 | Alt-tab during a bank hold | ⛔ Game pauses; hold timer does **not** keep running |

### Steam
| # | Case | Expected |
|---|---|---|
| 20 | Play a full run with Steam killed | ⛔ Fully playable, no popups, no disabled menus |
| 21 | Steam killed *mid-run* | Graceful; no crash on next Steam call |
| 22 | Cloud conflict: play on PC, then Mac | ⛔ Prompt appears; neither side silently overwritten |
| 23 | Unlock an achievement offline, then go online | Syncs on next boot |
| 24 | Overlay Shift+Tab during a bank | Game pauses; input focus returns correctly |

### Save & data
| # | Case | Expected |
|---|---|---|
| 25 | Truncate `save.json` to 10 bytes | Defaults load; original renamed to `save.corrupt-*.json` |
| 26 | Set `version` to 999 | Defaults in memory; file **not** overwritten; notice shown |
| 27 | Hand-edit `lifetimeBankedScore` | Progress preserved; leaderboard submission silently suppressed |
| 28 | Fill the disk, then save | ⛔ No corruption (atomic write path) |

### UI & localisation
| # | Case | Expected |
|---|---|---|
| 29 | Switch to **German**, check every button | ⛔ No overflow (DE runs ~35% longer than EN) |
| 30 | Switch to **Russian** | No overflow, Cyrillic renders |
| 31 | Switch to **Japanese/Chinese/Korean** on a machine with no CJK system font | ⛔ No tofu boxes (fallback atlas must be embedded) |
| 32 | 21:9 ultrawide and 16:10 | HUD anchors hold, no stretched arena |
| 33 | Windows display scaling 125% and 150% | ⛔ UI readable, no clipping |
| 34 | MULT ×999 and score 9,999,999,999 | ⛔ No layout overflow, no `int` overflow |

### Performance & stability
| # | Case | Expected |
|---|---|---|
| 35 | 45-minute continuous session | Flat memory, no framerate decay |
| 36 | Peak enemy count (wave 6, RISK V) | ≥60 fps on M4 and UHD 620 |
| 37 | Profiler, 60s capture at peak | ⛔ Zero GC allocation per frame |
| 38 | Leave a run idle for 10 minutes | No runaway spawns, no pool exhaustion, no overflow |
| 39 | 10 consecutive full runs, each OS | Zero exceptions in the player log |
| 40 | Exhaust the enemy pool deliberately (mass splitter deaths) | Splits skipped silently; no null-ref |

### macOS
| # | Case | Expected |
|---|---|---|
| 41 | Fresh Steam download, Gatekeeper at default, **network off** | ⛔ Launches (proves stapling, not just notarization) |
| 42 | `spctl -a -vvv -t install OVERDRAFT.app` | Prints *accepted, source=Notarized Developer ID* |
| 43 | Executable bit after the depot round-trip | `Contents/MacOS/OVERDRAFT` still `+x` |
| 44 | 20-minute session on fanless M4 | No thermal throttling below 60 fps |
| 45 | Retina at 1440p+ | Render scale capped at 1.0 |

---

## 4. Bug severity

| Sev | Definition | Rule |
|---|---|---|
| **P0** | Crash, save corruption, cross-platform determinism break, unlaunchable build | Fix immediately; blocks the milestone gate |
| **P1** | Core loop broken, Steam feature non-functional, soft-lock | Fix before the next milestone |
| **P2** | Visual bug, audio bug, localisation overflow | Batch; fix on Day 18 |
| **P3** | Polish, nice-to-have | → `POST_LAUNCH.md`. Do not fix before 2026-08-27. |

---

## 5. Milestone gate checklists

**M1 (Day 3)** — the Day-3 gate in `PRD.md` §6 (4/4), plus manual cases 1–8.
**M2 (Day 7)** — full run completable; cases 9–13; `GenerationTests` all green.
**M3 (Day 10)** — one store-quality screenshot; both environments distinct; case 37.
**M4 (Day 14)** — cases 20–28 on a real Steam client; `SteamOfflineTests` + `SaveTests` green.
**M5 (Day 21)** — the entire matrix, both platforms, plus `verify.sh --release`.
