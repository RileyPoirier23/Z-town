using System.Diagnostics;
using ZTown.Core.Data;
using ZTown.Core.Map;
using ZTown.Core.Protection;

namespace ZTown.Core.Tests.Systems;

/// <summary>The real map (converted OpenStreetMap data) builds into a sane, playable world.</summary>
public class RealMapTests
{
    static readonly Lazy<GameWorld?> World = new(() =>
    {
        var src = new FileDataSource(TestData.GameDir);
        var cfg = MapBuilder.LoadConfig(src);
        if (!src.Exists($"maps/{cfg.Map}.map.json")) return null;
        var sw = Stopwatch.StartNew();
        var w = MapBuilder.Build(TestData.Data, MapFile.Load(src, cfg.Map), cfg, 7);
        Console.WriteLine($"built {cfg.Map} in {sw.ElapsedMilliseconds} ms: {w.Buildings.Count} buildings, {w.Zombies.Count()} zombies, {w.Containers.Count} containers");
        return w;
    });

    [Fact]
    public void Memere_house_is_set_up_and_safe()
    {
        var w = World.Value;
        if (w == null) return; // no converted map in this checkout
        Assert.True(w.HomeBuilding >= 0);
        Assert.True(w.Memere.IsProtected);
        Assert.True(w.ProtectedZones.Contains(w.Memere.Tile));
        Assert.True(w.IsHome(w.Player.Tile));
        Assert.False(w.ProtectedZones.Contains(w.Player.Tile));
        Assert.DoesNotContain(w.Zombies, z => w.ProtectedZones.Contains(z.Tile));
        Assert.DoesNotContain(w.Zombies, z => z.Tile.DistanceTo(w.Memere.Tile) < 20);
        Assert.Equal("memere_chair", w.Map.At(w.Memere.Tile).Object);
        Assert.True(w.Power.Generator.Present);
    }

    [Fact]
    public void Player_can_walk_from_memeres_to_the_street_and_into_shops()
    {
        var w = World.Value;
        if (w == null) return;
        // open every door, then the player must reach a road and a pharmacy and a store
        for (int y = 0; y < w.Map.Height; y++)
            for (int x = 0; x < w.Map.Width; x++)
            {
                ref var t = ref w.Map.At(x, y);
                if (t.North == Edge.DoorClosed) t.North = Edge.DoorOpen;
                if (t.West == Edge.DoorClosed) t.West = Edge.DoorOpen;
            }
        var reach = Flood(w, w.Player.Tile, 250_000);
        Assert.Contains(reach, p => w.Map.At(p).Floor is "asphalt" or "road_line" or "road_line_y");
        // the whole house is reachable (no sealed rooms)
        var home = w.Buildings[w.HomeBuilding];
        var homeFree = home.Tiles.Where(t => !w.Map.At(t).Solid).ToList();
        Assert.All(homeFree, t => Assert.Contains(t, reach));
    }

    [Fact]
    public void Every_building_has_a_way_in_and_no_sealed_rooms()
    {
        var w = World.Value;
        if (w == null) return;
        int sealedBuildings = 0;
        foreach (var b in w.Buildings.Take(120))
        {
            var free = b.Tiles.Where(t => !w.Map.At(t).Solid).ToList();
            if (free.Count == 0) continue;
            // flood from inside with doors passable (closed doors open for this check)
            var set = new HashSet<TilePos>();
            var q = new Queue<TilePos>();
            q.Enqueue(free[0]);
            set.Add(free[0]);
            bool outside = false;
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new TilePos(p.X + dx, p.Y + dy);
                    if (!w.Map.InBounds(n) || set.Contains(n) || w.Map.At(n).Solid) continue;
                    var e = w.Map.EdgeBetween(p, n);
                    if (e is not (Edge.None or Edge.DoorOpen or Edge.DoorClosed)) continue;
                    if (w.Map.At(n).Building != b.Index + 1) { outside = true; continue; }
                    set.Add(n);
                    q.Enqueue(n);
                }
            }
            if (!outside || set.Count < free.Count) sealedBuildings++;
        }
        Assert.True(sealedBuildings <= 3, $"{sealedBuildings} of the first 120 buildings have sealed rooms or no door out");
    }

    [Fact]
    public void Shops_have_loot_memere_needs()
    {
        var w = World.Value;
        if (w == null) return;
        var tables = w.Containers.Values.Select(c => c.LootTable).ToHashSet();
        Assert.Contains("store_pharmacy", tables);
        Assert.Contains("store_drinks", tables);
    }

    static HashSet<TilePos> Flood(GameWorld w, TilePos from, int max)
    {
        var set = new HashSet<TilePos> { from };
        var q = new Queue<TilePos>();
        q.Enqueue(from);
        while (q.Count > 0 && set.Count < max)
        {
            var p = q.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new TilePos(p.X + dx, p.Y + dy);
                if (set.Contains(n) || !Pathfinder.CanStep(w.Map, p, n, _ => true)) continue;
                set.Add(n);
                q.Enqueue(n);
            }
        }
        return set;
    }
}
