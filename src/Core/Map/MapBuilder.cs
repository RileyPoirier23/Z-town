using System.Text.Json;
using ZTown.Core.Data;
using ZTown.Core.Entities;
using ZTown.Core.Power;

namespace ZTown.Core.Map;

/// <summary>data/world.json: which map to play and which buildings are memere's and Dad's.</summary>
public sealed class WorldConfig
{
    public string Map { get; set; } = "riverview_centre";
    /// <summary>OSM id of memere's house (e.g. "w123456"). Null = pick a house near the middle until Riley chooses.</summary>
    public string? MemereHouse { get; set; }
    public string? DadHouse { get; set; }
    /// <summary>Zombies per 1000 outdoor tiles at the start.</summary>
    public float OutdoorZombiesPer1000 { get; set; } = 1.2f;
    public float BuildingZombieChance { get; set; } = 0.35f;
    /// <summary>No zombies start within this many tiles of memere's house.</summary>
    public int SafeRadius { get; set; } = 30;
}

/// <summary>Builds a playable world from a converted OpenStreetMap area.</summary>
public static class MapBuilder
{
    static readonly string[] TreeKinds = { "tree_maple_0", "tree_maple_1", "tree_maple_0", "tree_spruce_0", "tree_spruce_1", "tree_maple_autumn" };

    public static GameWorld Build(GameData data, MapFile mf, WorldConfig cfg, ulong seed)
    {
        var map = new TileMap(mf.Width, mf.Height);
        var bytes = Convert.FromBase64String(mf.Floors);
        for (int i = 0; i < bytes.Length && i < mf.Width * mf.Height; i++)
        {
            string floor = mf.FloorPalette[bytes[i]];
            ref var t = ref map.At(i % mf.Width, i / mf.Width);
            t.Floor = floor;
            t.Solid = floor is "water" or "void";
        }
        foreach (var o in mf.Objects)
        {
            int x = o[0].GetInt32(), y = o[1].GetInt32();
            if (!map.InBounds(new TilePos(x, y))) continue;
            ref var t = ref map.At(x, y);
            if (t.Solid || t.Floor is not ("grass" or "field" or "dirt")) continue;
            t.Object = TreeKinds[BuildingGen.StableHash($"{x},{y}") % TreeKinds.Length];
            t.Solid = true;
        }

        var w = new GameWorld(data, map, seed) { MapName = mf.Name };
        int roomId = 0;
        var gens = new Dictionary<Building, BuildingGen.Result>();
        foreach (var mb in mf.Buildings)
        {
            var b = new Building { Id = mb.Osm.Length > 0 ? mb.Osm : mb.Id, Kind = mb.Kind };
            foreach (var r in mb.Runs)
                for (int x = r[1]; x < r[2]; x++)
                    if (map.InBounds(new TilePos(x, r[0])) && !map.At(x, r[0]).Solid) b.Tiles.Add(new TilePos(x, r[0]));
            if (b.Tiles.Count < 12) continue;
            b.ComputeRoof();
            w.AddBuilding(b);
        }
        // all footprints first, so no building puts its door into a neighbour's lot
        foreach (var b in w.Buildings) gens[b] = BuildingGen.Generate(w, b, ref roomId);

        var home = PickHouse(w, cfg.MemereHouse, new TilePos(mf.Width / 2, mf.Height / 2), null, 0)
                   ?? throw new InvalidDataException("map has no house for memere");
        SetUpMemeresHouse(w, data, home, gens[home]);

        var dadHouse = PickHouse(w, cfg.DadHouse, w.Memere.Tile, home, 160);
        if (dadHouse != null) SetUpDad(w, data, dadHouse, gens[dadHouse]);

        w.SafeRadius = cfg.SafeRadius;
        SpawnZombies(w, cfg, home);
        w.StartZombies = w.Zombies.Count();
        w.SpawnParkedCars(new Sim.Rng(seed ^ 0xCA75));
        // a neighbour's car with keys in it, not far from memere's, so early runs are possible
        var near = w.Vehicles.OrderBy(v => MathF.Abs(v.X - w.Player.X) + MathF.Abs(v.Y - w.Player.Y)).FirstOrDefault();
        if (near != null) { near.HasKeys = true; near.Fuel = Math.Max(near.Fuel, 15); near.Condition = Math.Max(near.Condition, 0.7f); }
        return w;
    }

