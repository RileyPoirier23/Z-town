namespace ZTown.Core.Map;

/// <summary>A* over the tile grid, 8-way. Diagonal steps need both orthogonal routes open
/// (no cutting wall corners).</summary>
public static class Pathfinder
{
    static readonly (int dx, int dy)[] Dirs =
    {
        (0, -1), (1, 0), (0, 1), (-1, 0), (1, -1), (1, 1), (-1, 1), (-1, -1),
    };

    /// <param name="canEnter">Extra rule for which tiles the walker may stand on (e.g. zombies
    /// can never enter memere's protected rooms).</param>
    /// <param name="throughBreakables">plan as if closed doors, windows and barricades could be broken through (zombies)</param>
    public static List<TilePos>? Find(TileMap map, TilePos from, TilePos to, Func<TilePos, bool> canEnter, int maxNodes = 4000, bool throughBreakables = false)
    {
        if (from == to) return new List<TilePos>();
        if (!map.InBounds(to) || !canEnter(to) || map.At(to).Solid) return null;
        if (from.Z != to.Z) return null; // stairs come later

        var open = new PriorityQueue<TilePos, float>();
        var came = new Dictionary<TilePos, TilePos>();
        var g = new Dictionary<TilePos, float> { [from] = 0 };
        open.Enqueue(from, 0);
        int expanded = 0;

        while (open.Count > 0)
        {
            var cur = open.Dequeue();
            if (cur == to) return Rebuild(came, from, to);
            if (++expanded > maxNodes) return null;
            float gc = g[cur];
            foreach (var (dx, dy) in Dirs)
            {
                var next = new TilePos(cur.X + dx, cur.Y + dy, cur.Z);
                if (!CanStep(map, cur, next, canEnter, throughBreakables)) continue;
                float cost = gc + (dx != 0 && dy != 0 ? 1.4142f : 1f);
                if (g.TryGetValue(next, out var old) && old <= cost) continue;
                g[next] = cost;
                came[next] = cur;
                float h = MathF.Sqrt((to.X - next.X) * (to.X - next.X) + (to.Y - next.Y) * (to.Y - next.Y));
                open.Enqueue(next, cost + h);
            }
        }
        return null;
    }

    public static bool CanStep(TileMap map, TilePos a, TilePos b, Func<TilePos, bool> canEnter, bool throughBreakables = false)
    {
        if (!map.InBounds(b) || map.At(b).Solid || !canEnter(b)) return false;
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (Math.Abs(dx) > 1 || Math.Abs(dy) > 1 || (dx == 0 && dy == 0)) return false;
        if (dx == 0 || dy == 0) return EdgeOk(map, a, b, throughBreakables);
        // diagonals never go through doors (no corner-cutting through a doorway)
        var viaX = new TilePos(a.X + dx, a.Y, a.Z);
        var viaY = new TilePos(a.X, a.Y + dy, a.Z);
        return Ortho(map, a, viaX, canEnter) && Ortho(map, viaX, b, canEnter)
            && Ortho(map, a, viaY, canEnter) && Ortho(map, viaY, b, canEnter);
    }

    static bool EdgeOk(TileMap map, TilePos a, TilePos b, bool throughBreakables) =>
        map.Passable(a, b) || throughBreakables && map.Breakable(a, b);

    static bool Ortho(TileMap map, TilePos a, TilePos b, Func<TilePos, bool> canEnter) =>
        map.InBounds(b) && !map.At(b).Solid && canEnter(b) && map.Passable(a, b);

    static List<TilePos> Rebuild(Dictionary<TilePos, TilePos> came, TilePos from, TilePos to)
    {
        var path = new List<TilePos>();
        for (var p = to; p != from; p = came[p]) path.Add(p);
        path.Reverse();
        return path;
    }
}
