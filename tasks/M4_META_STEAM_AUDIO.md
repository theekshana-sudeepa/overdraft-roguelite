# M4 — Save, progression, Steam & audio (Days 11–14)

**Gate:** achievements, leaderboards, and cloud save all verified in a real Steam client; the game is fully playable with Steam killed.

References: `docs/02_DATA_SCHEMAS.md`, `docs/03_STEAM.md`, `docs/04_ART_AUDIO_SPEC.md` §7.

---

## Day 11 — Save & progression (~5h)

### M4-T01 · `SaveData` + `SaveSystem` (FR-521 – FR-524)
Schema exactly as in `docs/02_DATA_SCHEMAS.md` §1. JSON at `Application.persistentDataPath/save.json`.

⚠️ **Atomic write:** `save.tmp` → `File.Replace`. Six lines; prevents a corrupt save on a force-quit and a resulting 1-star review.
⚠️ Write on run end, settings change, focus loss, and quit. **Never per-frame.**

**AC:** `Save_RoundTrips_AllFields` and `Save_IsAtomic_UnderSimulatedFailure` green.

### M4-T02 · Corruption recovery (FR-524)
Handle every case in the failure table: missing, empty, truncated, future-version, checksum mismatch.

⚠️ **Never delete the user's file.** Rename to `save.corrupt-<unixtime>.json`. A future-version save must not be overwritten — a player who rolled back a build must not lose everything.

**AC:** `Load_RecoversFrom_*` all green; QA cases 25–27 pass.

### M4-T03 · Checksum & tamper handling (FR-525)
Salted SHA-256 per `docs/02_DATA_SCHEMAS.md` §4. A mismatch sets `tampered` **in memory only** and suppresses **leaderboard submission only**.

⚠️ Progress and unlocks are always preserved. Never show an accusatory message, never block play. This protects the leaderboard, not the game.

**AC:** `TamperedSave_KeepsProgress_BlocksLeaderboard` green.

### M4-T04 · `CreditRankSystem` (FR-481, FR-482)
12 linear nodes on the threshold table in `docs/00_TUNING.md`. Lifetime banked score drives rank. Unlocks: 3 loadouts, 5 RISK tiers, Glass modifier, 2 cosmetics, Custom Seed.

**AC:** `CreditRank_AdvancesAtCorrectThresholds` green; unlocks gate the correct menu entries.

### M4-T05 · Credit Ranks screen
A single linear track visualisation in `01_Menu`. Show current rank, progress to next, and what the next node unlocks — *"1,240,000 to RISK IV"* is a retention driver, so make the next unlock always visible.

**AC:** navigable by gamepad and mouse; progress persists across restart.

### M4-T06 · The 3 loadouts + Glass modifier
*Scattergun* (start SPREAD T1), *Hair Trigger* (start LEECH T1), *Bankrupt* (start MULT ×5, cannot bank until wave 3). *Glass* (1 HP, score ×2).

**AC:** each applies at run start and is reflected in `RunProof`.

---

## Day 12 — Steam core (~5h)

### M4-T07 · Steamworks.NET + `SteamManager` (FR-501)
Install the package, `steam_appid.txt` in the project root for editor testing, `Overdraft.Steam` asmdef. `RestartAppIfNecessary` guarded by `#if !UNITY_EDITOR`.

⚠️ Be accurate about what this is: a launcher convenience, **not** DRM. Do not add key checks, server pings, or integrity scans. The soft ownership check **fails open** (`fallback: true`) on purpose — a false positive on a legitimate offline or Family-Sharing user costs far more than the piracy it would prevent.

**AC:** the game launches identically with Steam running and with Steam absent.

### M4-T08 · The `Steam.Try` guard (FR-506)
Implement the wrapper in `docs/03_STEAM.md` §1. **Every** Steam call goes through it.

**AC:** `AllSteamCalls_NoOpWhenUninitialized` green; QA cases 20–21 pass (full run with Steam killed, and Steam killed mid-run).

### M4-T09 · 10 achievements (FR-502)
Exactly the list in `docs/03_STEAM.md` §3. Local mirror in `SaveData` first, then `SetAchievement`.

⚠️ **One debounced `StoreStats()` per run end.** Calling it per unlock gets you rate-limited by Steam.
⚠️ On boot, reconcile local ↔ Steam as a **union** so offline unlocks sync later.

**AC:** `Achievements_BatchedToOneStoreCall` green; all 10 fire in a real client; QA case 23 passes.

### M4-T10 · Steamworks portal setup (human)
Create the app, upload achievement icons (locked + unlocked, 64×64), configure Auto-Cloud for both platforms, upload the rich-presence token file.

**AC:** the checklist in `docs/03_STEAM.md` §8 is complete.

### M4-T11 · Cloud save (FR-504)
Auto-Cloud config + the conflict prompt from `docs/02_DATA_SCHEMAS.md` §7.

⚠️ **Never resolve a conflict silently in either direction.** Modal with "Use cloud / Use local", default focus on cloud.
⚠️ Exclude `save.corrupt*.json` and `*.tmp` from the sync pattern — never upload a broken file.

**AC:** QA case 22 passes (play PC → play Mac → prompt appears, neither side lost).

