# Memere (working title) — Design

> **Status: draft, waiting for Riley's approval.** Nothing here is final until Riley signs off.
> Facts about real people come only from [`FACTS.md`](FACTS.md). Open questions are marked **❓**.

## 1. The idea in one line

> I would rather lose everything else in an apocalypse than lose my memere.

An isometric, story-driven survival game in the style of Project Zomboid, set in Moncton,
Riverview and Salisbury, New Brunswick, in the present day. You keep memere safe and
comfortable in her house while the world comes apart: you scavenge for her Mepsi, her
cigarettes and her puffers, keep the power on so her shows keep playing, and keep going back
out for Dad.

## 2. Pillars

1. **She is safe. Everything else is at risk.** Tension comes from what *you* could lose: your
   health, your supplies, the house, the power, time, Dad. Never her.
2. **Home is the reward.** Every run ends back at memere's house. Bringing her something good
   and seeing her settle in her chair with her show on is the payoff loop.
3. **Real place, real people.** The roads are the real roads (OpenStreetMap). The people are
   only what Riley says they were.
4. **Zomboid-grade survival.** Needs, injuries, skills, loot, power, weather, hordes. It's a
   real survival game, not a walking sim with zombies painted on.
5. **Plain talk.** Dialogue sounds like people from here. Short. No speeches.

## 3. Core loop

```
            ┌──────────────── at the house ────────────────┐
            │ check memere's comfort  ->  plan a run        │
            │ fix / fortify / cook / keep generator fueled  │
            └──────────────┬───────────────────────────────-┘
                           │  go out (car / foot)
                           ▼
            ┌──────────────── out in town ─────────────────┐
            │ scavenge (Mepsi, smokes, puffers, food, fuel) │
            │ avoid / fight zombies, noise & light matter   │
            │ story objectives, Dad, other survivors        │
            └──────────────┬───────────────────────────────-┘
                           │  come home (or don't make it)
                           ▼
            ┌──────────────── payoff ──────────────────────┐
            │ give memere what you found -> her comfort up │
            │ her comfort feeds YOUR morale (stress, mood)  │
            │ time passes, the world gets worse             │
            └──────────────────────────────────────────────┘
```

**Why her comfort matters mechanically:** her comfort meter never hurts her. It changes how
she acts (talkative, napping, quiet, asking for things) and it feeds the *player's* mood.
Time near a happy memere lowers your stress and unhappiness and helps you sleep. A run that
brings home the right thing is how you recover.

## 4. Memere

### 4.1 The protection guarantee (absolute rule #1)

Memere cannot be harmed, by anything, ever. This is enforced in code, not by convention:

- Memere's entity carries `IsProtected = true`. She has **no health component at all**; she has
  a `Comfort` component instead. There is nothing for a bug to reduce to zero.
- Every system that can affect an entity — damage, infection, illness, AI targeting, events,
  scripted scenes, fire, falls, vehicles — has to go through one gate (`Harm.Apply` /
  `Targeting.CanTarget`). The gate refuses any protected entity.
- **Zombie AI never paths to her.** She's excluded from target selection, and her rooms are
  excluded from zombie navigation.
- **Automated tests fail the build** if any system can harm her:
  1. *Architecture test*: finds every class implementing a harm/targeting/event interface (by
     reflection) and checks it routes through the gate.
  2. *Gate test*: feeds every damage type, infection source and event at a protected entity and
     asserts nothing changes.
  3. *Soak test*: runs thousands of simulated days with hordes, fires, breaches and car crashes
     around the house, and asserts her state never shows harm and no zombie ever targeted her.
  4. *Data test*: no event, quest, cutscene or dialogue file may apply a harm effect to her or
     use a "hurt/sick/scared" animation or pose on her.
- **No cutscene, animation, sprite, or line** shows her injured, sick, dying, or scared in a
  graphic way. The art spec has no such animations for her rig, so they can't be played.

### 4.2 What happens when things go wrong

Failure hits the player, the house, the supplies and the power, never her.

