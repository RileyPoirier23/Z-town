using ZTown.Core.Items;
using ZTown.Core.Entities;

namespace ZTown.Core.Map;

/// <summary>
/// A small hand-made stand-in for memere's house and a corner store, used by tests and the
/// Phase 0 debug scene. Phase 1 replaces it with the real house (from Riley's notes) and the
/// real block (from OpenStreetMap).
/// </summary>
public static class TestMaps
{
    public const string MemereRoomId = "memere_room";

    /// <summary>Builds the test map and populates the world: player, memere, generator, containers.</summary>
    public static GameWorld CreateTestWorld(Data.GameData data, ulong seed, int zombies = 0)
    {
        var map = new TileMap(48, 40);
        foreach (var p in map.AllTiles()) map.At(p).Floor = "grass";

        // street
        for (int x = 0; x < map.Width; x++)
            for (int y = 20; y < 24; y++) map.At(x, y).Floor = "asphalt";

        // memere's house: 4..16 x 4..14, living room with her chair on the west, kitchen east
        var house = new TileRect(4, 4, 16, 14);
        Room(map, house, "carpet", 1, "siding_white");
        // inner wall between her room (4..9) and the kitchen/hall (9..16), with a door
        for (int y = 4; y < 14; y++) map.At(9, y).West = Edge.Wall;
        map.At(9, 8).West = Edge.DoorClosed;
        for (int y = 4; y < 14; y++) for (int x = 9; x < 16; x++) { map.At(x, y).Floor = "linoleum"; map.At(x, y).Room = 2; }
        // front door to the street side, windows
        map.At(12, 14).North = Edge.DoorClosed;
        map.At(6, 4).North = Edge.WindowClosed;
        map.At(4, 10).West = Edge.WindowClosed;
        map.At(13, 4).North = Edge.WindowClosed;
        map.At(16, 8).West = Edge.WindowClosed;

        // corner store across the street: 26..38 x 26..36
        var store = new TileRect(26, 26, 38, 36);
        Room(map, store, "tile_store", 3, "brick_red");
        map.At(31, 26).North = Edge.DoorClosed;
        map.At(29, 26).North = Edge.WindowClosed;
        map.At(34, 26).North = Edge.WindowClosed;

        var w = new GameWorld(data, map, seed);
        w.HomeArea.Add(house);
        // memere's room is the safe room: zombies can never path into it
        w.ProtectedZones.Add(new TileRect(4, 4, 9, 14));

        var memere = w.Add(new MemereEntity());
        memere.PlaceAt(new TilePos(6, 9));
        map.At(6, 9).Object = "memere_chair";
        map.At(6, 7).Object = "tv";
        foreach (var s in data.Supplies.Values) memere.Supplies.Add(s.Id, s.StartAmount);

        var player = w.Add(new Player());
        player.PlaceAt(new TilePos(12, 10));

        // generator in the back yard, unconnected and empty
        w.GeneratorPos = new TilePos(12, 2);
        map.At(12, 2).Object = "generator";
        w.Power.Generator.Present = true;
        w.Power.Appliances.Add(new Power.Appliance { Id = "tv", Kind = "tv", Watts = 120 });
        w.Power.Appliances.Add(new Power.Appliance { Id = "fridge", Kind = "fridge", Watts = 180 });
        w.Power.Appliances.Add(new Power.Appliance { Id = "lights", Kind = "light", Watts = 200 });
        w.Tv.Channel = data.Tv.Channels.FirstOrDefault()?.Id ?? "";

        AddContainer(w, "house_fridge", "fridge", new TilePos(15, 5), "house_fridge", refrigerated: true);
        AddContainer(w, "house_cupboard", "cupboard", new TilePos(14, 5), "house_kitchen");
        AddContainer(w, "store_drinks", "cooler", new TilePos(27, 30), "store_drinks", refrigerated: true);
        AddContainer(w, "store_shelf_1", "shelf", new TilePos(30, 30), "store_snacks");
        AddContainer(w, "store_counter", "counter", new TilePos(33, 28), "store_counter");
        AddContainer(w, "store_pharmacy", "shelf", new TilePos(36, 33), "store_pharmacy");
        AddContainer(w, "shed", "crate", new TilePos(14, 2), "house_shed");

        for (int i = 0; i < zombies; i++)
            w.TrySpawnZombie(new TilePos(w.Rng.Range(18, 46), w.Rng.Range(16, 39)));
        return w;
    }

    static void AddContainer(GameWorld w, string id, string kind, TilePos p, string loot, bool refrigerated = false)
    {
        w.Containers[id] = new Container { Id = id, Kind = kind, Pos = p, LootTable = loot, Refrigerated = refrigerated };
        w.Map.At(p).Object = kind;
        w.Map.At(p).Solid = true;
    }

    /// <summary>Floor + outer walls for a rectangular building.</summary>
    static void Room(TileMap map, TileRect r, string floor, int roomId, string wallStyle)
    {
        for (int y = r.MinY; y < r.MaxY; y++)
            for (int x = r.MinX; x < r.MaxX; x++)
            {
                ref var t = ref map.At(x, y);
                t.Floor = floor;
                t.Room = roomId;
                t.WallStyle = wallStyle;
            }
        for (int x = r.MinX; x < r.MaxX; x++)
        {
            map.At(x, r.MinY).North = Edge.Wall;
            if (r.MaxY < map.Height) map.At(x, r.MaxY).North = Edge.Wall;
        }
        for (int y = r.MinY; y < r.MaxY; y++)
        {
            map.At(r.MinX, y).West = Edge.Wall;
            if (r.MaxX < map.Width) map.At(r.MaxX, y).West = Edge.Wall;
        }
    }
}
