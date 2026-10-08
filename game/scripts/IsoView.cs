using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Entities;
using ZTown.Core.Map;

namespace ZTown.Game;

/// <summary>
/// Placeholder renderer: draws the world with flat-shaded shapes in the sizes ART_SPEC.md
/// sets, so everything is playable before real art exists. Walls in front of the player are
/// cut down (Zomboid-style cutaway). Replaced piece by piece by sprites as art arrives.
/// </summary>
public partial class IsoView : Node2D
{
    public GameWorld? World { get; set; }
    public Vector2I? HoverTile { get; set; }

    static readonly Dictionary<string, Color> FloorColors = new()
    {
        ["grass"] = new Color("5d6b45"),
        ["asphalt"] = new Color("4a4a4d"),
        ["carpet"] = new Color("8a6f5a"),
        ["linoleum"] = new Color("b8ad95"),
        ["tile_store"] = new Color("a3a6a0"),
    };

    static readonly Dictionary<string, Color> WallColors = new()
    {
        ["siding_white"] = new Color("d9d4c7"),
        ["brick_red"] = new Color("8c4a3c"),
    };

    static readonly Dictionary<string, (Color c, float h)> Objects = new()
    {
        ["memere_chair"] = (new Color("7b5e8a"), 34),
        ["tv"] = (new Color("2a2a2e"), 40),
        ["fridge"] = (new Color("e8e8e4"), 96),
        ["cupboard"] = (new Color("8a6a48"), 52),
        ["cooler"] = (new Color("9fc4d8"), 96),
        ["shelf"] = (new Color("a08868"), 80),
        ["counter"] = (new Color("6d5a48"), 46),
        ["crate"] = (new Color("8f7a52"), 36),
        ["generator"] = (new Color("c8a03a"), 34),
    };

    public override void _Process(double delta) => QueueRedraw();

    float Light(TilePos p)
    {
        var w = World!;
        double h = w.Clock.HourOfDay;
        // daylight curve: dark 21:00-05:00, dawn/dusk ramps
        float day = h < 5 || h >= 21 ? 0.22f : h < 7 ? Mathf.Lerp(0.22f, 1f, (float)(h - 5) / 2f) : h >= 19 ? Mathf.Lerp(1f, 0.22f, (float)(h - 19) / 2f) : 1f;
        ref var t = ref w.Map.At(p);
        if (t.Room != 0)
        {
            bool lit = w.IsHome(p) ? w.HousePowered : w.Power.GridOn(w.Clock.Day);
            return lit ? Mathf.Max(day * 0.85f, 0.85f) : day * 0.7f;
        }
        // streetlights while the grid is up
        return w.Power.GridOn(w.Clock.Day) ? Mathf.Max(day, 0.38f) : day;
    }

    static Color Shade(Color c, float l) => new(c.R * l, c.G * l, c.B * l, c.A);

    public override void _Draw()
    {
        var w = World;
        if (w == null) return;
        var map = w.Map;
        var player = w.Player;
        float playerDepth = player.X + player.Y;

        // only draw what's near the camera
        var center = player.Tile;
        int r = 30;
        int x0 = Mathf.Max(0, center.X - r), x1 = Mathf.Min(map.Width - 1, center.X + r);
        int y0 = Mathf.Max(0, center.Y - r), y1 = Mathf.Min(map.Height - 1, center.Y + r);

        // 1. floors
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new TilePos(x, y);
                ref var t = ref map.At(p);
                var c = FloorColors.GetValueOrDefault(t.Floor ?? "", new Color("555555"));
                if ((x + y) % 2 == 0) c = c.Darkened(0.04f);
                if (w.ProtectedZones.Contains(p)) c = c.Lerp(new Color("c9a66b"), 0.12f);
                DrawDiamond(x, y, Shade(c, Light(p)));
                if (HoverTile is { } hv && hv.X == x && hv.Y == y) DrawDiamondOutline(x, y, new Color(1, 1, 1, 0.5f));
            }

