namespace ZTown.Core.Map;

/// <summary>A tile coordinate. Z is the floor: 0 = ground, 1 = upstairs, -1 = basement.</summary>
public readonly record struct TilePos(int X, int Y, int Z = 0)
{
    public static TilePos operator +(TilePos a, TilePos b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

    /// <summary>Distance on the same floor (different floors count as far apart).</summary>
    public float DistanceTo(TilePos o) =>
        o.Z != Z ? float.PositiveInfinity : MathF.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));

    public int ChebyshevTo(TilePos o) => Math.Max(Math.Abs(X - o.X), Math.Abs(Y - o.Y));

    public override string ToString() => $"({X},{Y},{Z})";
}

/// <summary>An axis-aligned rectangle of tiles on one floor, inclusive of Min, exclusive of Max.</summary>
public readonly record struct TileRect(int MinX, int MinY, int MaxX, int MaxY, int Z = 0)
{
    public bool Contains(TilePos p) => p.Z == Z && p.X >= MinX && p.X < MaxX && p.Y >= MinY && p.Y < MaxY;
    public int Width => MaxX - MinX;
    public int Height => MaxY - MinY;
}
