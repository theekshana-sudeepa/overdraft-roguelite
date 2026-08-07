# 04 — ART & AUDIO (implementable spec)

This is a build spec, not a mood board. Every value here goes into a material, a volume profile, or an `AudioMixer` snapshot.

**Thesis: nothing is drawn, everything is lit.** All perceived production value comes from bloom, timing, and colour discipline — which are code, not art.

---

## 1. Colour constants

```csharp
// Scripts/Core/Palette.cs — the ONLY place colours are defined
public static class Palette
{
    public static readonly Color Void      = Hex("07070C");  // background
    public static readonly Color Grid      = Hex("141C2E");  // floor lines, 8% alpha
    public static readonly Color Ion       = Hex("3BF4FF");  // PLAYER — everything "yours"
    public static readonly Color Bank      = Hex("FFC53B");  // GOLD — CHARGE only. Appears nowhere else.
    public static readonly Color Threat    = Hex("FF3B5C");  // all enemies and enemy fire
    public static readonly Color Splitter  = Hex("FF7A3B");  // splitter accent
    public static readonly Color Hazard    = Hex("B03BFF");  // vault lasers, boss audit beam
    public static readonly Color Banked    = Hex("FFFFFF");  // banked SCORE — safe
    public static readonly Color Warn      = Hex("FF3BE1");  // mult about to decay, low HP
}
```

**The rule that carries the whole design: cyan = you, red = death, gold = money, white = safe.** Four colours, learnable in three seconds, language-independent. **Gold appears on exactly one thing.** If gold ever appears on a second element, the CHARGE number stops being the loudest thing on screen and the compulsion loop weakens. Enforce this.

### Colourblind modes
| Mode | Change |
|---|---|
| 0 — None | as above |
| 1 — Deuter/Protan | `Threat → #FF9E00`, `Ion → #00E5FF`, enemy outline width `0 → 0.05` |
| 2 — Tritan | `Bank → #FF6BD6`, `Hazard → #00C2A8` |

Modes drive a global `MaterialPropertyBlock` swap plus a shader keyword. No per-mode assets.

---

## 2. Materials — 12 maximum (NFR-04)

| # | Material | Shader | Notes |
|---|---|---|---|
| 1 | `M_Unlit_Emissive` | Unlit, HDR `_EmissionColor` | **Every actor.** Per-instance colour via `MaterialPropertyBlock`. |
| 2 | `M_Grid_Floor` | Custom `GridShader` | Procedural lines, no texture |
| 3 | `M_Grid_Vault` | Custom `GridShader` | Different params only |
| 4 | `M_Wall_Emissive` | Unlit additive | Arena boundary |
| 5 | `M_Projectile_Player` | Unlit additive | Instanced quads |
| 6 | `M_Projectile_Enemy` | Unlit additive | |
| 7 | `M_Hazard_Laser` | Unlit additive + scroll | Vault bars, boss spokes |
| 8 | `M_Puddle` | Unlit additive + noise | Spitter death puddle |
| 9 | `M_Particle_Additive` | URP Particles Unlit | All VFX |
| 10 | `M_BankRing` | Unlit, radial-fill | Drawn around the player |
| 11 | `M_Outline` | Unlit, inverted-hull | Elites + colourblind mode |
| 12 | `M_UI_Default` | TMP / UI Default | |

**No realtime lights. No shadows. No lightmaps. No reflection probes.** Turn them off in the URP asset and delete the default Directional Light.

### Meshes
All primitives, all authored in ProBuilder or generated at build:

| Actor | Mesh | Tris |
|---|---|---|
| Player | Triangle (extruded, flat) | 8 |
| Drifter | Triangle | 4 |
| Spitter | Hollow square (4 quads) | 16 |
| Splitter | Diamond | 8 |
| Projectile | Quad, billboarded | 2 |
| THE AUDITOR | Hexagonal prism, hollow | ~200 |
| Fragment | Hexagon shard | 12 |

Silhouette does 100% of the identification work. Nothing has a texture.

---

## 3. Post-processing — the actual art budget

One global `Volume` in `02_Arena`, plus a second weighted volume per environment for the colour-grade crossfade.

