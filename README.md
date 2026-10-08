# Memere (working title)

A story-driven isometric survival game, in the style of Project Zomboid, set in present-day
Moncton, Riverview and Salisbury, New Brunswick. You keep memere safe and comfortable in her
house while the world comes apart: scavenge for her Mepsi, her cigarettes and her puffers, keep
the power on so her shows keep playing, and keep going back out for Dad.

Made in memory of memere.

## Status

Phase 0 (foundations). Planning docs:

| Doc | What |
|---|---|
| [`FACTS.md`](FACTS.md) | What's known about memere, Dad and the family. The only source for anything about real people. |
| [`DESIGN.md`](DESIGN.md) | Story outline, core loop, systems, tech architecture |
| [`FEATURES.md`](FEATURES.md) | Full feature list with priorities |
| [`ROADMAP.md`](ROADMAP.md) | Phases, starting with the vertical slice |
| [`ART_SPEC.md`](ART_SPEC.md) | Art, animation and audio spec, placeholder plan |
| [`DECISIONS.md`](DECISIONS.md) | Decision log |
| [`CHANGELOG.md`](CHANGELOG.md) | Changes |

## Building and running (dev)

Needs the .NET 8 SDK and Godot 4.7.2 (.NET edition).

```bash
dotnet test                                        # unit tests + memere protection tests
dotnet run --project tools/ZTown.Tools -- validate # check game data
dotnet run --project tools/ZTown.Tools -- release-check   # what still needs approval
python tools/dialogue_review/serve.py              # review dialogue in the browser
godot --path game                                  # run the game (or open game/ in the Godot editor)
```

Pushing to `main` or a `claude/*` branch builds the Windows installer on GitHub (Actions → the
run → Artifacts). Bumping `version.txt` publishes a release once all dialogue is approved.

## Credits

Map data © OpenStreetMap contributors, available under the Open Database Licence (ODbL).
All brands in the game are parodies.
