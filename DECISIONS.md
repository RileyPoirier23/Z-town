# Decisions

A running log of architectural and project decisions. Big or hard-to-reverse ones need
Riley's OK before they're marked **Accepted**.

Status: **Accepted** (Riley agreed) · **Proposed** (waiting for Riley) · **Superseded**

---

### D-001 — The game lives in `RileyPoirier23/Z-town`
**Accepted** (2026-10-08). Riley attached the empty Z-town repo for this project. Nothing for
this game goes in the CageBoss repo.

Note: the repo is **public**, so `FACTS.md` and the design docs (memere, Dad) are publicly
readable. Public is also what lets the installer download updates from GitHub Releases without
a token, the same as CageBoss. ❓ Riley: keep it public, or make it private and host the
downloads on 506clicks.ca instead (see D-005)?

### D-002 — Engine: Godot 4 (.NET / C#)
**Accepted** (from the brief). Latest stable Godot 4.x .NET at setup time, pinned in CI.

### D-003 — Simulation core is a plain C# library with no Godot dependency
**Proposed.** `src/Core` holds the whole simulation (entities, needs, inventory, power, comfort,
protection gate, saves). Godot (`src/Game`) only renders and handles input. Why: core tests run
with plain `dotnet test` in seconds on CI without a Godot install, the soak tests can simulate
thousands of days headless, and the memere-protection guarantee is testable in isolation.

### D-004 — Auto-updates: Velopack, with a small launcher exe
**Proposed.** CageBoss uses `electron-updater`, which only works with Electron apps. Velopack is
the closest equivalent for a .NET/Godot game: installer, background download, "restart now or
on quit", reads updates from GitHub Releases.

Velopack needs its startup hook to run at the very start of the program, and Godot owns the
program's start. So the installed `.exe` is a tiny C# launcher (`src/Launcher`) that runs the
Velopack hook, checks for updates, then starts the Godot game. This is checked end to end in
Phase 1 (ship v0.1.0, then v0.1.1, confirm the installed copy updates).

**Update (Phase 0):** the portable download is `Z-Town-Portable.zip` instead of a single `.exe`.
A Godot .NET game ships its .NET files next to the exe, so it can't be one file. The upside is
that Velopack's portable zip updates itself just like the installed copy, which CageBoss's
portable exe can't do. The launcher is a trimmed single-file exe (~12 MB).

### D-005 — Releases and hosting mirror CageBoss exactly
**Proposed.** Same rules as CageBoss's `.github/workflows/desktop.yml`:
- Windows runner builds on every push to `main` or `claude/**` and uploads the installer and
  portable `.exe` as artifacts.
- A GitHub Release is published when the version is bumped (in `version.txt`) and no release for it
  exists yet, or when a `v*` tag is pushed.
- Release notes = the commit message, with `Co-Authored-By` / `Claude-Session` lines removed
  (the website's "What's new" reads these).
- Stable names copied alongside versioned ones, for website links:
  `Z-Town-Setup.exe`, `Z-Town-Portable.exe` →
  `https://github.com/RileyPoirier23/Z-town/releases/latest/download/Z-Town-Setup.exe`
- 506clicks.ca links to those, same as CageBoss. (The site couldn't be reached from this build
  environment, so how it reads releases is assumed from CageBoss's setup. ❓ Riley to confirm.)
- Extra for this game: if any dialogue line or parody name isn't `approved`, the release is
  **held back** (the build stays green, uploads the dev build as an artifact, and posts a
  warning saying what's waiting). It doesn't turn CI red on every push while drafts exist.

### D-006 — Map data: OpenStreetMap only
**Accepted** (from the brief). Never Google Maps data. Import from a Geofabrik New Brunswick
extract (`.osm.pbf`) processed offline by `tools/osm_import` (Python + pyosmium), so imports are
repeatable and don't hammer the Overpass API.

### D-007 — ODbL compliance
**Proposed.** The game's map is a "Produced Work" from a "Derivative Database" of OSM. ODbL
requires attribution (credits + map screen: "© OpenStreetMap contributors, ODbL"), and if the
game is publicly distributed, the derived map data (or the means to recreate it) must be offered
under ODbL. We satisfy that by keeping the import tool and its config in the repo, which recreates
the derived data from the public OSM extract. Hand-made content (interiors, memere's house, art)
is ours and is not covered by that requirement.

### D-008 — Scale: 1 tile ≈ 1 m, world in 32×32-tile chunks
**Proposed.** Matches Zomboid's feel. Real distances stay real. Revisit after the vertical slice
if driving between towns feels too long.

### D-009 — Data formats
**Proposed.** JSON (with JSON Schemas) for items, recipes, loot, quests, TV, brands, comfort.
YAML for dialogue (as the brief asks; easier for Riley to read and edit by hand). Saves are
versioned, compressed JSON with a migration chain.

### D-010 — Memere protection is structural
**Accepted** (from the brief, rule #1). Memere has no health component; all harm and targeting go
through one gate that refuses protected entities; four test suites fail the build on any breach;
her animation rig has no harm states. See `DESIGN.md` §4.1.

### D-011 — Working branch
**Proposed.** Work happens on `claude/nice-pasteur-eqykl2` (the first branch in the empty repo,
so GitHub made it the default for now). ❓ Riley: once the docs are approved, should this become
`main`?

### D-012 — Player death returns to the last save
**Accepted** (Riley, 2026-10-08). Saves happen when you sleep, when you come home, when you
close the game, and on F5.

### D-013 — Godot 4.7.2 (.NET), pinned
**Accepted** (follows D-002). Latest stable at setup. Pinned in `game/ZTown.Game.csproj` and
`GODOT_VERSION` in the workflow; upgrade both together.

### D-014 — Phase 0 renders with flat placeholder shapes
**Superseded** by D-015: Riley asked for Zomboid-level quality.

### D-015 — Procedural painted art pipeline (until commissioned art)
**Proposed.** `tools/art` (Python) paints the stand-in art: textures (siding, brick, shingles,
grass, asphalt…) projected onto the isometric shapes, and people/trees built from 3D shapes
rendered to sprites with Zomboid's camera angle, shading and outlines. Every sprite has its
final size, origin and name, so commissioned art can replace any piece without code changes.
Riley can't be sent clips from here (the build environment can't reach image sites), so
screenshots pasted into chat are the way to steer the look.

### D-016 — Layered, tinted clothing (paper doll)
**Proposed.** Characters are drawn as layers (body, bottoms, shoes, top, jacket, hair, hat, held
item); each garment sheet is neutral grey and tinted in-game. Each layer is rendered with the
body as an invisible occluder so layers stack correctly in any combination. This is how
Zomboid-style customisation and varied zombies work without drawing every combination.
Cost: ~40 MB of character sheets for 14 garments × 14 animations; more garments add ~3 MB each.

### D-017 — The story spans 5–7 years
**Accepted** (Riley). Eras: Collapse, Lights out, First winter, Thaw, Scarcity, Long after.

### D-018 — Horses for travel
**Accepted** (Riley). Ride horses when cars can't be found or fixed. Planned for Phase 5 (P2).

### D-019 — Memere doesn't age
**Accepted** (Riley: "stay the way i remember her"). Over the 5–7 years the world ages around
her (eras, overgrowth, the player and Dad can change), but memere's look stays the same.
