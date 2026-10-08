namespace ZTown.Core.Map;

/// <summary>What sits on a tile's north or west edge. A wall between two tiles belongs to
/// exactly one of them, like Project Zomboid: tile (x,y)'s North edge is the line between
/// (x,y-1) and (x,y); its West edge is between (x-1,y) and (x,y).</summary>
public enum Edge : byte
{
    None,
    Wall,
    DoorClosed,
    DoorOpen,
    DoorBroken,
    WindowClosed,
    WindowOpen,
    WindowBroken,
}

public static class EdgeExt
{
    public static bool IsPassable(this Edge e) => e is Edge.None or Edge.DoorOpen or Edge.DoorBroken;

    /// <summary>Open or smashed windows can be climbed through (slowly).</summary>
    public static bool IsClimbable(this Edge e) => e is Edge.WindowOpen or Edge.WindowBroken;

    /// <summary>Things a zombie bangs on until they break.</summary>
    public static bool IsBreakable(this Edge e) => e is Edge.DoorClosed or Edge.WindowClosed;

    public static bool BlocksSight(this Edge e) => e is Edge.Wall or Edge.DoorClosed;
}

public struct Tile
{
    public string? Floor;
    public Edge North;
    public Edge West;
    /// <summary>Furniture / object id on this tile (data/tiles), if any.</summary>
    public string? Object;
    public bool Solid;
    /// <summary>Room id, 0 = outdoors.</summary>
    public int Room;
    /// <summary>Sprite id for the wall on the north/west edge, for rendering.</summary>
    public string? WallStyle;
    /// <summary>1-based index of the building this tile belongs to (0 = outdoors).</summary>
    public int Building;
    /// <summary>Planks nailed across the north / west edge (0-4).</summary>
    public byte BarN, BarW;
}

/// <summary>The tile grid: Width x Height on each floor from MinZ to MaxZ.</summary>
public sealed class TileMap
{
    public int Width { get; }
    public int Height { get; }
    public int MinZ { get; }
    public int MaxZ { get; }
    readonly Tile[][] _levels;

    public TileMap(int width, int height, int minZ = 0, int maxZ = 0)
    {
        Width = width;
        Height = height;
        MinZ = minZ;
        MaxZ = maxZ;
        _levels = new Tile[maxZ - minZ + 1][];
        for (int i = 0; i < _levels.Length; i++) _levels[i] = new Tile[width * height];
    }

    public bool InBounds(TilePos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height && p.Z >= MinZ && p.Z <= MaxZ;

    public ref Tile At(TilePos p)
    {
        if (!InBounds(p)) throw new ArgumentOutOfRangeException(nameof(p), p.ToString());
        return ref _levels[p.Z - MinZ][p.Y * Width + p.X];
    }

    public ref Tile At(int x, int y, int z = 0) => ref At(new TilePos(x, y, z));

    /// <summary>The edge crossed when stepping orthogonally from a to b (they must be neighbours).</summary>
    public Edge EdgeBetween(TilePos a, TilePos b)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == -1) return At(a).North;
        if (dx == 0 && dy == 1) return At(b).North;
        if (dx == -1 && dy == 0) return At(a).West;
        if (dx == 1 && dy == 0) return At(b).West;
        throw new ArgumentException("not orthogonal neighbours");
    }

    public void SetEdgeBetween(TilePos a, TilePos b, Edge e)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == -1) At(a).North = e;
        else if (dx == 0 && dy == 1) At(b).North = e;
        else if (dx == -1 && dy == 0) At(a).West = e;
        else if (dx == 1 && dy == 0) At(b).West = e;
        else throw new ArgumentException("not orthogonal neighbours");
    }

    public int BarricadeBetween(TilePos a, TilePos b)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == -1) return At(a).BarN;
        if (dx == 0 && dy == 1) return At(b).BarN;
        if (dx == -1 && dy == 0) return At(a).BarW;
        if (dx == 1 && dy == 0) return At(b).BarW;
        return 0;
    }

    public void SetBarricadeBetween(TilePos a, TilePos b, int planks)
    {
        byte v = (byte)Math.Clamp(planks, 0, 4);
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dx == 0 && dy == -1) At(a).BarN = v;
        else if (dx == 0 && dy == 1) At(b).BarN = v;
        else if (dx == -1 && dy == 0) At(a).BarW = v;
        else if (dx == 1 && dy == 0) At(b).BarW = v;
    }

    /// <summary>Can you walk across the edge between two orthogonal neighbours right now?</summary>
    public bool Passable(TilePos a, TilePos b) => EdgeBetween(a, b).IsPassable() && BarricadeBetween(a, b) == 0;

    /// <summary>Something a zombie can bang through: a closed door/window or a barricade.</summary>
    public bool Breakable(TilePos a, TilePos b) => EdgeBetween(a, b).IsBreakable() || BarricadeBetween(a, b) > 0;

    public IEnumerable<TilePos> AllTiles(int z = 0)
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return new TilePos(x, y, z);
    }
}
