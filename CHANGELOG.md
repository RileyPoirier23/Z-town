# Changelog

## Unreleased

### Added (Chapter 1 systems)
- Quests and quest log, Chapter 1 objectives incl. **Find Dad**; world map with markers.
- Dad: states, follows you, can be brought home.
- Phone: battery, texts (memere's heart texts), service dying over time.
- Barricading doors and windows with planks.
- Sound: procedural SFX, ambience and music; zombies hear what you hear.
- Memere's everyday life: asks for supplies when low, "I love and miss you" after time away,
  reacts to her shows.
- Injuries, bleeding, bandaging, healing; sleep screen.
- Cars parked on the real streets: keys, hotwiring, fuel, crashes, trunks.
- Weather and seasons with Moncton temperatures; wet and cold.
- Character creation: a job (before all this) and traits with a point budget.
- Skills that level by doing; crafting and cooking panel (C), skills panel (K):
  donairs, hot soup, bandages, spiked bat, planks.
- Pause menu with settings.

### Added (art pass)
- Zomboid-style look: painted siding, brick, shingles, grass, roads and sidewalks; hip roofs
  that hide when you're inside; wall cutaway; see-through trees; smooth lighting; fog of war
  (what you can't see is darkened and zombies there are hidden); night and indoor light.
- Layered, tinted clothing for everyone; zombies get random outfits.
- Character creator (name, skin, hair, clothes, colours) and a title screen.
- Zomboid-style HUD: moodle badges, clock, memere's card, equipped item, speech over heads.
- Item and moodle icons; inventory shows icons.
- Story timeline set to 5–7 years; horses and clothing added to the plan.

### Added (Phase 0)
- Simulation core (`src/Core`): clock, deterministic RNG, tile map with doors/windows, A*
  pathing, needs and moodles, inventory and loot, power (grid shutoff + generator), memere's
  comfort and supplies, TV schedule with emergency broadcasts and reruns, zombies (sight,
  hearing, chase, door-breaking), world events, versioned saves with migrations.
- Memere protection: harm gate, targeting, protected zones, and four test suites that fail the
  build on any way to hurt her.
- Data files for items (Mepsi, smokes, puffers, donairs…), loot, TV, comfort; draft dialogue.
- Dialogue review tool (browser + spreadsheet), data validator, release check.
- Godot project with a playable stand-in house and store (placeholder graphics).
- Windows build: Velopack installer + self-updating portable zip, GitHub Releases, same rules as CageBoss.
- Placeholder art generator.

### Added
- Planning docs for review: `DESIGN.md`, `FEATURES.md`, `ROADMAP.md`, `ART_SPEC.md`,
  `DECISIONS.md`, and `FACTS.md` (what's known about memere, Dad and the family).