    static Building? PickHouse(GameWorld w, string? osmId, TilePos near, Building? not, int idealDistance)
    {
        if (osmId != null && w.Buildings.FirstOrDefault(b => b.Id == osmId) is { } chosen) return chosen;
        return w.Buildings
            .Where(b => b != not && b.Kind == "house" && b.Tiles.Count is >= 60 and <= 220)
            .OrderBy(b => Math.Abs(Center(b).DistanceTo(near) - idealDistance))
            .FirstOrDefault();
    }

    static TilePos Center(Building b) => new((b.Bounds.MinX + b.Bounds.MaxX) / 2, (b.Bounds.MinY + b.Bounds.MaxY) / 2);

    /// <summary>Largest rectangle inside the room (memere's safe zone has to be a rectangle).</summary>
    static TileRect InnerRect(BuildingGen.Room r)
    {
        var set = r.Tiles.Select(t => (t.X, t.Y)).ToHashSet();
        TileRect best = new(r.Tiles[0].X, r.Tiles[0].Y, r.Tiles[0].X + 1, r.Tiles[0].Y + 1);
        int bestArea = 1;
        foreach (var t in r.Tiles)
            for (int w = 1; set.Contains((t.X + w - 1, t.Y)); w++)
                for (int h = 1; ; h++)
                {
                    bool ok = true;
                    for (int x = t.X; x < t.X + w && ok; x++) ok = set.Contains((x, t.Y + h - 1));
                    if (!ok) break;
                    if (w * h > bestArea) { bestArea = w * h; best = new TileRect(t.X, t.Y, t.X + w, t.Y + h); }
                }
        return best;
    }

    static void SetUpMemeresHouse(GameWorld w, GameData data, Building home, BuildingGen.Result gen)
    {
        var map = w.Map;
        w.HomeBuilding = home.Index;
        var living = gen.Rooms.FirstOrDefault(r => r.Type is "living" or "living_kitchen") ?? gen.Rooms[0];
        var zone = InnerRect(living);
        w.ProtectedZones.Add(zone);

        // clear the generated TV/couch spots in her room; her chair and TV go in
        foreach (var t in living.Tiles)
        {
            ref var tile = ref map.At(t);
            if (tile.Object is "tv" or "couch" or "lamp") { tile.Object = null; tile.Solid = false; }
        }
        // chair near the back of the room, TV a couple of tiles in front (toward +y if there's room)
        var chair = new TilePos(zone.MinX + zone.Width / 2, zone.MinY + Math.Min(1, zone.Height - 1));
        var tv = new TilePos(chair.X, Math.Min(zone.MaxY - 1, chair.Y + 3));
        if (tv == chair) tv = new TilePos(Math.Min(zone.MaxX - 1, chair.X + 2), chair.Y);
        Place(map, chair, "memere_chair");
        Place(map, tv, "tv");
        var lamp = new TilePos(Math.Max(zone.MinX, chair.X - 1), chair.Y);
        if (lamp != chair && map.At(lamp).Object == null) Place(map, lamp, "lamp");

        BuildingGen.EnsureConnected(w, home, gen, new HashSet<string> { "memere_chair", "tv" });

        var memere = w.Add(new MemereEntity());
        memere.PlaceAt(chair);
        memere.Facing = MathF.Atan2(tv.Y - chair.Y, tv.X - chair.X);
        foreach (var s in data.Supplies.Values) memere.Supplies.Add(s.Id, s.StartAmount);

        // player starts in the kitchen (or anywhere free in the house outside her room)
        var kitchen = gen.Rooms.FirstOrDefault(r => r.Type is "kitchen") ?? gen.Rooms.FirstOrDefault(r => r != living) ?? living;
        var start = kitchen.Tiles.FirstOrDefault(t => !map.At(t).Solid && !w.ProtectedZones.Contains(t));
        if (start == default) start = home.Tiles.First(t => !map.At(t).Solid && !w.ProtectedZones.Contains(t));
        var player = w.Add(new Player());
        player.PlaceAt(start);

        // generator and shed out back (the side away from the front door)
        var front = gen.FrontDoorOutside ?? new TilePos(home.Bounds.MaxX, home.Bounds.MaxY);
        var yard = home.Tiles
            .SelectMany(t => new[] { new TilePos(t.X + 2, t.Y), new TilePos(t.X - 2, t.Y), new TilePos(t.X, t.Y + 2), new TilePos(t.X, t.Y - 2) })
            .Where(p => map.InBounds(p) && map.At(p).Building == 0 && !map.At(p).Solid && map.At(p).Floor is "grass" or "dirt" or "driveway")
            .OrderByDescending(p => p.DistanceTo(front)).ToList();
        if (yard.Count > 0)
        {
            w.GeneratorPos = yard[0];
            Place(map, yard[0], "generator");
            var shed = yard.FirstOrDefault(p => p.ChebyshevTo(yard[0]) is >= 2 and <= 4);
            if (shed != default)
            {
                Place(map, shed, "crate");
                w.Containers["shed"] = new Items.Container { Id = "shed", Kind = "crate", Pos = shed, LootTable = "house_shed", Filled = false };
            }
        }
        w.Power.Generator.Present = true;
        w.Power.Appliances.Add(new Appliance { Id = "tv", Kind = "tv", Watts = 120 });
        w.Power.Appliances.Add(new Appliance { Id = "fridge", Kind = "fridge", Watts = 180 });
        w.Power.Appliances.Add(new Appliance { Id = "lights", Kind = "light", Watts = 200 });
        w.Tv.Channel = data.Tv.Channels.FirstOrDefault()?.Id ?? "";
    }