| Failure | What happens | ❓ |
|---|---|---|
| Player dies out in town | You wake up at the house a day later, stiff and sore. You lose what you were carrying, and time passed. | ❓ Who brought you home? A rewind instead? |
| Player bitten / infected | Infection runs its course for the player (Zomboid-style). If it would kill you, you get the same wake-up as above. ❓ Should there be a story cure, or should infection be turned off for the player in story mode? | ❓ |
| Zombies get inside the house | Memere's room is a safe room zombies can't path into. You have to clear the house, and every door and window they came through is wrecked. Supplies inside can get spoiled or knocked over. | |
| Generator dies / out of fuel | The house goes dark and her show cuts out. Her comfort drops, the fridge starts to spoil. | |
| Run out of her Mepsi, smokes or puffers | She's unhappy and asks about them. You get a pharmacy or store run objective. She is **not** shown struggling to breathe. Low puffers = she worries and asks, nothing more. | |

### 4.3 Comfort meter

Comfort is 0–100. It is made up of factors, all data-driven (`data/memere/comfort.json`):

| Factor | Good | Bad |
|---|---|---|
| Power | lights and TV on | dark house |
| Warmth | house is warm | cold (NB winters make this a big one) |
| Mepsi | she has some | out |
| Cigarettes | she has some | out |
| Puffers | stocked | running low (she worries) |
| Food she likes | ❓ need her favourites | only canned stuff |
| Her shows | on schedule | TV off / no signal |
| House | tidy, quiet | mess, noise, boarded-up gloom |
| You | you're home, you came back | you've been gone a long time |
| Dad | ❓ | ❓ |

Comfort sets her **mood state** (e.g. *content*, *chatty*, *dozing*, *quiet*, *asking*).
Mood picks her idle animations and which lines can play. All states are calm; the lowest one is
"quiet and asking for something", never distress.

### 4.4 Her ambient life

From the brief: napping in her chair, commenting on what you're doing, reacting to what you
bring her, telling stories, knitting (❓ confirm she knit). Everything she says uses lines Riley
has approved.

- **The heart texts.** In the first days while the cell network still works, your phone buzzes
  with a text from her that's just a heart. When the network goes down, they stop, and that's
  where her lines about missing you can come in once you're home. ❓ Riley to confirm this is
  okay to use.
- **"I love and miss you."** Reserved for when you've been gone a while. Used sparingly so it lands.

### 4.5 The TV

- A real broadcast schedule (`data/tv/schedule.json`) with parody shows: a family-survey game
  show, a TV judge show, a paternity-test talk show, other game shows. Names are parodies,
  approved by Riley.
- Days 1–N: live broadcasts, then emergency broadcasts creeping in, then dead air on most
  channels. Later the house runs a "reruns" box (parody DVR/DVD/hard drive found on a run) so her
  shows keep "airing" off the generator.
- Each show has short parody segments (text and portraits at first, little animated clips later).
- When the TV is on and it's one of her shows, it's a big comfort boost.

## 5. Dad

From Riley: Dad is a drug addict and a deadbeat. Part of the story is keeping him clean and
alive. He's pretty out of it when you find him.

