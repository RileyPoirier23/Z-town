using Godot;

namespace ZTown.Game;

/// <summary>2:1 isometric projection, per ART_SPEC.md: tile footprint 128x64, storey 192 px.</summary>
public static class Iso
{
    public const float HalfW = 64f;
    public const float HalfH = 32f;
    public const float Storey = 192f;
    public const float WallHeight = 112f; // drawn wall height (placeholder art)
    public const float CutWallHeight = 18f;

    /// <summary>Tile-space point (x, y on floor z) to screen.</summary>
    public static Vector2 ToScreen(float x, float y, int z = 0) => new((x - y) * HalfW, (x + y) * HalfH - z * Storey);

    /// <summary>Screen point to tile space on floor z.</summary>
    public static Vector2 ToTile(Vector2 s, int z = 0)
    {
        float sy = s.Y + z * Storey;
        return new Vector2((s.X / HalfW + sy / HalfH) / 2f, (sy / HalfH - s.X / HalfW) / 2f);
    }

    /// <summary>Screen-facing direction for an 8-way facing angle in tile space.</summary>
    public static Vector2 FacingVector(float facing) =>
        (ToScreen(Mathf.Cos(facing), Mathf.Sin(facing)) - ToScreen(0, 0)).Normalized();
}