    static void SetUpDad(GameWorld w, GameData data, Building house, BuildingGen.Result gen)
    {
        w.DadHouse = house.Index;
        var room = gen.Rooms.FirstOrDefault(r => r.Type is "living" or "living_kitchen") ?? gen.Rooms[0];
        var spot = room.Tiles.FirstOrDefault(t => !w.Map.At(t).Solid);
        if (spot == default) return;
        var dad = w.Add(new Dad { Outfit = data.Clothing.Presets.GetValueOrDefault("dad")?.Clone() ?? new Outfit() });
        dad.PlaceAt(spot);
    }

    static void Place(TileMap map, TilePos p, string obj)
    {
        ref var t = ref map.At(p);
        t.Object = obj;
        t.Solid = true;
    }

    static void SpawnZombies(GameWorld w, WorldConfig cfg, Building home)
    {
        var map = w.Map;
        var homeCenter = Center(home);
        int outdoor = 0;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                ref var t = ref map.At(x, y);
                if (t.Solid || t.Building != 0) continue;
                outdoor++;
            }
        int target = (int)(outdoor / 1000f * cfg.OutdoorZombiesPer1000);
        for (int i = 0, tries = 0; i < target && tries < target * 20; tries++)
        {
            var p = new TilePos(w.Rng.Range(0, map.Width), w.Rng.Range(0, map.Height));
            ref var t = ref map.At(p);
            if (t.Solid || t.Building != 0 || p.DistanceTo(homeCenter) < cfg.SafeRadius) continue;
            if (w.TrySpawnZombie(p) != null) i++;
        }
        foreach (var b in w.Buildings)
        {
            if (b == home || Center(b).DistanceTo(homeCenter) < cfg.SafeRadius) continue;
            if (!w.Rng.Chance(cfg.BuildingZombieChance)) continue;
            int n = w.Rng.Range(1, 4);
            for (int k = 0; k < n; k++)
            {
                var p = b.Tiles[w.Rng.Range(0, b.Tiles.Count)];
                if (!map.At(p).Solid) w.TrySpawnZombie(p);
            }
        }
    }

    public static WorldConfig LoadConfig(IDataSource src) =>
        src.Exists("data/world.json")
            ? JsonSerializer.Deserialize<WorldConfig>(src.ReadText("data/world.json"), GameData.Json) ?? new()
            : new();
}
