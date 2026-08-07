# SESSION PROMPTS — copy-paste these into Claude Code

## How to use this package

1. Copy the whole `OVERDRAFT_Handover/` folder into your Unity project root (or create the project inside it).
2. `CLAUDE.md` must sit at the repo root — Claude Code auto-loads it every session.
3. Start each session with the prompt for the milestone you're on.
4. One milestone per session where possible. If a session runs long, stop at a task boundary, commit, and resume with the "resume" prompt.

**File layout in your repo:**
```
<repo root>/
  CLAUDE.md          ← auto-loaded, keep at root
  PRD.md
  docs/00_TUNING.md  01_ARCHITECTURE.md  02_DATA_SCHEMAS.md
       03_STEAM.md   04_ART_AUDIO_SPEC.md  05_QA_AND_VERIFY.md
  tasks/M0_M1_...  M2_...  M3_...  M4_...  M5_...
  tools/verify.sh  verify.bat
  TUNING_LOG.md      ← you create; Day-3 answers + every tuning change
  POST_LAUNCH.md     ← you create; every good idea that isn't in scope
  Assets/ ...
```

---

## Session 1 — M0 + M1 (Days 1–3)

```
Read CLAUDE.md, PRD.md, docs/00_TUNING.md, docs/01_ARCHITECTURE.md, and
tasks/M0_M1_SETUP_AND_CORE_LOOP.md in full before writing any code.

Execute M0-T01 through M0-T08, then M1-T01 through M1-T14, in the listed order.
The order encodes dependencies — do not reorder.

Hard rules for this session:
- No art, no audio, no menus, no Steam, no saving. Untextured primitives
  and raw TextMeshPro only.
- Every tunable value comes from TuningConfig. If a value you need isn't in
  docs/00_TUNING.md, add it there first, then reference it.
- Generation/ must not reference UnityEngine at all.
- Zero runtime Instantiate. Zero per-frame GC allocation.

Write EditMode tests alongside the Economy and Pool work — not afterwards.
Run ./tools/verify.sh before reporting any task complete.

Report at the end: task IDs completed, files changed, any decision you made
that wasn't in the spec, anything at risk, and the single next task.

Do not run M1-T15 (the Day-3 gate) — that one is mine to run.
```

---

## Session 2 — M2 (Days 4–7)

```
Read CLAUDE.md, docs/00_TUNING.md, docs/01_ARCHITECTURE.md,
docs/02_DATA_SCHEMAS.md, and tasks/M2_CONTENT.md.

The M1 gate passed. Execute M2-T01 through M2-T18 in order.

Priorities this session, highest first:
1. M2-T04/T05/T06 — the pure deterministic generator and the golden hash test.
   If cross-platform determinism breaks, the daily leaderboard is invalid,
   which is a P0. Get this exactly right.
2. M2-T13 — the boss AUDIT mechanic. It drains unbanked CHARGE only, never
   banked SCORE. This is the boss's entire design idea.
3. Everything else.

Still no art pass, no audio, no menus. The build should still look like a
programmer-art prototype at the end of this session, and it should already
be fun.

Do not balance away the GREED T3 + LEECH T3 combo (FR-474). It is deliberate.

Run ./tools/verify.sh before reporting complete. Report in the CLAUDE.md §9 format.
```

---

## Session 3 — M3 (Days 8–10)

```
Read CLAUDE.md, docs/04_ART_AUDIO_SPEC.md, and tasks/M3_JUICE_AND_UI.md.

Execute M3-T01 through M3-T17.

This is the milestone where solo projects die to scope creep. Guard rails:
- If an effect is not in docs/04_ART_AUDIO_SPEC.md, do not build it.
  Put the idea in POST_LAUNCH.md and move on.
- Exactly three fullscreen shaders. No fourth.
- Materials must stay at 12 or fewer.
- If you pass ~15 hours of work across this milestone, stop and tell me.
  Steam integration is not skippable; a fourth shader is.

Spend extra care on M3-T05 (FS_BankFocus). That single effect is the
highest-marketing-value visual in the project — it's the shot that goes on
the capsule and in every clip.

Film grain stays ON (banding on near-black). Motion blur stays OFF.

Run ./tools/verify.sh. Report in the CLAUDE.md §9 format.
```

---

## Session 4 — M4 (Days 11–14)

