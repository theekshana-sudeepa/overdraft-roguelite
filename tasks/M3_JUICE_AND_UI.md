# M3 — Juice, look & UI (Days 8–10)

**Gate:** a single screenshot is store-page quality, and both environments read as visually distinct.
**This milestone is where the game stops looking like a prototype.** It is also where scope creep kills projects — build exactly what is listed.

Reference throughout: `docs/04_ART_AUDIO_SPEC.md`.

---

## Day 8 — Post-processing & shaders (~5h)

### M3-T01 · Volume profile
One global `Volume` in `02_Arena` with every effect and value from `docs/04_ART_AUDIO_SPEC.md` §3.

⚠️ **Film grain is required, not optional** — the near-black background bands severely without it, especially on OLED.
⚠️ **Motion blur stays OFF.** It destroys readability in a twin-stick and generates refund-tier complaints.

**AC:** a static screenshot at wave 5 looks like a commercial game.

### M3-T02 · Reactive post-processing
Chromatic aberration and vignette driven by game state per the table: CA `0.08` base → `0.40` on hit (0.25s decay) → `0.25` during bank; vignette `0.32` → `0.55` during bank.

**AC:** taking a hit is felt through the screen, not just the HUD.

### M3-T03 · `FS_Shockwave`
Radial UV displacement ring on bank complete and boss phase change. Radius `0 → 1.4` over `0.35s`, width `0.08`, chromatic offset on the ring. URP `ScriptableRendererFeature` + Blit.

**AC:** bank completion is visible from across the room.

### M3-T04 · `FS_Glitch`
0.12s horizontal RGB slice on player hit: 6 bands, offsets to `0.03` UV.

**AC:** getting hit reads instantly at a glance.

### M3-T05 · ★ `FS_BankFocus` — the money shot
During a bank hold: desaturate everything to `bankDesaturation` (0.85) **except** the player and enemies within 4.0 units, via a radial mask; darken the edges.

⚠️ **This is the shot that sells the game on TikTok and on your capsule. Spend an extra hour here.** It is the single highest-marketing-value visual in the project.

**AC:** a 6-second recording of a bank hold is compelling with the sound off.

### M3-T06 · Two quality presets
`PC_High` and `PC_Low` URP assets per §3. Auto-select `PC_Low` on integrated GPUs at first launch; expose a toggle in options.

⚠️ Cap render scale at 1.0 at ≥1440p or an M4 renders bloom at 4× fill rate for zero visible benefit.

**AC:** `PC_Low` holds 60fps on Intel UHD 620 at 1080p (NFR-01).

---

## Day 9 — Environments & camera feel (~5h)

### M3-T07 · `GridShader` + arena materials
Procedural grid lines, no textures. `M_Grid_Floor` (cool) and `M_Grid_Vault` (warm) as parameter variants of one shader.

**AC:** total project materials ≤ 12 (NFR-04).

### M3-T08 · Two colour-grading LUTs
`LUT_Floor` (cool, lifted blacks) and `LUT_Vault` (warm, crushed blacks), crossfaded over `environmentLerpTime` on the wave-4 transition.

**AC:** side-by-side screenshots of Floor and Vault are unmistakably different places.

### M3-T09 · Hazard laser visuals
`M_Hazard_Laser` with scroll, plus the `hazardTelegraphTime` (0.8s) warning state. The telegraph must be unmissable — a lethal bar with a weak warning is the fastest route to "unfair" reviews.

**AC:** a first-time player dodges the bars on their second encounter.

### M3-T10 · Camera punch & polish
Zoom punch (`-4%` over `80ms`, ease-out) on bank complete; verify shake, trauma decay, and the `Settings.screenShake` user slider all wire through.

⚠️ Ship the shake slider. Motion-sensitive players refund otherwise, and refunds hurt more than a slider costs.

**AC:** shake at slider 0 is completely still; at 1.0 it's violent but never nauseating.

### M3-T11 · VFX pass
All pooled `ParticleSystem`s with `M_Particle_Additive`: kill burst (6 particles), dash trail, projectile impact, bank deposit burst, boss phase shatter, elite outline shimmer.

**AC:** < 1,500 particles alive at peak; zero runtime `Instantiate`.

---

## Day 10 — Scene flow & real HUD (~5h)

### M3-T12 · Menu → Game → Results flow
`01_Menu`: main menu, mode select (Endless / Daily / Custom Seed), options, quit. `02_Arena` loads additively and unloads on run end.

**AC:** a full session runs without the editor; loads are under one frame of hitch.

### M3-T13 · Options menu — exactly six settings (FR-544)
Master / Music / SFX volume, screen shake, colourblind palette, quality preset, fullscreen mode, language. **Nothing else.** Fullscreen defaults to **borderless windowed** (alt-tab reliability beats 1ms of exclusive-mode gain).

**AC:** every setting persists across restart; no setting exists that isn't on this list.

### M3-T14 · Pause
Pauses on Esc/Start **and on focus loss**. Resume, options, quit-to-menu.

⚠️ QA case 19: alt-tab during a bank hold must pause and the hold timer must **not** keep running.

**AC:** case 19 passes.

### M3-T15 · Real HUD (FR-542)
Full pass per `docs/04_ART_AUDIO_SPEC.md` §6.
- CHARGE: gold, 96pt Chakra Petch SemiBold, pulsing at `1 + MULT/20` Hz, ±4% scale
- Bank ring **around the player**, never in a corner
- MULT + decay bar, turning `Warn` magenta when `IsWarning`
- SCORE (white), wave counter, HP pips, dash charges
- `FastNumberText` — zero allocation
- No panels, no borders, no drop shadows. Everything animates in ≤120ms.

**AC:** `Profiler` shows zero allocation from the HUD over a 60s capture; the gold number is the loudest thing on screen.

### M3-T16 · Results screen (FR-545)
Banked score, run time, kills, largest single bank, highest multiplier. **One-press retry, under 2.0 seconds back into play** (G1). Leaderboard panel is a placeholder until M4.

**AC:** measured retry time < 2.0s.

### M3-T17 · Fonts
Chakra Petch (HUD/headers) + Inter (body) as TMP assets. Set up CJK fallbacks now even though localisation lands in M5 — retrofitting fallbacks after every label exists is painful.

**AC:** no tofu when a Japanese string is pasted into any label.

---

## Definition of Done for M3

- [ ] `./tools/verify.sh` ✅ PASS
- [ ] One screenshot you would put on a Steam store page, unedited
- [ ] Floor and Vault unmistakably distinct
- [ ] ≤12 materials, <150 draw calls, <20 SetPass (NFR-04)
- [ ] 60fps on `PC_Low` at 1080p on integrated graphics
- [ ] Zero GC allocation per frame including HUD (QA case 37)
- [ ] QA cases 14–19, 32–34 pass
- [ ] Tagged `m3-juice`

## ⚠️ Scope warning for this milestone

M3 is where solo projects die. The work is visual and open-ended, and there is always one more effect. Guard rails:

- If it isn't in `docs/04_ART_AUDIO_SPEC.md`, **it doesn't get built**. Put the idea in `POST_LAUNCH.md`.
- No new enemy visuals, no new VFX categories, no additional shaders beyond the three specified.
- If you're past 15 hours across Days 8–10, **stop and move to M4.** Steam integration is not skippable; a fourth fullscreen shader is.

## Explicitly NOT in M3

Audio (M4) · Steam (M4) · saving (M4) · meta progression (M4) · localisation (M5) · trailer (M5).
