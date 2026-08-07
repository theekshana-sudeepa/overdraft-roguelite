# M5 — Localisation, builds, ship (Days 15–21)

**Gate:** `1.0.0-rc1` on a Steam beta branch, both platforms, macOS notarized and stapled.
**Day 21 is feature freeze.** After 2026-08-27 the only permitted changes are bug fixes.

---

## Day 15 — Daily mode polish (~5h)

### M5-T01 · Daily Challenge UX
Countdown to the next UTC seed, today's seed number, one-attempt indicator, post-run result card with rank + score suitable for screenshotting.

⚠️ QA case 11: if the UTC day rolls over **mid-run**, the run must complete and submit to the **original** seed's board.

**AC:** cases 11–12 pass.

### M5-T02 · Share card
A generated results image (score, seed, rank, run time, the OVERDRAFT logo) saved to disk and copied to clipboard on one keypress.

This is free marketing — every shared card is an impression. Cheap to build, high leverage.

**AC:** the card is legible at 1080×1080 and at thumbnail size.

### M5-T03 · Tutorial — four prompts, no system
Four full-screen text prompts triggered by game state on the very first run only: *move & aim* → *dash* → **"CHARGE IS WORTH NOTHING UNTIL YOU BANK IT"** → *bank now*.

⚠️ Say the CHARGE rule **once**, prominently, and never again. If a player misses it, the first failed bank teaches it better than any tooltip.
⚠️ No tutorial *system*. Four prompts, one bool in `SaveData`.

**AC:** a new player understands the bank mechanic within 60 seconds without being told verbally.

---

## Day 16 — Localisation (~5h)

### M5-T04 · String extraction
Editor script scanning all `TMP_Text` in prefabs and scenes → `strings.csv` with stable keys per `docs/02_DATA_SCHEMAS.md` §6.

**AC:** zero hard-coded user-facing strings remain; target ≤ 200 keys.

### M5-T05 · `Localizer` + font fallbacks
Runtime CSV load by locale; TMP fallback assets for CJK (**dynamic SDF**) and Cyrillic. Auto-detect locale from Steam, then OS, on first launch.

⚠️ Static CJK atlases blow the 120 MB build-size budget (NFR-05). Dynamic SDF only.

**AC:** QA case 31 passes on a machine with **no CJK system fonts installed**.

### M5-T06 · Translation
DeepL for all 8 languages, then a native-speaker pass on Fiverr for **zh-Hans, ja, ko** (~$60 total). Simplified Chinese alone is routinely 20–30% of an arcade indie's units — this is the highest-ROI spend in the project.

**AC:** all 8 locales load; a native speaker has reviewed the three CJK locales.

### M5-T07 · Overflow validator
Editor script that fails the build if any string exceeds its key's declared max length.

⚠️ German and Russian run ~35% longer than English (QA cases 29–30).

**AC:** validator passes for all 8 locales.

---

## Day 17 — Build pipeline & notarization (~5h)

### M5-T08 · `BuildScript` (Editor)
`-executeMethod BuildScript.BuildWindows` and `BuildMac`, driving version, build number, defines, and output paths from CI.

**AC:** both builds produced headlessly from one command.

### M5-T09 · Windows build
IL2CPP, x86_64, DX11 only, **Master** configuration.

⚠️ Do **not** buy a Windows code-signing certificate ($200–400/yr). Steam-delivered binaries are launched by the Steam client, which largely bypasses the SmartScreen prompt. Save the money.

**AC:** runs on a machine without VS redistributables; QA cases 17, 33 pass.

### M5-T10 · ★ macOS sign + notarize + staple
Universal (Apple Silicon + Intel), Metal only.

```bash
codesign --deep --force --verify --verbose \
  --sign "Developer ID Application: <Name> (TEAMID)" \
  --options runtime --timestamp \
  --entitlements entitlements.plist "OVERDRAFT.app"

ditto -c -k --keepParent "OVERDRAFT.app" "OVERDRAFT.zip"
xcrun notarytool submit "OVERDRAFT.zip" \
  --apple-id "<id>" --team-id "<TEAMID>" --password "<app-specific-pw>" --wait
xcrun stapler staple "OVERDRAFT.app"
spctl -a -vvv -t install "OVERDRAFT.app"   # must say: accepted, Notarized Developer ID
```

`entitlements.plist` **must** include:
- `com.apple.security.cs.disable-library-validation` — Steamworks loads `libsteam_api.dylib`
- `com.apple.security.cs.allow-unsigned-executable-memory` — IL2CPP/Mono

⚠️ **Staple before zipping for Steam** so offline launches still validate (QA case 41).
⚠️ Doing this on Day 17 rather than Day 21 is deliberate. Apple Developer enrolment and notarization both have unpredictable multi-hour tails, and discovering that on the last day is how Mac builds get cut.

**AC:** QA cases 41–43 pass — fresh Steam download, Gatekeeper default, **network off**, launches.

### M5-T11 · Depot upload
Depots per platform. Exclusions: `*.pdb`, `.DS_Store`, `__MACOSX`, `*_BurstDebugInformation_*`, `steam_appid.txt`.

⚠️ Verify the macOS executable bit survives the round-trip — a downloaded `.app` that lost `+x` on `Contents/MacOS/OVERDRAFT` is *the* classic Mac-on-Steam bug.