```
Read CLAUDE.md, docs/02_DATA_SCHEMAS.md, docs/03_STEAM.md,
docs/04_ART_AUDIO_SPEC.md §7, and tasks/M4_META_STEAM_AUDIO.md.

Execute M4-T01 through M4-T22, skipping M4-T10 and M4-T21 (Steamworks portal
setup and store page submission are mine).

Non-negotiables this session:
- FR-506: the game must be fully playable with Steam killed. No popups, no
  disabled menus, no error dialogs. Every Steam call goes through Steam.Try.
- The ownership check FAILS OPEN. A false positive on a legitimate offline or
  Family-Sharing user costs far more than the piracy it prevents. Do not add
  key checks, server pings, or integrity scans. There is no DRM in this game.
- Save writes are atomic (tmp + File.Replace). A corrupt save never gets
  deleted — it gets renamed and preserved.
- A tampered save keeps all progress and only suppresses leaderboard
  submission. Never accuse the user, never block play.
- One debounced StoreStats() per run end, not per achievement unlock.
- Voice limiting on SFX is mandatory, or wave 6 clips into mud.

Remind me on M4-T16 to capture 40+ minutes of footage today — if I leave
capture to trailer day and the run I need doesn't happen, I lose a day.

Run ./tools/verify.sh. Report in the CLAUDE.md §9 format.
```

---

## Session 5 — M5 (Days 15–21)

```
Read CLAUDE.md, docs/05_QA_AND_VERIFY.md, and tasks/M5_SHIP.md.

Execute M5-T01 through M5-T23, skipping the human-only tasks
(M5-T06 translation purchase, M5-T15/16/17 trailer and store assets,
M5-T18 external playtest).

Priorities:
1. M5-T10 — macOS sign + notarize + staple, on Day 17, not Day 21. Apple's
   enrolment and notarization both have unpredictable multi-hour tails.
   Verify with: fresh Steam download, Gatekeeper default, network OFF.
2. M5-T05 — CJK fallbacks must be dynamic SDF, and must work on a machine
   with no CJK system fonts installed.
3. M5-T14 — clear every P0 and P1. P3 items go to POST_LAUNCH.md untouched.

Day 21 is feature freeze. From that point, bug fixes only.

Run ./tools/verify.sh --release. Report in the CLAUDE.md §9 format.
```

---

## Utility prompts

**Resume mid-milestone**
```
Read CLAUDE.md and tasks/<current milestone file>. I stopped after <task ID>.
Run ./tools/verify.sh to confirm current state, then continue from the next
task in order. Do not re-do completed work.
```

**Tuning change**
```
The Day-3 gate scored <N>/4. Question <X> failed.
Per PRD.md §6, adjust only the tuning levers in the documented order, one at
a time. Update the value in docs/00_TUNING.md, add a row to its changelog with
the reason, and tell me what to feel for on the next playtest.
Do not change any code.
```

**Bug fix**
```
Bug: <what happened> / <expected> / <repro steps> / <platform>.
Classify it P0-P3 per docs/05_QA_AND_VERIFY.md §4. If P0 or P1, fix it now and
add a regression test. If P2, add it to a batch list for Day 18. If P3, append
it to POST_LAUNCH.md and do not fix it.
```

**Performance regression**
```
Frame time is over the 8ms budget at <situation>. Profile it, identify the top
3 costs, and fix them within the constraints in CLAUDE.md §4 — no new
dependencies, no architectural changes. Report before/after numbers.
```

**Scope pushback (use this often)**
```
I want to add <feature>. Before writing anything: check it against PRD.md §3
Non-goals and the remaining schedule. If it's out of scope or it puts the
2026-08-27 freeze at risk, say so plainly and append it to POST_LAUNCH.md
instead of building it.
```

---

## Files you create on Day 1

**`TUNING_LOG.md`**
```markdown
# Tuning log

## Day 3 gate — <date>
1. Chose not to bank because I wanted a bigger number?   YES / NO
2. Death felt like my fault?                             YES / NO
3. Pressed Retry without deciding to?                    YES / NO
4. Watched the gold CHARGE number, not my character?     YES / NO
Score: _/4
Notes:
```

**`POST_LAUNCH.md`**
```markdown
# Post-launch ideas — DO NOT BUILD BEFORE 2026-08-27

Every good idea that isn't in the PRD lands here. This file is how the project
ships on time. Writing an idea down is not deferring it — it's the mechanism
that lets you stop thinking about it.

## Ideas
-
```

---

## The three things that decide whether this ships

1. **Pay the Steam Direct fee today.** The 30-day waiting period runs in the background while you build. Every day you delay pushes launch one day, one-for-one. Nothing in this package matters if that clock hasn't started.

2. **Day 3 is the real deadline, not Day 21.** If the bank loop isn't compulsive by 2026-08-09, no amount of shaders, achievements, or TikToks will rescue it. Be genuinely willing to change the core numbers on Day 4. Everything after Week 1 is presentation layered on a loop that either works or doesn't.

3. **`POST_LAUNCH.md` is load-bearing.** You will have twenty good ideas between now and freeze. Every one you build costs you polish, marketing, or the date. Write them down and keep moving.
