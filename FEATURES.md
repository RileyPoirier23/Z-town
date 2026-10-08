# Features

> **Status: draft, waiting for Riley's approval.**

Feature list for Project Zomboid parity (checked against Build 42, the current build as of
2026), plus this game's own systems. Each one has a priority:

| Priority | Meaning |
|---|---|
| **P0** | Phase 1 vertical slice. Without these there's no game to try. |
| **P1** | Core game: needed for Chapter 1–2 to be playable start to finish. |
| **P2** | Full game: needed before the story is complete. |
| **P3** | Stretch / polish / post-launch. |

Items marked **(B42)** are things Zomboid added or reworked in Build 42.

---

## 1. Memere & the safehouse (the heart)

| Feature | P |
|---|---|
| Protection guarantee: `IsProtected`, single harm/targeting gate, no health component, zombie nav excludes her rooms | **P0** |
| Protection tests: architecture, gate, soak, data — build fails on any breach | **P0** |
| Comfort meter (data-driven factors) + mood states | **P0** |
| Mepsi, cigarettes, puffers as consumable supplies that run down | **P0** |
| TV with a broadcast schedule and **one** parody show | **P0** |
| Comfort feeds the player's mood (stress/unhappiness relief, sleep quality) | **P0** |
| Memere idle life: chair, napping, a smoke, a Mepsi, reacting to what you bring her | **P0** (placeholder lines) |
| Full TV schedule: several parody shows, segments, emergency broadcasts, dead air | P1 |
| Reruns box (found item) keeps her shows airing off the generator | P1 |
| Heart texts on the player's phone in the early days ❓ | P1 |
| Memere's safe room (zombie-proof) during breaches | P1 |
| Memere stories (from real details only) | P1 |
| Upgradable safehouse: cozier and more fortified per chapter, visible upgrades | P1 |
| Radio + emergency broadcast system that evolves as society collapses | P1 |
| Memere comments on the house state (mess, noise, boarded windows) | P2 |

## 2. Dad

| Feature | P |
|---|---|
| "Find Dad" main quest, Dad's place as a location | P1 |
| Dad state: out of it / withdrawal / steady / relapsed / missing | P1 |
| Keep-him-clean thread: withdrawal days handled via objectives and supplies | P1 |
| Recurring Dad side quests (wanders off, gets in trouble, rescue) | P2 |
| Dad as helper when steady (❓ which skills) | P2 |

## 3. Player character & survival

