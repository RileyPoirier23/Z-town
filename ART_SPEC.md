# Art, Animation & Audio Spec

> **Status: draft, waiting for Riley's approval.** Everything shipped before commissioned art
> arrives is a **placeholder** and is listed in `assets/PLACEHOLDERS.md` (generated).

## 1. Look

Zomboid-inspired: **muted, painterly isometric**. Desaturated, slightly warm palette; readable
silhouettes; every room cluttered and lived-in. Night is genuinely dark. Memere's house is the
warmest-looking place in the game (warm lamp light, colour), and the town gets greyer and more
overgrown as eras pass.

## 2. Projection & grid

| Thing | Value |
|---|---|
| Projection | 2:1 dimetric ("isometric") |
| Floor tile footprint | **128 × 64 px** (the diamond) |
| Tile sprite canvas | **128 × 256 px**, floor diamond sits at the bottom, origin = bottom-centre of the diamond |
| Floor (storey) height | **192 px** (one level up = 192 px screen offset) |
| World scale | 1 tile ≈ 1 m |
| Pixel density | Authored at the sizes above; scaled in-engine. No mixed densities. |

## 3. Tiles

### 3.1 Categories

| Category | Examples | Notes |
|---|---|---|
| `floor` | grass, asphalt, sidewalk, carpet, linoleum, hardwood | full diamond |
| `wall` | siding, brick, drywall, fence | two orientations: **W** (north-west edge) and **N** (north-east edge), plus corner pieces |
| `wallcut` | cutaway version of each wall | short stub shown when the player is behind it |
| `door`, `window` | each with open / closed / broken / barricaded (1–4 planks, metal) states | per wall orientation |
| `roof` | shingle, flat | per slope direction |
| `furniture` | couch, memere's chair, TV, fridge, bed | may span multiple tiles; split into per-tile sprites with a shared `object` id |
| `nature` | trees, bushes, tall grass, weeds-in-cracks | era-aware variants |
| `vehicle` | per model, 8 directions | separate sheet format (§5) |
| `overlay` | blood, litter, snow, vines, cracks, scorch | layered on top of base tiles; used by era overlays |

### 3.2 Naming

```
tile_<category>_<name>_<variant>_<state>_<orient>.png
tile_wall_vinylsiding_white_01_intact_W.png
tile_door_interior_wood_01_open_N.png
tile_overlay_vines_02.png
```

All lowercase, `_` separated, variant numbered from `01`. Tiles are packed into atlases at build
time by `tools/atlas` — artists only deliver single PNGs.

### 3.3 Tile data

Each tile has a sidecar entry in `data/tiles/*.json`: id, sprite, solid, blocks sight, blocks
sound (0–1), flammable, climbable, container (loot table id), light emitter, era variants.

## 4. Characters

### 4.1 Rig: layered paper-doll sprites

Every character (player, memere, Dad, survivors, zombies) is built from **layers** that share
exactly the same frame grid, so clothing is swappable (like Zomboid):

```
draw order: shadow → body (skin tone) → underwear → bottom → shoes → top → outerwear → hair → hat
            → held item (back) / held item (front depending on direction) → effects (blood, wet, snow)
```

| Thing | Value |
|---|---|
| Frame size | **256 × 256 px** |
| Character height | ~**170 px** for an adult (matches 1.7–1.8 m at our tile scale) |
| Anchor | feet, bottom-centre at (128, 236) |
| Directions | **8** (N, NE, E, SE, S, SW, W, NW) |
| Frame rate | authored at 12 fps; engine blends between states |

### 4.2 Animation states

| Group | States (frames per direction) |
|---|---|
| Locomotion | idle (8), idle variants ×3 (16), walk (8), run (8), sneak (8), stumble (8) |
| Melee | swing 1H (8), swing 2H (10), stab (8), shove (6), stomp (8) |
| Firearms | aim pistol (4), aim rifle (4), shoot (4), reload (12) |
| Movement | climb window (16), climb fence (16), climb sheet rope (8), fall (8), get up (12) |
| Interaction | pick up (6), search container (8, loop), carry (8), craft (8, loop), eat (8), drink (8), read (loop), sit (6), sit idle (loop), sleep (loop), smoke (8, loop) |
| Hurt (**player, Dad, zombies, survivors only**) | hit react (4), knockdown (8), death (12) |
| Zombie | shamble (8), lunge (8), attack (8), thump door (8), crawl (8), eat (8), get up (12) |

