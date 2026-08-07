# Post-launch ideas — DO NOT BUILD BEFORE 2026-08-27

Every good idea that isn't in the PRD lands here. This file is how the project ships on time.
Writing an idea down is not deferring it — it's the mechanism that lets you stop thinking about it.

## Ideas

- **Bank-chain bonus** — consecutive banks with no hit taken pay an escalating kicker. Tempting,
  but it competes with MULT for the player's attention and would need its own HUD element.
- **Replay export from a seed** — the generator is already deterministic, so a run is
  reconstructable from `seed + input log`. Cheap to add later; zero launch value.
- **Per-line upgrade cosmetics** — the player's triangle changes silhouette per upgrade line
  taken. Pure NG5 risk before launch, great for a post-launch trailer.
- **Colourblind mode 3 (achromatopsia)** — the current spec covers deuter/protan and tritan.
  A full monochrome-safe mode needs shape tells, not colour tells.

## Deferred from M0/M1 implementation (2026-08-07)

These are real gaps found while building, not new features. Each has a milestone it belongs to.

- **`RunSeed` + `WaveGenerator`** — the `Generation` assembly currently holds only the schema
  types (`EnemyType`, `ClumpPattern`, `SpawnEvent`, `WaveRules`). The seeded generator itself is
  M2-T04 and was deliberately not written early, because M1 explicitly excludes seeds.
- **`Camera.main` in `PlayerInputRouter`** — fine now (one camera, one scene). When the results
  screen adds a second camera in M2-T17, this needs an explicit reference.
- **`docs/02_DATA_SCHEMAS.md` §4 uses `System.Convert.ToHexString`** — that method is .NET 5+ and
  does **not** exist under .NET Standard 2.1, which is the API level this project targets (M0-T01).
  The save checksum in M4 must hex-encode the SHA-256 by hand or use `BitConverter.ToString`.
  Catching this at M4 instead of now would cost an hour on a day that has none.
- **`CLAUDE.md` §5 shows a file-scoped namespace** (`namespace Overdraft.Economy;`). That is a
  C# 10 feature; Unity 2022.3 LTS compiles C# 9, so the whole codebase uses block namespaces.
  Worth correcting in CLAUDE.md so it doesn't get "fixed" back later.