        // 2. everything with height, back to front
        var items = new List<(float depth, int order, System.Action draw)>();
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var p = new TilePos(x, y);
                var t = map.At(p);
                float l = Light(p);
                if (t.North != Edge.None)
                {
                    float d = x + 0.5f + y;
                    bool cut = d > playerDepth && Mathf.Abs(x - player.X) < 10 && Mathf.Abs(y - player.Y) < 10;
                    int xx = x, yy = y;
                    var style = t.WallStyle;
                    items.Add((d, 0, () => DrawEdge(new Vector2(xx, yy), new Vector2(xx + 1, yy), map.At(xx, yy).North, style, l, cut, true)));
                }
                if (t.West != Edge.None)
                {
                    float d = x + y + 0.5f;
                    bool cut = d > playerDepth && Mathf.Abs(x - player.X) < 10 && Mathf.Abs(y - player.Y) < 10;
                    int xx = x, yy = y;
                    var style = t.WallStyle;
                    items.Add((d, 0, () => DrawEdge(new Vector2(xx, yy), new Vector2(xx, yy + 1), map.At(xx, yy).West, style, l, cut, false)));
                }
                if (t.Object != null && Objects.TryGetValue(t.Object, out var o))
                {
                    int xx = x, yy = y;
                    items.Add((x + y + 1f, 1, () => DrawBox(xx, yy, Shade(o.c, l), o.h)));
                }
            }
        foreach (var e in w.Entities)
        {
            if (Mathf.Abs(e.X - player.X) > r || Mathf.Abs(e.Y - player.Y) > r) continue;
            var ent = e;
            float l = map.InBounds(e.Tile) ? Light(e.Tile) : 1;
            items.Add((e.X + e.Y, 2, () => DrawEntity(ent, l)));
        }
        foreach (var it in items.OrderBy(i => i.depth).ThenBy(i => i.order)) it.draw();
    }

    void DrawDiamond(float x, float y, Color c)
    {
        var pts = new[] { Iso.ToScreen(x, y), Iso.ToScreen(x + 1, y), Iso.ToScreen(x + 1, y + 1), Iso.ToScreen(x, y + 1) };
        DrawColoredPolygon(pts, c);
    }

    void DrawDiamondOutline(float x, float y, Color c)
    {
        var pts = new[] { Iso.ToScreen(x, y), Iso.ToScreen(x + 1, y), Iso.ToScreen(x + 1, y + 1), Iso.ToScreen(x, y + 1), Iso.ToScreen(x, y) };
        DrawPolyline(pts, c, 2);
    }

    void DrawEdge(Vector2 a, Vector2 b, Edge e, string? style, float light, bool cut, bool north)
    {
        var A = Iso.ToScreen(a.X, a.Y);
        var B = Iso.ToScreen(b.X, b.Y);
        float h = cut ? Iso.CutWallHeight : Iso.WallHeight;
        var wall = WallColors.GetValueOrDefault(style ?? "", new Color("bdb5a6"));
        wall = north ? wall : wall.Darkened(0.18f);
        wall = Shade(wall, light);
        var up = new Vector2(0, -h);

        void Quad(Vector2 p, Vector2 q, float bottom, float top, Color c) =>
            DrawColoredPolygon(new[] { p + new Vector2(0, -bottom), q + new Vector2(0, -bottom), q + new Vector2(0, -top), p + new Vector2(0, -top) }, c);
        Vector2 L(float f) => A.Lerp(B, f);

        switch (e)
        {
            case Edge.Wall:
                Quad(A, B, 0, h, wall);
                break;
            case Edge.DoorClosed:
                Quad(A, L(0.15f), 0, h, wall);
                Quad(L(0.85f), B, 0, h, wall);
                Quad(L(0.15f), L(0.85f), Mathf.Min(h, 90), h, wall);
                Quad(L(0.15f), L(0.85f), 0, Mathf.Min(h, 90), Shade(new Color("6b4a2e"), light));
                break;
            case Edge.DoorOpen:
            case Edge.DoorBroken:
                Quad(A, L(0.15f), 0, h, wall);
                Quad(L(0.85f), B, 0, h, wall);
                Quad(L(0.15f), L(0.85f), Mathf.Min(h, 90), h, wall);
                if (e == Edge.DoorBroken)
                    DrawLine(L(0.2f), L(0.7f) + new Vector2(0, -10), Shade(new Color("4a3320"), light), 4);
                break;
            default: // windows
                Quad(A, B, 0, Mathf.Min(h, 40), wall);
                if (h > 40)
                {
                    Quad(A, L(0.2f), 40, h, wall);
                    Quad(L(0.8f), B, 40, h, wall);
                    Quad(L(0.2f), L(0.8f), Mathf.Min(h, 90), h, wall);
                    var glass = e switch
                    {
                        Edge.WindowOpen => new Color(0.6f, 0.75f, 0.85f, 0.25f),
                        Edge.WindowBroken => new Color(0.15f, 0.15f, 0.18f, 0.8f),
                        _ => new Color(0.55f, 0.72f, 0.85f, 0.65f),
                    };
                    Quad(L(0.2f), L(0.8f), 40, Mathf.Min(h, 90), Shade(glass, light));
                }
                break;
        }
        DrawLine(A + up, B + up, wall.Darkened(0.3f), 1);
    }

    void DrawBox(float x, float y, Color c, float h)
    {
        float i = 0.12f;
        var p0 = Iso.ToScreen(x + i, y + i);
        var p1 = Iso.ToScreen(x + 1 - i, y + i);
        var p2 = Iso.ToScreen(x + 1 - i, y + 1 - i);
        var p3 = Iso.ToScreen(x + i, y + 1 - i);
        var up = new Vector2(0, -h);
        DrawColoredPolygon(new[] { p3, p2, p2 + up, p3 + up }, c.Darkened(0.25f));
        DrawColoredPolygon(new[] { p1, p2, p2 + up, p1 + up }, c.Darkened(0.1f));
        DrawColoredPolygon(new[] { p0 + up, p1 + up, p2 + up, p3 + up }, c.Lightened(0.08f));
    }

    void DrawEntity(Entity e, float light)
    {
        var s = Iso.ToScreen(e.X, e.Y);
        Color body, head = Shade(new Color("e0b9a0"), light);
        float height = 150;
        switch (e)
        {
            case Player:
                body = new Color("3d5a80");
                break;
            case MemereEntity:
                body = new Color("9a7bb0");
                height = 118; // she's in her chair
                break;
            case Zombie z when z.IsDead:
                DrawSetTransform(s, 0, new Vector2(1, 0.45f));
                DrawCircle(Vector2.Zero, 34, Shade(new Color("4d5a42"), light));
                DrawSetTransform(Vector2.Zero, 0, Vector2.One);
                return;
            case Zombie:
                body = new Color("5f6e4f");
                head = Shade(new Color("9aa58a"), light);
                break;
            default:
                body = new Color("8a6a4a");
                break;
        }
        body = Shade(body, light);
        // shadow
        DrawSetTransform(s, 0, new Vector2(1, 0.5f));
        DrawCircle(Vector2.Zero, 26, new Color(0, 0, 0, 0.3f));
        DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        // body capsule + head
        var top = s + new Vector2(0, -height + 26);
        DrawRect(new Rect2(s.X - 18, top.Y, 36, height - 26 - 6), body);
        DrawCircle(top, 18, body);
        DrawCircle(s + new Vector2(0, -height), 20, head);
        // facing tick
        var f = Iso.FacingVector(e.Facing);
        DrawLine(s + new Vector2(0, -height * 0.6f), s + new Vector2(0, -height * 0.6f) + f * 30, Shade(Colors.White, light * 0.8f), 3);
    }
}