| Effect | Setting | Reactive? |
|---|---|---|
| **Bloom** | Threshold `0.90`, Intensity `1.40`, Scatter `0.72`, High Quality Filtering **on** | no |
| **Chromatic Aberration** | base `0.08` | ▲ `0.40` on hit (decay 0.25s), `0.25` during bank |
| **Lens Distortion** | Intensity `-0.08`, Scale `1.0` | no |
| **Vignette** | Intensity `0.32`, Smoothness `0.50` | ▲ `0.55` during bank |
| **Film Grain** | Type `Thin1`, Intensity `0.15` | no — but **required**: kills banding on the near-black background, which is severe on OLED |
| **Colour Grading** | Two LUTs: `LUT_Floor` (cool, lifted blacks), `LUT_Vault` (warm, crushed blacks) | crossfade over `environmentLerpTime` |
| **Motion Blur** | **OFF** | Never. It destroys readability in a twin-stick and generates complaints. |
| **Depth of Field / SSAO / SSR** | **OFF** | Nothing to focus on; pure cost |

### Quality presets (NFR-01)
| Preset | Bloom | CA | Grain | Render scale |
|---|---|---|---|---|
| `PC_High` | full, HQ filtering | on | on | 1.0 (capped at 1.0 even on Retina) |
| `PC_Low` | half-res, no HQ filter | off | on | 1.0 |

Auto-select `PC_Low` on integrated GPUs at first launch (`SystemInfo.graphicsDeviceName` heuristic), expose the toggle in options. **Cap render scale at 1.0 at ≥1440p** or an M4 will render bloom at 4× fill rate for no visible benefit.

---

## 4. Three custom fullscreen shaders

| Shader | Trigger | Behaviour |
|---|---|---|
| `FS_Shockwave` | Bank complete, boss phase change | Radial UV displacement ring: radius `0 → 1.4` over `0.35s`, width `0.08`, amplitude falloff `1-r`, plus a small chromatic offset on the ring |
| `FS_Glitch` | Player hit | `0.12s` of horizontal RGB slice: 6 bands, random offsets up to `0.03` UV, driven by a seeded-per-hit noise |
| `FS_BankFocus` | Bank hold | Desaturate to `bankDesaturation` (0.85) everywhere **except** the player and any enemy within `4.0` units, via a radial mask; darken edges. **This is the shot that sells the game — spend an extra hour on it.** |

Implement as URP `ScriptableRendererFeature` + `Blit`. Each is ~40 lines of ShaderGraph or HLSL.

---

## 5. Feel — the three functions that do 80% of the work

### Hitstop
```csharp
// Scripts/Utility/Hitstop.cs — durations are UNSCALED
public void Freeze(float unscaledDuration)   // sets Time.timeScale = feel.hitstopScale (0.02)
```
Because gameplay is in `FixedUpdate` and VFX/audio are in `Update`, hitstop freezes the world while particles and sound keep playing. That contrast *is* the impact. Requests stack by taking the **max** remaining duration, never by summing.

### Screen shake
```csharp
// shake = trauma², trauma decays linearly. Squaring makes small hits subtle and big hits violent.
offset   = Perlin(t * shakeFrequency) * shakeMaxOffset   * trauma * trauma * userScale;
rotation = Perlin(t * shakeFrequency + 7) * shakeMaxRotation * trauma * trauma * userScale;
```
Applied in `LateUpdate` to a camera **child** transform, never the follow transform. `userScale` comes from `Settings.screenShake` (0–1) — ship the slider; motion-sensitive players will otherwise refund.

### Camera
```
position   = Lerp(current, player + aimDirection * 0.25, cameraFollowLerp * dt)
orthoSize  = envSize * (1 - zoomPunch)     // punch: -4% over 80ms on bank complete, ease-out
```
The `aimDirection * 0.25` lead is small but makes the camera feel responsive rather than dragged.

---

## 6. HUD layout

```
┌──────────────────────────────────────────────────────────────┐
│  ×14.5                    8,420                      124,900 │
│  ▓▓▓▓▓▓▓░░░           U N B A N K E D                WAVE 4/6│
│  (Ion, 32pt)          (Bank gold, 96pt)         (White, 28pt)│
│                                                              │
│                                                              │
│                         ((  ◣  ))          ← bank ring drawn │
│                                              AROUND the      │
│                                              player, not in  │
│                                              a corner        │
│                                                              │
│  ◆◆◇                                                         │
│  ▮▮                                                          │
│  (HP pips, dash charges — bottom left, Ion)                  │
└──────────────────────────────────────────────────────────────┘
```

Non-negotiables:
- **CHARGE is the largest element and the only gold thing.** It pulses at `1 + MULT/20` Hz — scale oscillates ±4%.
- The **bank ring draws around the player**, so during the 1.2s hold the player's eye is exactly where the danger is. A corner progress bar would break the entire tension design.
- MULT decay bar turns `Warn` magenta and pulses when `IsWarning`.
- No panels, no borders, no rounded rects, no drop shadows. Numbers floating in space.
- Everything animates in **≤120ms**. Slow UI makes a fast game feel slow.
- `FastNumberText` (see `docs/01_ARCHITECTURE.md` §11) — zero allocation.