**AC:** both builds install and launch from a clean Steam download on a machine that has never seen the project.

---

## Day 18 — Bug day & accessibility (~5h)

### M5-T12 · Profile & fix the top 10
Memory, GC spikes, draw calls, SetPass on both platforms.

**AC:** every NFR-01 to NFR-06 target met and measured.

### M5-T13 · Accessibility & assist
Colourblind palettes (modes 1 and 2), screen-shake slider verified end to end, and an **assist option: bank duration ×1.5**.

⚠️ The assist option widens your audience meaningfully and costs one multiplier. Ship it.

**AC:** the game is completable with all assists on and all shake off.

### M5-T14 · P2 bug batch
Fix everything logged as P2 during M1–M4. P3 items go to `POST_LAUNCH.md` untouched.

**AC:** zero open P0/P1; the full QA matrix in `docs/05_QA_AND_VERIFY.md` §3 passes.

---

## Day 19 — Trailer & store assets (~5h)

### M5-T15 · Cut the trailer
The 30-second beat sheet from the design bible. DaVinci Resolve (free).

Rules: **gameplay by 0:03**, no studio logo at the front, **cut on every beat**, must read completely **with sound off** (Steam autoplays muted), **under 40 seconds** or it won't be watched to completion. Upload at 1920×1080, H.264, ≥8 Mbps, with a poster frame.

**AC:** trailer live on the store page.

### M5-T16 · Capsules & screenshots
All capsule sizes + 5 screenshots, each showing a **different thing**: the bank moment, a ×20 multiplier, the boss, Vault lasers, the results/leaderboard.

⚠️ Design the small capsule at **231×87 first**, then scale up. A capsule that only works at full size fails in the library grid where most impressions happen.

**AC:** the small capsule is legible at actual size in a real Steam library grid.

### M5-T17 · Store page copy
Short description, long description, 15 tags, 8 supported languages, honest system requirements for both OSes, price + 15% launch discount. All copy is in the design bible.

⚠️ Only claim languages you actually shipped.

**AC:** store page complete and proofread.

---

## Day 20 — External playtest (~5h)

### M5-T18 · 5 external testers
2 on Mac, 2 on controller, 1 on a low-end PC. **Watch without speaking.** Note every moment of confusion; do not explain anything.

**AC:** at least one tester finishes a run without asking a question (FR/G-level validation of M5-T03).

### M5-T19 · Fix what they hit
Only confusion and bugs. **New feature requests go to `POST_LAUNCH.md`** — you are 1 day from freeze.

**AC:** every confusion point either fixed or consciously accepted with a written reason.

### M5-T20 · Cut 5 vertical clips
The 5 TikTok/Shorts hooks from the design bible. 9:16, gameplay from frame 1, text within 0.5s, under 20s, loop-friendly, watchable muted.

⚠️ You need **17 clips** for the Sep 1–17 daily posting run. Cut 5 now, batch the rest from your archived footage after freeze.

**AC:** 5 clips exported and captioned.

---

## Day 21 — Freeze (~5h)

### M5-T21 · Demo build
`OVERDRAFT_DEMO` define, separate AppID, contents per `docs/03_STEAM.md` §7. Ends on a score screen with a **"Wishlist to see THE VAULT"** button and an 8-second AUDITOR teaser.

Demo → wishlist conversion for a good arcade game runs 15–30%. It is the single most effective wishlist tool available. Publish it **2026-09-01**.

**AC:** demo build uploaded and linked to the main store page.

### M5-T22 · Release candidate
`./tools/verify.sh --release`, version bump to `1.0.0-rc1`, changelog, tag the commit, upload both platforms to a Steam **beta branch**.

**AC:** `verify.sh --release` ✅ PASS; both builds installable from the beta branch.

### M5-T23 · ★ FEATURE FREEZE
From here to 2026-09-17: **bug fixes only.** Every idea goes to `POST_LAUNCH.md`.

---

## After freeze — the part that earns the money (Aug 28 → Sep 17)

Dev is done. This window is 100% marketing plus fixes from real players.

| Window | Action |
|---|---|
| Aug 28–30 | Keys to 20 creators (500–20k subs) who cover Brotato/Nova Drift-likes. Personalised, short, key attached, no press release. |
| **Aug 31** | Store page goes live. Post everywhere the same day. |
| **Sep 1** | Publish the demo. |
| Sep 1–14 | **One vertical clip per day. Non-negotiable.** This is where wishlists come from. |
| **Sep 8** | Upload RC to the default branch, **submit build for review** (1–5 business days). |
| Sep 10–16 | Fix demo feedback. **Do not add features.** |
| **Sep 17** | Launch ~10am Pacific. Reply to every review and forum post for 72 hours. Ship a patch within 48h even if trivial. |

---

## Definition of Done for M5

- [ ] `./tools/verify.sh --release` ✅ PASS
- [ ] Full QA matrix passes on **both** platforms
- [ ] macOS notarized + stapled, verified offline with Gatekeeper at default
- [ ] Windows verified on a clean machine
- [ ] 8 languages with no overflow and no tofu
- [ ] Trailer live; all capsules and 5 screenshots uploaded
- [ ] Demo build ready
- [ ] `1.0.0-rc1` tagged and on a beta branch
- [ ] Zero open P0/P1 bugs
- [ ] Tagged `m5-ship`