**Proposed arc (needs Riley's approval):**

1. **Where's Dad?** Early chapter. Phones still half-work. You go looking. His place is a mess.
2. **Finding him.** He's out of it. You get him somewhere safe, either to his place, fortified, or a
   spot near memere's. ❓ Does he come to memere's house?
3. **Keeping him clean.** A recurring thread. Withdrawal is a hard few days you get him through
   with water, food, rest, meds from the pharmacy and you being there. It's handled through
   objectives and his state, not shown in graphic detail, and the game never explains how to use
   anything.
4. **Keeping him alive.** He wanders off, gets into trouble, relapses. Each one is a rescue or
   side quest. He's the person in the game who **can** be in danger, so he carries the stakes
   memere doesn't.
5. **His ending.** ❓ Talk to Riley before writing it.

**Portrayal:** "In between", per Riley. You see him out of it, the mess, the consequences.
Drugs are generic and parodied ("pills", "dope"), with no instructions and no real brands, and
using them is never a player mechanic.

❓ Can Dad die (permanent fail of his thread / chapter retry), or only get hurt and go missing?

## 6. Story structure

Chapters are separated by **time skips**. Each skip moves the world to a new *era*, and the map
changes (§7.3). Chapter content below is an outline only, with no dialogue.

| # | Era | Rough time | Shape |
|---|---|---|---|
| Prologue | Day 0 | Outbreak day | ❓ Where is the player when it starts? Get to memere's. |
| 1 | Collapse | Days 1–14 | Lock down the house, first runs (store for Mepsi/smokes, pharmacy for puffers), phones and social media failing, heart texts, find Dad. |
| 2 | Lights out | ~1–2 months | Grid power fails, water goes. Generator, fuel runs, Dad's withdrawal. Radio chatter, other survivors. |
| 3 | First winter | ~6 months | NB winter: heat, snow, cold-weather runs, firewood, the house as a fortress. |
| 4 | Thaw | ~1 year | Riverview/Salisbury opened up, overgrown town, bigger hordes, Dad's relapse arc. |
| 5 | Long after | ~3+ years | *Last of Us Part II*-style overgrowth. Memere's house is cozy and fortified. |
| End | ❓ | ❓ | **Talk to Riley before writing the ending.** |

Main quests carry the chapter; side quests come from places (Dad, neighbours ❓, survivors,
landmarks Riley cares about). There's a quest log, cutscenes are in-engine scripted scenes, and
lore comes from collectibles (texts, news alerts, flyers, receipts, social posts).

## 7. The world

### 7.1 Real towns, from OpenStreetMap

- **Moncton, Riverview, Salisbury, NB**. Road, building, land-use, water and point-of-interest
  data comes from **OpenStreetMap** (ODbL), never Google. Google Maps/Street View is only Riley's
  own visual reference while he describes places to us.
- A tool (`tools/osm_import`) takes a bounding box, reads an OSM extract, and writes the game's
  tile format: roads, sidewalks, lots, building shells, water, trees, land use, POIs.
- Building interiors are generated by building type. Riley can hand-edit and **lock** important
  buildings (memere's house, Dad's place, landmarks). Locked buildings are never regenerated.
- An in-editor notes panel lets Riley attach notes and photos per building for later detailing.
- OSM attribution goes in the credits and on the map screen. (ODbL note: see `DECISIONS.md` D-007.)

### 7.2 Scale

1 tile ≈ 1 m, like Zomboid. The tri-city area is roughly the size of Zomboid's whole map, so we
build it town by town, starting with memere's block.

### 7.3 Eras and map change (time skips)

The map is the **base layer** (from OSM) plus **era overlays**. Each era is a deterministic
pass over the base map, seeded so the same world always ages the same way:

| Era | Changes |
|---|---|
| Collapse | Crashed/abandoned cars, open doors, broken windows, litter, police tape, a few fires |
| Lights out | Burned blocks, barricades by other survivors, dead streetlights, first weeds in cracks |
| First winter | Snowpack, collapsed carports/roofs under snow load, frozen river edges |
| Thaw | Tall grass, saplings in lots, flooded low ground near the river, some buildings caved in |
| Long after | Trees through roads, vines on walls, collapsed landmarks, deer/moose ❓ in town |

Locked buildings follow hand-authored era states instead (memere's house only gets *better*:
that's your upgrades).

### 7.4 Simulation

- Persistent, lootable world. Containers fill on first visit from loot tables by building/room type.
- Day/night, four seasons, weather (rain, snow, fog, wind, storms), temperature.
- Utilities: grid power and water shut off after configurable days; generators, fuel, wiring,
  solar later.
- Noise and light attract zombies. World events: helicopter, alarms, gunshots, fires, radio chatter.
- Vehicles (gas + EVs with batteries that die), keys/hotwire, damage/repair, trunk storage.
- Multi-floor buildings, basements, stairs, sheet-rope escapes.

### 7.5 Zombies

Hordes with sound and sight detection, migration, wandering. Sandbox options: shamblers / fast
shamblers / sprinters, lunges, crawlers, barricade breaking. Weighty melee and gunplay with
weapon durability. Aiming uses a reticle (like Zomboid Build 42).

## 8. Modern-day flavour

Parody brands everywhere (`data/brands/*.json`, names approved by Riley): Mepsi, memere's
cigarette brand ❓, fast food, phones, social media, delivery apps, big-box stores, gas
stations, pharmacies, streaming services. Phones die and service collapses; social media and
texts fill the first days; smart-home gadgets fail; EVs vs gas cars; abandoned delivery robots.

## 9. Technical architecture

| Layer | What | Tech |
|---|---|---|
| `src/Core` | Pure simulation: entities, needs, inventory, power, comfort, protection gate, save model. **No Godot dependency** so it's fast to test. | C# class library (.NET 8) |
| `src/Game` | Godot nodes: rendering, input, UI, audio, animation, glue to Core | Godot 4.x .NET (C#) |
| `src/Launcher` | Tiny Windows launcher: Velopack install/update hooks, update check, then starts the game | C# (.NET 8) |
| `data/` | Items, recipes, loot tables, quests, TV schedules, brands, comfort factors (JSON) and dialogue (YAML) | JSON Schema-validated |
| `tests/` | Unit tests, protection tests, soak tests, data validation | xUnit, runs with `dotnet test` |
| `tools/` | OSM importer, placeholder art generator, dialogue review tool, data validator | Python 3 + C# |

- **Save/load from day one**: versioned save format (`saveVersion`), a chain of migrations
  v1→v2→…, tests that load every old fixture save.
- **Data-driven**: anything a designer would tweak is in `data/`, hot-reloadable in dev builds.
- **Performance target**: 60 fps on a mid-range PC. Chunked world (32×32 tiles), only nearby
  chunks are simulated in full; distant zombies are simulated coarsely (like Zomboid's
  "meta" hordes).

## 10. Dialogue pipeline

See `ROADMAP.md` Phase 0. Summary:

- Lines live in `dialogue/*.yaml`. Each line: `id`, `speaker`, `text`, `context`,
  `status: draft | approved | rejected`, optional `notes`.
- Dev builds show **[DRAFT]** on any unapproved line. **Release builds fail CI** if any line in
  use is not `approved`.
- Review tool: a local web page (`tools/dialogue_review`) to read lines in context, edit,
  approve/reject. Exports to a spreadsheet (`.xlsx` + `.csv`).
- Memere's lines are built only from `FACTS.md`. When more is needed, the line is a
  placeholder like `[MEMERE: reacts to getting Mepsi — need a real phrase]`, and Riley is asked.

## 11. Presentation

Zomboid-inspired: muted, painterly isometric, readable silhouettes, cluttered lived-in rooms.
Dynamic lighting (power outages visibly darken the town), flashlights, ambient audio, music
system, sound propagation. Full UI: drag-and-drop inventory with weight, crafting, health panel,
annotated map, settings, keybinds, controller support. See `ART_SPEC.md`.

## 12. Open questions for Riley (collected)

1. Memere's favourite foods and snacks.
2. Where memere's house is, and its layout (photos/notes).
3. Her TV routine, and any phrases / stories / habits beyond `FACTS.md`.
4. Parody name for her cigarette brand (I'll propose options for you to pick).
5. Is the heart-text idea okay?
6. Where is the player when the outbreak starts (prologue)?
7. Where is Dad's place? Does Dad come to live at memere's?
8. Can Dad die, or only get hurt / go missing / relapse?
9. Player death: wake up at the house, or rewind to last save? Infection on or off for the player in story mode?
10. The ending (we talk before anything is written).
11. Game title: "Memere" (working title) vs "Z-Town" (repo name) vs something else.