---

## Day 13 — Leaderboards, presence, capture (~5h)

### M4-T12 · Leaderboards (FR-503, FR-507)
Four boards per `docs/03_STEAM.md` §4. `RunProof` details blob with the **fixed index layout** in `docs/02_DATA_SCHEMAS.md` §5 — never reorder it, or historical entries become unreadable.

Daily: first attempt only, `KeepBest` off, enforced client-side by `lastDailySeedPlayed`.

**AC:** submissions land and entries display on all four boards.

### M4-T13 · Results screen leaderboard — friends first
Friends board first with the player highlighted; global shown as a **percentile** (*"TOP 12%"*), never a raw rank outside the top 1,000.

⚠️ This is a psychology decision, not a layout one. *"You beat Alex by 400"* drives another run; *"you are 8,412th of 40,000"* drives quitting.

**AC:** friends board renders first; global shows a percentile.

### M4-T14 · Plausibility filter (FR-507)
Client-side display filter per `docs/02_DATA_SCHEMAS.md` §5.

⚠️ Presentational only. Never report a user, never display "cheater", never block a client.

**AC:** a synthetic impossible entry is hidden from the displayed board.

### M4-T15 · Rich presence (FR-505)
Tokens per `docs/03_STEAM.md` §6. Update on **wave change and bank completion only** — Steam rate-limits to roughly 20 calls/minute.

**AC:** a friend's client shows *"DAILY #47 · Wave 6 · 41.2k unbanked"*.

### M4-T16 · ★ Capture footage
All visuals and the boss now exist. Record **40+ minutes** at 1440p60 in OBS, high bitrate, no compression artefacts.

Deliberately capture: a failed bank at 95%, a ×25+ multiplier, a full VOLATILE chain, the boss audit draining the bar, a perfect-dash slow-mo, and a clean boss kill into the free bank.

⚠️ Do this **today**, not on Day 19. If you leave capture to trailer day and the run you need doesn't happen, you lose the day.

**AC:** 40+ minutes of clean 1440p60 footage archived, with each listed moment present.

---

## Day 14 — Audio + buffer + store page submission (~5h)

### M4-T17 · `AudioDirector`, mixer & voice limiting
`AudioMixer` with the bus hierarchy and levels from `docs/04_ART_AUDIO_SPEC.md` §7. Snapshots for: normal, banking, hit, low-HP, boss-audit.

⚠️ **Voice limiting is mandatory**: max 4 instances of any SFX id; a 5th within 60ms is skipped and instead bumps the existing instance `+1.5dB` (cap `+6dB`); pitch `±8%` and volume `±2dB` jitter on every repeat. Without this, 200 simultaneous deaths produce clipping mud.

**AC:** wave 6 at RISK V does not clip; the mix stays legible.

### M4-T18 · Core SFX
Shoot, enemy hit, enemy death, dash, perfect dash, player hit, **bank start / bank tick / bank complete / bank fail**, wave clear, boss phase, boss audit, UI select/confirm.

Use the **3-layer technique** (click + body + tail) on the bank-complete sound specifically — it should be the most satisfying sound in the game.

**AC:** the bank-complete stinger is genuinely satisfying on the 50th hearing.

### M4-T19 · Dynamic audio rules
Every row of the trigger table in `docs/04_ART_AUDIO_SPEC.md` §7.

⚠️ The **200ms of silence before the bank stinger** is what makes it land. Do not remove it because it "feels like a bug."
⚠️ The MULT decay warning is deliberately irritating. Players will describe it as stressful. That is the design.

**AC:** playing with headphones, the bank hold is audibly tense without any visual.

### M4-T20 · Music integration
Three tracks with layer stems. Arp layers fade in at MULT ×10 and ×20; boss track builds by phase.

**AC:** licence PDF stored in the repo (Valve occasionally asks).

### M4-T21 · ★ Submit the Steam store page (human, hard deadline)
Using Day-13 footage. Store page review takes **7 business days**; submitting today puts the page live around **2026-08-31**, which is the earliest date that satisfies the mandatory 2-week Coming Soon window before the 2026-09-17 launch.

⚠️ **This is the highest-consequence deadline in the project.** Slipping it slips launch day-for-day. Capsules, description, and tags are all in the design bible — use them.

**AC:** store page submitted for review on 2026-08-20.

### M4-T22 · Buffer
Fix what M4 broke. Run QA cases 20–28.

---

## Definition of Done for M4

- [ ] `./tools/verify.sh` ✅ PASS
- [ ] `SaveTests`, `SteamOfflineTests` fully green
- [ ] All 10 achievements fire in a real Steam client
- [ ] All 4 leaderboards accept and display entries
- [ ] Cloud save verified PC → Mac with a conflict prompt
- [ ] **A full run completes with Steam killed, no popups, no disabled menus**
- [ ] Rich presence visible on a friend's client
- [ ] 40+ minutes of 1440p60 footage archived
- [ ] Store page submitted for review
- [ ] QA cases 20–28 pass
- [ ] Tagged `m4-meta-steam`

## Explicitly NOT in M4

Localisation (M5) · build pipeline (M5) · notarization (M5) · trailer edit (M5) · demo build (M5).
