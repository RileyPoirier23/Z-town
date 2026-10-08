using ZTown.Core.Items;
using ZTown.Core.Sim;

namespace ZTown.Core.Map;

/// <summary>
/// Generates a building's inside from its footprint: outer walls, rooms (split like a floor
/// plan), doors that connect everything, a front door facing the street, windows, floors,
/// furniture and lootable containers by room and building type. Deterministic per building
/// id, so the same house always has the same layout.
/// </summary>
public static class BuildingGen
{
    public sealed class Room
    {
        public string Type = "room";
        public TileRect Rect;
        public List<TilePos> Tiles = new();
        public int Id;
    }

    public sealed class Result
    {
        public List<Room> Rooms = new();
        public TilePos? FrontDoorInside;
        public TilePos? FrontDoorOutside;
    }

    static readonly HashSet<string> Homes = new() { "house", "apartments" };
    static readonly string[] Sidings = { "siding_white", "siding_white", "siding_blue", "siding_yellow" };

    public static Result Generate(GameWorld w, Building b, ref int nextRoomId)
    {
        var map = w.Map;
        var rng = new Rng((ulong)StableHash(b.Id));
        var set = b.Tiles.Select(t => (t.X, t.Y)).ToHashSet();
        bool In(int x, int y) => set.Contains((x, y));
        var res = new Result();
        bool home = Homes.Contains(b.Kind);
        b.Exterior = home ? Sidings[rng.Range(0, Sidings.Length)] : b.Kind is "garage_shed" or "warehouse" ? "siding_blue" : "brick_red";
        b.Roof = home ? (rng.Chance(0.5) ? "shingle_grey" : "shingle_brown") : "flat_tar";
        b.Pitch = home ? 0.5f : 0f;

        // ---- outer walls
        foreach (var (x, y) in set)
        {
            ref var t = ref map.At(x, y);
            t.WallStyle = b.Exterior;
            t.Solid = false;
            t.Object = null;
            static Edge Outer(Edge e) => e is Edge.None ? Edge.Wall : e; // keep a neighbour's door/window
            if (!In(x, y - 1)) t.North = Outer(t.North);
            if (!In(x - 1, y)) t.West = Outer(t.West);
            if (!In(x, y + 1) && map.InBounds(new TilePos(x, y + 1))) map.At(x, y + 1).North = Outer(map.At(x, y + 1).North);
            if (!In(x + 1, y) && map.InBounds(new TilePos(x + 1, y))) map.At(x + 1, y).West = Outer(map.At(x + 1, y).West);
        }

        // ---- rooms: split the bounding box like a floor plan
        var leaves = new List<TileRect>();
        int maxRoom = home ? 7 : b.Tiles.Count > 400 ? 18 : 30;
        Split(b.Bounds, leaves, rng, maxRoom, home ? 3 : 4, depth: 0, maxDepth: home ? 4 : 1);
        foreach (var leaf in leaves)
        {
            var room = new Room { Rect = leaf, Id = ++nextRoomId };
            for (int y = leaf.MinY; y < leaf.MaxY; y++)
                for (int x = leaf.MinX; x < leaf.MaxX; x++)
                    if (In(x, y)) room.Tiles.Add(new TilePos(x, y));
            if (room.Tiles.Count == 0) continue;
            res.Rooms.Add(room);
        }
        // tiny scraps join their neighbour (no wall between)
        var roomOf = new Dictionary<(int, int), Room>();
        foreach (var r in res.Rooms) foreach (var t in r.Tiles) roomOf[(t.X, t.Y)] = r;
        foreach (var r in res.Rooms.Where(r => r.Tiles.Count < 6).ToList())
        {
            var nb = r.Tiles.SelectMany(t => new[] { (t.X + 1, t.Y), (t.X - 1, t.Y), (t.X, t.Y + 1), (t.X, t.Y - 1) })
                .Where(n => roomOf.TryGetValue(n, out var o) && o != r).Select(n => roomOf[n]).FirstOrDefault();
            if (nb == null) continue;
            nb.Tiles.AddRange(r.Tiles);
            foreach (var t in r.Tiles) roomOf[(t.X, t.Y)] = nb;
            res.Rooms.Remove(r);
        }

        // ---- room types
        var bySize = res.Rooms.OrderByDescending(r => r.Tiles.Count).ToList();
        if (home)
        {
            for (int i = 0; i < bySize.Count; i++)
                bySize[i].Type = i == 0 ? "living" : i == 1 ? "kitchen" : i == bySize.Count - 1 && bySize.Count >= 4 ? "bathroom" : "bedroom";
            if (bySize.Count == 1) bySize[0].Type = "living_kitchen";
        }
        else
        {
            bySize[0].Type = "main";
            foreach (var r in bySize.Skip(1)) r.Type = "back";
        }

        // ---- inner walls between different rooms, with one door per shared stretch
        var doorsBetween = new HashSet<(int, int)>();
        foreach (var (x, y) in set)
        {
            var r = roomOf[(x, y)];
            if (In(x, y - 1) && roomOf[(x, y - 1)] != r) map.At(x, y).North = Edge.Wall;
            if (In(x - 1, y) && roomOf[(x - 1, y)] != r) map.At(x, y).West = Edge.Wall;
        }
        foreach (var a in res.Rooms)
            foreach (var c in res.Rooms)
            {
                if (a.Id >= c.Id) continue;
                // candidate edges between a and c
                var edges = new List<(TilePos from, TilePos to)>();
                foreach (var t in a.Tiles)
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (roomOf.TryGetValue((t.X + dx, t.Y + dy), out var o) && o == c)
                            edges.Add((t, new TilePos(t.X + dx, t.Y + dy)));
                if (edges.Count == 0) continue;
                // shops: back rooms get one door; homes: every adjoining pair (keeps it all connected)
                var pick = edges[edges.Count / 2];
                map.SetEdgeBetween(pick.from, pick.to, Edge.DoorOpen);
                doorsBetween.Add((pick.from.X, pick.from.Y));
                doorsBetween.Add((pick.to.X, pick.to.Y));
            }

        // ---- floors
        foreach (var r in res.Rooms)
        {
            string floor = r.Type switch
            {
                "kitchen" or "bathroom" => "linoleum",
                "living" or "living_kitchen" => rng.Chance(0.5) ? "hardwood" : "carpet",
                "bedroom" => "carpet",
                "main" => b.Kind is "warehouse" or "garage_shed" or "garage" ? "driveway" : "tile_store",
                _ => b.Kind is "warehouse" or "garage_shed" ? "driveway" : "linoleum",
            };
            foreach (var t in r.Tiles)
            {
                ref var tile = ref map.At(t);
                tile.Floor = floor;
                tile.Room = r.Id;
            }
        }

        // ---- front door: the outside wall that faces a street
        var boundary = new List<(TilePos inside, TilePos outside)>();
        foreach (var (x, y) in set)
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                if (!In(x + dx, y + dy) && map.InBounds(new TilePos(x + dx, y + dy)))
                    boundary.Add((new TilePos(x, y), new TilePos(x + dx, y + dy)));
        (TilePos inside, TilePos outside) best = default;
        float bestScore = float.MaxValue;
        foreach (var e in boundary)
        {
            ref var ot = ref map.At(e.outside);
            if (ot.Solid || ot.Building != 0) continue; // must open onto open ground
            int dx = e.outside.X - e.inside.X, dy = e.outside.Y - e.inside.Y;
            float score = 40;
            for (int k = 1; k <= 40; k++)
            {
                var p = new TilePos(e.inside.X + dx * k, e.inside.Y + dy * k);
                if (!map.InBounds(p) || In(p.X, p.Y) && k > 1) break;
                var f = map.At(p).Floor;
                if (f is "sidewalk" or "asphalt" or "road_line" or "road_line_y" or "parking") { score = k; break; }
            }
            // prefer the middle of a wall, not corners
            int sideNeighbours = (In(e.inside.X + dy, e.inside.Y + dx) ? 1 : 0) + (In(e.inside.X - dy, e.inside.Y - dx) ? 1 : 0);
            score += sideNeighbours == 2 ? 0 : 3;
            score += (float)rng.NextDouble() * 0.5f;
            if (score < bestScore) { bestScore = score; best = e; }
        }
        if (bestScore == float.MaxValue && boundary.Count > 0)
        {
            // no open ground next to it: clear a tree/prop outside, or join onto the neighbouring building
            var e = boundary.Where(e => !map.At(e.outside).Solid || map.At(e.outside).Building == 0 && map.At(e.outside).Floor is not ("water" or "void"))
                .OrderBy(e => map.At(e.outside).Building != 0 ? 1 : 0).FirstOrDefault();
            if (e != default)
            {
                ref var ot = ref map.At(e.outside);
                if (ot.Building == 0) { ot.Solid = false; ot.Object = null; }
                best = e;
                bestScore = 99;
            }
        }
        if (bestScore < float.MaxValue)
        {
            map.SetEdgeBetween(best.inside, best.outside, Edge.DoorClosed);
            res.FrontDoorInside = best.inside;
            res.FrontDoorOutside = best.outside;
            // a little path to the door
            if (map.At(best.outside).Floor == "grass") map.At(best.outside).Floor = "sidewalk";
        }
        // houses usually have a back door too
        if (home && boundary.Count > 8)
        {
            var back = boundary.Where(e => !map.At(e.outside).Solid && map.At(e.outside).Building == 0 && (e.outside.X - e.inside.X) == -(best.outside.X - best.inside.X) && (e.outside.Y - e.inside.Y) == -(best.outside.Y - best.inside.Y)).ToList();
            if (back.Count > 2 && rng.Chance(0.7)) { var e = back[back.Count / 2]; map.SetEdgeBetween(e.inside, e.outside, Edge.DoorClosed); }
        }

        // ---- windows along outside walls (not right next to doors)
        int every = home ? 3 : b.Kind is "warehouse" or "garage_shed" ? 7 : 2;
        int i2 = 0;
        foreach (var e in boundary)
        {
            if (map.EdgeBetween(e.inside, e.outside) != Edge.Wall) continue;
            if (++i2 % every != 0) continue;
            bool nearDoor = boundary.Any(o => map.EdgeBetween(o.inside, o.outside) == Edge.DoorClosed && o.inside.ChebyshevTo(e.inside) <= 1);
            if (!nearDoor) map.SetEdgeBetween(e.inside, e.outside, Edge.WindowClosed);
        }

        // ---- furniture and containers
        var keepClear = new HashSet<(int, int)>(doorsBetween);
        if (res.FrontDoorInside is { } fd) keepClear.Add((fd.X, fd.Y));
        foreach (var e in boundary.Where(e => map.EdgeBetween(e.inside, e.outside) is Edge.DoorClosed)) keepClear.Add((e.inside.X, e.inside.Y));
        int cn = 0;
        foreach (var r in res.Rooms) Furnish(w, b, r, set, keepClear, rng, ref cn);
        EnsureConnected(w, b, res);
        return res;
    }

