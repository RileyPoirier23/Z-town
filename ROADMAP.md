# Roadmap

> **Status: draft, waiting for Riley's approval.**
> Phases are in order. Every phase ends with a short summary to Riley: what works, what's
> placeholder, and what needs his input (especially dialogue to review). No dates: each phase is
> done when its checklist is done and the tests pass.

## Phase 0 — Foundations

The things everything else stands on. Little to look at, but it has to be right first.

- [ ] Godot 4 .NET project, solution layout (`src/Core`, `src/Game`, `src/Launcher`, `tests/`)
- [ ] Core entity/component model, game clock, deterministic RNG
- [ ] **Protection gate + `IsProtected` + all four protection test suites** (architecture, gate, soak, data)
- [ ] Data loading + JSON schemas + validator (`tools/validate`)
- [ ] Save/load v1 with migration framework and fixture tests
- [ ] Dialogue YAML format, loader, [DRAFT] tag in dev builds, release gate in CI
- [ ] Dialogue review tool (local web page) + `.xlsx`/`.csv` export
- [ ] Placeholder art generator (consistent sizes per `ART_SPEC.md`)
- [ ] CI: tests on every push; Windows export, Velopack installer, portable exe, GitHub Release on version bump (same rules as CageBoss)
- [ ] Launcher with Velopack hooks + update check; portable "new version" dialog
- [ ] `CHANGELOG.md`, `DECISIONS.md` kept up to date

**Riley's input needed:** approve the docs; pick the parody cigarette brand name.

## Phase 1 — Vertical slice

One block of town, played start to finish, as the full EXE that auto-updates.

- [ ] OSM import of memere's block (❓ which street) → tiles, roads, lots, building shells
- [ ] Memere's house, hand-built and locked (from Riley's photos/notes)
- [ ] One store on or near the block (corner store / gas station with a pharmacy shelf ❓ or a pharmacy)
- [ ] Isometric rendering: floors, walls with cutaway, 2 floors, stairs, doors, windows
- [ ] Player: walk/run/sneak, interact, basic melee, hunger/thirst/fatigue, moodles
- [ ] A few zombies: wander, see/hear, chase, bang on doors, die to melee
- [ ] Inventory + containers + loot tables for the house and the store
- [ ] Mepsi, cigarettes, puffers as items; giving them to memere
- [ ] Power: grid on → grid off on day N → generator + fuel; lights/TV/fridge depend on it
- [ ] Memere: chair, idle, nap, comfort meter, mood states, placeholder lines
- [ ] TV with one parody show on a schedule
- [ ] Player death → wake-up at the house (or ❓ rewind)
- [ ] Save/load everything above
- [ ] Installer from GitHub Release, auto-update tested by shipping v0.1.0 → v0.1.1

**Riley's input needed:** memere's house layout, the block, review the slice's dialogue lines,
play it.

## Phase 2 — Chapter 1: Collapse (Days 1–14)

- [ ] Character creation (name, traits, occupation)
- [ ] Full needs, injuries, first aid, health panel, illness, player infection
- [ ] Skills + books; carpentry & barricading
- [ ] Day/night polish, seasons, weather, temperature
- [ ] Vehicles (gas), keys/hotwire, fuel, trunk
- [ ] Phone: battery, texts (heart texts ❓), social feed, service collapse
- [ ] TV schedule expanded, emergency broadcasts, radio
- [ ] Quest system, quest log, Chapter 1 main quest incl. **Find Dad**
- [ ] Interiors: pharmacy, grocery, gas station, more house types
- [ ] More of Moncton (❓ which neighbourhoods first)
- [ ] Hordes + meta-simulation, zombie types, sandbox settings
- [ ] Firearms with reticle aiming
- [ ] Controller support, keybinds, accessibility options
- [ ] Ambient audio, music system, sound propagation

## Phase 3 — Chapter 2: Lights out

- [ ] Grid/water shutoff story beat, generator economy, wiring
- [ ] Dad: withdrawal thread, states, side quests
- [ ] Cooking, spoilage, rain collectors, water purification
- [ ] Building: walls, fences, furniture; safehouse upgrade tiers
- [ ] World events: helicopter, alarms, fires, survivors' radio chatter
- [ ] Era overlay system (Collapse → Lights out)
- [ ] Riverview (❓ or Salisbury) import

## Phase 4 — Chapter 3: First winter

- [ ] Snow, cold, insulation, firewood, heating
- [ ] Tailoring, winter clothing
- [ ] Hunting/trapping/butchering, foraging, fishing
- [ ] Basements, taller buildings
- [ ] Era overlay: First winter

## Phase 5 — Chapters 4–5: Thaw & Long after

- [ ] Farming, other survivors, trading
- [ ] Salisbury (❓ or Riverview) import, travel between towns
- [ ] Era overlays: Thaw, Long after (*Last of Us Part II*-style overgrowth, collapsed landmarks)
- [ ] Dad's relapse arc
- [ ] EVs, solar, crafting stations

## Phase 6 — Ending & polish

- [ ] **The ending, written with Riley**
- [ ] Commissioned art swap-in (see `ART_SPEC.md` §9)
- [ ] Performance pass (60 fps mid-range), save compatibility check across all versions
- [ ] Full dialogue approval sweep (release gate must pass)

## Later / stretch

- B42-style tech tree (smithing, pottery, glass, knapping)
- Farm animals / husbandry
- Modding hooks
- ❓ Co-op so family can play together