| Feature | P |
|---|---|
| Movement: walk, run, sneak, 8 directions | **P0** |
| Needs: hunger, thirst, fatigue | **P0** |
| Needs: boredom, unhappiness, stress, panic | P1 |
| Pain, temperature, wetness, illness (cold/flu, food poisoning) | P1 |
| Injuries: scratches, lacerations, bites, deep wounds, fractures, burns, bleeding | P1 |
| First aid: bandage, disinfect, stitch, splint, remove glass/bullets | P1 |
| Moodles-style status icons | **P0** (needs only) / P1 (all) |
| Health panel (body-part view) | P1 |
| Player infection (Zomboid-style), with story-mode options ❓ | P1 |
| Character creation: name and look (skin, hair, clothes, colours) | **P0** ✔ |
| Character creation: traits (positive/negative with point budget), occupation | P1 |
| Layered clothing (hair, hat, top, jacket, bottoms, shoes), each tinted | **P0** ✔ |
| Clothes as items: find, wear, swap; warmth, bite/scratch protection, condition, wet/bloody | P1 |
| More garments (dresses, coats, uniforms by area, gloves, glasses, backpacks) | P2 |
| Skills that level by doing; skill books & magazines | P1 |
| Skill list: Carpentry, Cooking, First Aid, Electrical, Mechanics, Foraging, Agriculture, Fishing, Trapping, Tailoring, Maintenance, Aiming, Reloading, Short/Long Blade, Short/Long Blunt, Axe, Spear, Sprinting, Lightfooted, Nimble, Sneaking, Strength, Fitness | P1–P2 |
| B42 craft skills: Carving, Blacksmithing, Welding, Masonry, Pottery, Knapping, Glassmaking **(B42)** | P3 |
| B42 animal skills: Animal Care, Butchering, Tracking **(B42)** | P2 (Butchering/Tracking for hunting) / P3 |
| Weight, encumbrance, fitness/strength body changes | P2 |
| Sleep, beds, sleeping bags, insomnia | P1 |
| Reading (skill books, recipe magazines, entertainment) | P1 |
| Smoking as a player trait option (the player can share memere's habit) | P2 |

## 4. World

| Feature | P |
|---|---|
| Tile world, chunks, multiple floors, stairs | **P0** |
| Memere's house + one street block from OSM | **P0** |
| Persistent lootable containers, loot tables by building/room type | **P0** (one store + house) / P1 |
| Doors (lock, break), windows (open, smash, climb), curtains | **P0** (doors) / P1 |
| Barricading doors and windows (planks, metal) | P1 |
| Day/night cycle | **P0** |
| Seasons, temperature | P1 |
| Weather: rain, snow, fog, wind, storms; fog hurts visibility & aiming **(B42)** | P1 |
| Darker nights; light passes through curtains/barricades **(B42)** | P1 |
| Power & water shutoff after configurable days | **P0** (power) / P1 (water) |
| Generators, fuel, generator noise attracting zombies | **P0** |
| Wiring, solar panels, batteries | P2 |
| Basements **(B42)** and tall buildings (NB has both) | P2 |
| World events: helicopter, house/car alarms, gunshots, fires, radio chatter | P1 |
| Fire spread | P2 |
| Rope (sheet rope) escapes from upper floors | P2 |
| Vehicles: drive, fuel, keys/hotwire, damage, repair, trunk storage | P1 |
| EVs with batteries vs gas cars | P2 |
| Gas goes stale, cars get harder to run as years pass | P2 |
| **Horses**: find on farms, tame, feed/water/shelter, ride, saddlebags, panic near zombies, can be hurt | P2 |
| Horse-drawn cart for big hauls | P3 |
| Other survivors (radio, encounters, traders) | P2 |
| Wild animals (deer, moose ❓), hunting, butchering **(B42)** | P2 |
| Farm animals / husbandry **(B42)** | P3 |
| Sandbox settings for everything (zombies, loot, shutoffs, animals) **(B42-style)** | P2 |

## 5. Map pipeline

| Feature | P |
|---|---|
| OSM import tool: bbox → roads, buildings, land use, water, POIs → game tiles | **P0** (one block) |
| Procedural building shells + interiors by type: house | **P0** |
| Interiors: corner store/gas station, pharmacy | **P0** (one store) / P1 |
| Interiors: grocery, big-box, school, church, apartment, mall, hospital, police, fire hall, restaurants | P1–P2 |
| Hand-edit & **lock** buildings | **P0** (memere's house) |
| In-editor notes + photos per building | P1 |
| Era overlays (vegetation, collapse, wrecks) across 5–7 years, driven by chapter | P2 (P1: Collapse era only) |
| Full Moncton / Riverview / Salisbury coverage | P2 |
| OSM attribution in credits and map screen | **P0** |

## 6. Zombies

| Feature | P |
|---|---|
| Zombie AI: wander, sight & sound detection, chase, attack doors | **P0** |
| Hordes, migration, meta-simulation off-screen | P1 |
| Types/speeds: shambler, fast shambler, sprinter, crawler; lunges | P1 |
| Barricade breaking, thumping draws more | P1 |
| Zombie variety in clothing (outfits by area: nurses near hospital, etc.) | P2 |

## 7. Combat

| Feature | P |
|---|---|
| Melee with weight, stagger, knockdown, stomp | **P0** (basic) / P1 |
| Weapon durability & repair | P1 |
| Firearms with reticle aiming **(B42)**, noise, ammo, reloading | P1 |
| Shoving, multi-hit, fatigue from swinging | P1 |
| Bows/crossbows (craftable) | P3 |

## 8. Base building, crafting, food

| Feature | P |
|---|---|
| Recipes (data-driven), crafting UI | P1 |
| Crafting stations/workbenches **(B42)** | P2 |
| Build walls, fences, doors, floors, furniture | P1 |
| Rain collectors, water purification | P1 |
| Cooking: stove/oven/BBQ/campfire, recipes, food quality | P1 |
| Food spoilage; fridges/freezers that need power | **P0** (fridge) / P1 |
| Donairs: a donair shop as a location, frozen donair meat that spoils without power, making donairs for memere | P1 |
| Farming (seasons matter in NB), foraging, fishing, trapping | P2 |
| Tailoring, clothing repair, insulation for winter | P2 |
| Smithing/pottery/glass/knapping tech tree **(B42)** | P3 |

## 9. Story & quests

| Feature | P |
|---|---|
| Quest system (data-driven), quest log | **P0** (one objective) / P1 |
| Chapters with time skips | P1 |
| In-engine scripted scenes / cutscenes | P1 |
| Collectible lore: texts, news alerts, flyers, receipts, social posts | P1 |
| Early-days phone: battery, texts, social feed, service dying | P1 |
| The ending (talk to Riley first) | P2 |

## 10. Modern-day flavour

| Feature | P |
|---|---|
| Parody brand registry (`data/brands`), every brand approved by Riley | **P0** (Mepsi, cigarettes) |
| Parody fast food, phones, social media, delivery apps, big-box, gas, pharmacy, streaming | P1 |
| Smart-home gadgets failing, abandoned delivery robots | P2 |

## 11. UI & presentation

| Feature | P |
|---|---|
| HUD, moodles, interaction prompts, context menu | **P0** |
| Inventory: drag & drop, containers, weight | **P0** (basic) / P1 |
| Crafting, health, skills panels | P1 |
| World map with annotations | P1 |
| Main menu, settings, keybinds | **P0** (minimal) / P1 |
| Controller support | P1 |
| Accessibility: subtitles, colourblind modes, text size, screen shake toggle | P1 |
| Dynamic lighting, flashlights, outages darken the town | **P0** (house power) / P1 |
| Smooth per-corner lighting, fog of war (what you can't see is darkened, zombies there hidden) | **P0** ✔ |
| Roofs that hide when you go inside, wall cutaway, see-through trees | **P0** ✔ |
| Speech over heads | **P0** ✔ |
| Animations: walk/run/sneak, melee, shoot, climb, carry, craft, eat, sit, idles | P0 (walk/run/idle/swing) / P1–P2 |
| Ambient audio, music system, sound propagation | P1 |
| Credits incl. OSM attribution and asset licences | **P0** |

## 12. Tech

| Feature | P |
|---|---|
| Versioned save/load with migrations | **P0** |
| Unit tests: inventory, needs, power, comfort, protection | **P0** |
| Data schemas + validator in CI | **P0** |
| Dialogue YAML, [DRAFT] tag, release gate, review tool, spreadsheet export | **P0** |
| Windows build + installer + auto-update (Velopack) + GitHub Release | **P0** |
| Portable .exe with "new version" check (like CageBoss) | **P0** |
| 60 fps on mid-range PC; profiling overlay in dev | P1 |
| Modding via data folders | P3 |
| Multiplayer | Not planned (❓ co-op with family someday?) |