### Fonts
| Use | Font | Licence |
|---|---|---|
| HUD, numbers, headers | **Chakra Petch** SemiBold/Bold | SIL OFL |
| Body, tooltips | **Inter** | SIL OFL |
| CJK fallback | **Noto Sans SC / JP / KR** (dynamic SDF) | SIL OFL |

Chakra Petch is chosen specifically for **tabular figures** — its digits are equal width, so the CHARGE number doesn't jitter horizontally as it ticks. Most fonts do jitter, and on a number that changes 8× per second it looks broken.

---

## 7. Audio

### Sources (all free or ≤$25 total)
| Source | Licence | Use |
|---|---|---|
| ChipTone / jsfxr | public domain | base layer for shoot/hit/pickup |
| Freesound (CC0 filter) | CC0 | impacts, whooshes, sub-drops |
| Kenney Audio | CC0 | UI |
| **Sonniss GDC Bundle** | royalty-free commercial | the professional layer — this is the real secret |
| Audacity | — | layering |

**The technique that makes SFX sound expensive:** every important sound is three layers — a **click** (transient, ~5ms, defines timing), a **body** (the recognisable tone), and a **tail** (reverb/sub, sells weight). The bank-complete sound is: coin click + rising synth swell + sub-bass drop with ~400ms plate reverb. Fifteen minutes in Audacity; it will be the most satisfying sound in the game.

### Music
Dark synthwave / minimal techno, 128–140 BPM, cold arpeggios, sidechained kick, almost no melody.

| Track | BPM | Length | Stems |
|---|---|---|---|
| `floor.ogg` | 128 | 2:30 loop | base + arp-high + arp-octave |
| `vault.ogg` | 140 | 2:30 loop | base + arp-high + arp-octave |
| `auditor.ogg` | 140 | 2:30 loop | drums / bass / lead (separate for phase build) |

> ⚠️ **Do not use AI-generated music.** Valve requires AI-content disclosure on the store page and the legal position for commercial release is unsettled. A licensed royalty-free pack costs ~$25. Keep the licence PDF in the repo — Valve occasionally asks.

### Dynamic rules (AudioMixer snapshots)

| Trigger | Response |
|---|---|
| MULT ≥ ×10 | fade in arp-high layer, +6dB over 1.0s |
| MULT ≥ ×20 | add arp-octave; music master +2dB |
| MULT warning (`GraceLeft ≤ 0.40`) | rising sine 800→1600 Hz, centre, volume ramp. **Players will call this stressful. That is the intent.** |
| **Bank starts** | duck music to `-14dB` over 120ms, lowpass to 800 Hz, heartbeat 72→130 bpm across the hold |
| **Bank completes** | **200ms of near-silence first**, then the 3-layer stinger, full unduck with 60ms attack. The silence is what makes the stinger land. |
| Bank interrupted | hard cut, lowpass slams shut, 400ms near-silence |
| Player hit | duck all `-8dB` for 250ms + high-passed ringing tone |
| HP = 1 | persistent 12 kHz lowpass on music + slow pulse — the player *hears* their danger |
| Boss P2 audit | music drops to drums only; add a descending detuned drone |
| Wave clear | 400ms of near-total silence before the next wave |

### Voice limiting — mandatory
With 200 enemies dying, naive playback produces clipping mud. Rules:
- Max **4** simultaneous instances of any single SFX id
- A 5th request within 60ms is **skipped**, and instead the existing instance's volume is bumped `+1.5dB` (cap `+6dB`)
- Every repeated sound gets pitch `±8%` and volume `±2dB` jitter

This single function is the difference between "chaotic" and "broken."

### Volume hierarchy
Mix at **−14 LUFS integrated**, true peak ≤ **−1.0 dBTP**.

| Bus | Level | Rationale |
|---|---|---|
| Bank stinger | `0 dB` | once per ~30s, it can afford to dominate |
| Player hit / death | `-2 dB` | must never be missed |
| Boss telegraph | `-3 dB` | survival-critical |
| MULT decay warning | `-4 dB` | deliberately irritating |
| Enemy death | `-8 dB` | high-frequency, cuts through anyway |
| **Player weapon** | `-12 dB` | constant — it must sit *under* everything. A loud gun is the #1 arcade audio mistake. |
| Music | `-10 dB` | ducked per table above |
| UI | `-14 dB` | |
| Ambience | `-20 dB` | |

Defaults shipped: music `0.70`, SFX `0.85`, master `1.00`. Most players never open options, so **your defaults are the mix 90% of them will ever hear.**