### 4.3 Memere's rig (restricted)

Memere's rig has **only** these animation states, and the engine refuses to play any state not
in her allow-list (checked by the protection data test):

`idle_chair`, `idle_chair_variants`, `nap_chair`, `watch_tv`, `smoke`, `drink_mepsi`, `puffer`,
`talk`, `laugh`, `react_happy`, `react_gift`, `walk_slow`, `sit_down`, `stand_up`,
`look_at_player`, `wave`.

There is no hit-react, fall, sick, scared, cough-fit or death state for her rig. `puffer` is a
calm everyday motion, not a struggle.

### 4.4 Naming

```
char_<layer>_<item>_<anim>_<dir>.png           (sheet: frames left→right)
char_top_hoodie_grey_walk_SE.png
char_body_memere_watch_tv_S.png
```

## 5. Vehicles

8 directions × (intact, damaged, wrecked) + doors/trunk/hood open overlays. Frame 384 × 384 px.
Naming `veh_<model>_<state>_<dir>.png`.

## 6. Items & UI

| Thing | Value |
|---|---|
| Inventory icons | 64 × 64 px, transparent, slight top-left light |
| World item sprites | 64 × 64 px on the floor, origin bottom-centre |
| Moodle icons | 48 × 48 px, 4 severity tints |
| UI font | readable sans with Acadian French accents supported (é, è, ç) |
| UI scale | 100–200% setting, text-size setting separate |

Naming `item_<name>.png`, `moodle_<name>_<level>.png`, `ui_<widget>_<state>.png`.

## 7. Lighting

- Per-tile light grid (like Zomboid): ambient (sun/moon/weather) + room lights + emitters,
  propagated per tile, blended per vertex. Cheap and works with hundreds of rooms.
- Godot 2D `PointLight2D` only for moving lights (flashlights, car headlights, fire flicker).
- Power state drives every powered light: an outage visibly turns the town dark.
- Optional normal maps for tiles later (P3).

## 8. Audio

| Thing | Spec |
|---|---|
| Format | `.ogg` (Vorbis) for music/ambience, `.wav` for short SFX; 48 kHz |
| Loudness | music −18 LUFS, ambience −24 LUFS, SFX normalised per category |
| Naming | `sfx_<category>_<name>_<nn>.wav`, `amb_<place>_<time>.ogg`, `mus_<cue>.ogg`, `tv_<show>_<segment>.ogg` |
| Sound propagation | every sound has a game-world radius (zombie hearing), separate from its audible volume |
| Music system | layered cues: home (warm), out (tense), danger (horde), plus stingers; crossfades by state |
| Ambience | per place (house hum, fridge, generator, wind, rain, snow quiet, birds by season) |

## 9. Placeholders now, and where real art matters most

### Placeholders (Phase 0–1)

`tools/placeholder_art` generates **flat-shaded, correctly sized** sprites for every tile,
character layer, item and moodle in the data files, labelled with their id, so everything is
playable and every slot is the right size for the real art. Audio placeholders are short
synthesized tones/noise bursts per category. All are listed in `assets/PLACEHOLDERS.md`.

### Where commissioned or licensed art makes the biggest difference (in order)

1. **Memere** — her character, her chair, her idle/nap/TV animations. Commission this. It's the
   emotional centre and should be made from Riley's photos.
2. **Memere's house interior** — every object in it, from photos. Commission.
3. **The player and Dad character rigs** + clothing layers. Commission the base body + a starter
   wardrobe; a licensed modern-clothing pack can fill out variety later.
4. **Zombies** — reuse the human rig with gore/decay overlays; commission the overlays.
5. **Town tiles** (siding, brick, asphalt, Maritimes house styles) — a licensed modern isometric
   tileset could cover the bulk, repainted to the palette. Maritime details (vinyl siding,
   steep roofs, snowbanks) are worth commissioning.
6. **The parody TV shows** — short animated or still segments; a good place for a single
   illustrator with a funny style.

Free starting points if needed: Kenney (CC0) for prototyping, Sonniss GDC bundles
(royalty-free) for SFX. Check every licence and record it in `assets/LICENSES.md`.

### Audio priorities

1. **Main theme / home theme** — commission a composer. This is the music people will remember.
2. **TV show stings and jingles** — commission with the theme, or a small licensed library.
3. Zombie vocals, weapon impacts, generator, house ambience — licensed SFX libraries are fine.