    /// <summary>
    /// Makes sure every free tile in the building can be reached from the front door, by taking
    /// out furniture that walls something off. Objects in <paramref name="keep"/> stay put.
    /// </summary>
    public static void EnsureConnected(GameWorld w, Building b, Result res, ISet<string>? keep = null)
    {
        var map = w.Map;
        int bi = b.Index + 1;
        for (int guard = 0; guard < 60; guard++)
        {
            var start = res.FrontDoorInside ?? b.Tiles.FirstOrDefault(t => !map.At(t).Solid);
            if (map.At(start).Solid) return;
            var reach = new HashSet<TilePos> { start };
            var q = new Queue<TilePos>();
            q.Enqueue(start);
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                foreach (var n in Neighbours(p))
                {
                    if (reach.Contains(n) || !map.InBounds(n) || map.At(n).Building != bi || map.At(n).Solid) continue;
                    if (map.EdgeBetween(p, n) is not (Edge.None or Edge.DoorOpen or Edge.DoorClosed)) continue;
                    reach.Add(n);
                    q.Enqueue(n);
                }
            }
            if (b.Tiles.All(t => map.At(t).Solid || reach.Contains(t))) return;
            // remove a piece of furniture next to the reachable area that's in the way
            TilePos? remove = null;
            foreach (var t in b.Tiles)
            {
                ref var tt = ref map.At(t);
                if (!tt.Solid || tt.Object == null || keep?.Contains(tt.Object) == true) continue;
                bool touchesReach = Neighbours(t).Any(n => reach.Contains(n) && map.EdgeBetween(n, t) is Edge.None or Edge.DoorOpen or Edge.DoorClosed);
                bool touchesOther = Neighbours(t).Any(n => map.InBounds(n) && map.At(n).Building == bi && !reach.Contains(n) && !map.At(n).Solid
                                                          && map.EdgeBetween(n, t) is Edge.None or Edge.DoorOpen or Edge.DoorClosed);
                if (touchesReach && touchesOther) { remove = t; break; }
                if (touchesReach && remove == null) remove = t;
            }
            if (remove is not { } r) return;
            ref var rt = ref map.At(r);
            rt.Object = null;
            rt.Solid = false;
            foreach (var c in w.Containers.Where(c => c.Value.Pos == r).Select(c => c.Key).ToList()) w.Containers.Remove(c);
        }
    }

    static IEnumerable<TilePos> Neighbours(TilePos p)
    {
        yield return new TilePos(p.X + 1, p.Y, p.Z);
        yield return new TilePos(p.X - 1, p.Y, p.Z);
        yield return new TilePos(p.X, p.Y + 1, p.Z);
        yield return new TilePos(p.X, p.Y - 1, p.Z);
    }

    static void Split(TileRect r, List<TileRect> leaves, Rng rng, int maxSide, int minSide, int depth, int maxDepth)
    {
        bool canX = r.Width >= minSide * 2 + 1, canY = r.Height >= minSide * 2 + 1;
        bool want = depth < maxDepth && (r.Width > maxSide || r.Height > maxSide || r.Width * r.Height > maxSide * maxSide * 0.8);
        if (!want || !(canX || canY)) { leaves.Add(r); return; }
        bool alongX = canX && (!canY || r.Width >= r.Height);
        if (alongX)
        {
            int cut = rng.Range(r.MinX + minSide, r.MaxX - minSide + 1);
            Split(new TileRect(r.MinX, r.MinY, cut, r.MaxY, r.Z), leaves, rng, maxSide, minSide, depth + 1, maxDepth);
            Split(new TileRect(cut, r.MinY, r.MaxX, r.MaxY, r.Z), leaves, rng, maxSide, minSide, depth + 1, maxDepth);
        }
        else
        {
            int cut = rng.Range(r.MinY + minSide, r.MaxY - minSide + 1);
            Split(new TileRect(r.MinX, r.MinY, r.MaxX, cut, r.Z), leaves, rng, maxSide, minSide, depth + 1, maxDepth);
            Split(new TileRect(r.MinX, cut, r.MaxX, r.MaxY, r.Z), leaves, rng, maxSide, minSide, depth + 1, maxDepth);
        }
    }

    /// <summary>What goes in each kind of room: (object, loot table or null, refrigerated).</summary>
    static List<(string obj, string? loot, bool cold)> Kit(string buildingKind, string roomType, Rng rng)
    {
        var k = new List<(string, string?, bool)>();
        switch (roomType)
        {
            case "kitchen":
            case "living_kitchen":
                k.Add(("fridge", "house_fridge", true));
                k.Add(("stove", null, false));
                k.Add(("cupboard", "house_kitchen", false));
                k.Add(("kitchen_counter", "house_kitchen", false));
                k.Add(("kitchen_counter", null, false));
                if (roomType == "living_kitchen") { k.Add(("couch", null, false)); k.Add(("tv", null, false)); }
                break;
            case "living":
                k.Add(("couch", null, false));
                k.Add(("tv", null, false));
                k.Add(("lamp", null, false));
                if (rng.Chance(0.5)) k.Add(("shelf_home", "house_living", false));
                break;
            case "bedroom":
                k.Add(("bed", null, false));
                k.Add(("dresser", "bedroom", false));
                if (rng.Chance(0.5)) k.Add(("lamp", null, false));
                break;
            case "bathroom":
                k.Add(("toilet", null, false));
                k.Add(("medicine_cabinet", "bathroom", false));
                break;
            case "main":
                switch (buildingKind)
                {
                    case "convenience" or "gas_station":
                        k.Add(("counter", "store_counter", false));
                        for (int i = 0; i < 3; i++) k.Add(("cooler", "store_drinks", true));
                        for (int i = 0; i < 4; i++) k.Add(("shelf", "store_snacks", false));
                        break;
                    case "pharmacy" or "clinic" or "hospital":
                        k.Add(("counter", "pharmacy_counter", false));
                        for (int i = 0; i < 6; i++) k.Add(("shelf", "store_pharmacy", false));
                        k.Add(("cooler", "store_drinks", true));
                        break;
                    case "grocery" or "bigbox":
                        k.Add(("counter", "store_counter", false));
                        for (int i = 0; i < 6; i++) k.Add(("cooler", "store_drinks", true));
                        for (int i = 0; i < 10; i++) k.Add(("shelf", "store_snacks", false));
                        break;
                    case "restaurant" or "bar":
                        k.Add(("counter", "store_counter", false));
                        k.Add(("fridge", "restaurant_fridge", true));
                        k.Add(("stove", null, false));
                        for (int i = 0; i < 4; i++) k.Add(("table", null, false));
                        break;
                    case "hardware" or "garage" or "garage_shed" or "warehouse":
                        for (int i = 0; i < 5; i++) k.Add(("crate", "hardware", false));
                        k.Add(("shelf", "hardware", false));
                        break;
                    default:
                        for (int i = 0; i < 4; i++) k.Add(("desk", "office", false));
                        k.Add(("shelf", "office", false));
                        break;
                }
                break;
            default:
                k.Add(("crate", buildingKind is "convenience" or "grocery" or "gas_station" ? "store_snacks" : "office", false));
                break;
        }
        return k;
    }

    static void Furnish(GameWorld w, Building b, Room r, HashSet<(int X, int Y)> set, HashSet<(int, int)> keepClear, Rng rng, ref int cn)
    {
        var map = w.Map;
        // spots along the walls first, then elsewhere in big open rooms
        var wallSpots = r.Tiles.Where(t =>
            !keepClear.Contains((t.X, t.Y)) &&
            (map.At(t).North != Edge.None || map.At(t).West != Edge.None ||
             (map.InBounds(new TilePos(t.X, t.Y + 1)) && map.At(t.X, t.Y + 1).North != Edge.None) ||
             (map.InBounds(new TilePos(t.X + 1, t.Y)) && map.At(t.X + 1, t.Y).West != Edge.None))).ToList();
        // never block the way through: skip spots next to any door
        wallSpots = wallSpots.Where(t => !NearDoor(map, t)).ToList();
        Shuffle(wallSpots, rng);
        var open = r.Tiles.Where(t => !keepClear.Contains((t.X, t.Y)) && !NearDoor(map, t) && !wallSpots.Contains(t)).ToList();
        Shuffle(open, rng);
        foreach (var (obj, loot, cold) in Kit(b.Kind, r.Type, rng))
        {
            bool inMiddle = obj is "table" || (obj is "shelf" && r.Type == "main" && open.Count > 12);
            var pool = inMiddle ? open : wallSpots;
            if (pool.Count == 0) pool = wallSpots.Count > 0 ? wallSpots : open;
            if (pool.Count == 0) break;
            var t = pool[^1];
            pool.RemoveAt(pool.Count - 1);
            // keep a free tile around middle furniture so rooms stay walkable
            if (inMiddle) open.RemoveAll(o => o.ChebyshevTo(t) <= 1);
            ref var tile = ref map.At(t);
            tile.Object = obj;
            tile.Solid = obj is not ("rug");
            if (loot != null)
            {
                var id = $"{b.Id}_{cn++}";
                w.Containers[id] = new Container { Id = id, Kind = obj, Pos = t, LootTable = loot, Refrigerated = cold };
            }
        }
        if (r.Type is "living" or "bedroom" && rng.Chance(0.6))
        {
            var spot = open.FirstOrDefault();
            if (spot != default && map.At(spot).Object == null) map.At(spot).Object = "rug";
        }
    }

    static bool NearDoor(TileMap map, TilePos t)
    {
        foreach (var (dx, dy) in new[] { (0, 0), (1, 0), (-1, 0), (0, 1), (0, -1) })
        {
            var p = new TilePos(t.X + dx, t.Y + dy);
            if (!map.InBounds(p)) continue;
            var tt = map.At(p);
            if (tt.North is Edge.DoorClosed or Edge.DoorOpen || tt.West is Edge.DoorClosed or Edge.DoorOpen) return true;
        }
        return false;
    }

    static void Shuffle<T>(List<T> list, Rng rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    public static int StableHash(string s)
    {
        unchecked
        {
            int h = 23;
            foreach (char c in s) h = h * 31 + c;
            return h & 0x7fffffff;
        }
    }
}
