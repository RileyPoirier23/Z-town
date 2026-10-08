using System.Collections.Generic;
using Godot;
using ZTown.Core;
using ZTown.Core.Entities;
using ZTown.Core.Map;
using ZTown.Core.Memere;
using ZTown.Core.Zombies;

namespace ZTown.Game;

/// <summary>
/// Draws the world with the generated sprites (tools/art), Zomboid-style: walls in front of
/// the player cut down, trees go see-through when you're behind them, what you can't see is
/// darkened and zombies there are hidden, and night is properly dark except where there's
/// power.
/// </summary>
public partial class IsoView : Node2D
{
    public GameWorld? World { get; set; }
    public Art? Art { get; set; }
    public Vector2I? HoverTile { get; set; }
    /// <summary>Set by Main while the player is swinging.</summary>
    public float PlayerSwing { get; set; }

    const int ViewRadius = 38;
    bool[] _visible = System.Array.Empty<bool>();
    bool[] _seen = System.Array.Empty<bool>();
    double _visTimer;
    readonly Dictionary<int, (Vector2 last, float time, bool moving, float sinceMove)> _anim = new();
    readonly List<(float depth, int order, int index)> _order = new();
    readonly List<System.Action> _draws = new();
    Color[] _tileLight = System.Array.Empty<Color>();
    // where the floor diamond sits inside a 128x256 tile canvas
    static readonly Vector2[] FloorUv = { new(0.5f, 0.75f), new(1f, 0.875f), new(0.5f, 1f), new(0f, 0.875f) };

    public override void _Process(double delta)
    {
        if (World == null) return;
        _visTimer -= delta;
        if (_visTimer <= 0)
        {
            _visTimer = 0.12;
            UpdateVisibility();
        }
        foreach (var e in World.Entities)
        {
            var pos = new Vector2(e.X, e.Y);
            if (!_anim.TryGetValue(e.Id, out var a)) a = (pos, 0, false, 1);
            bool moved = (pos - a.last).LengthSquared() > 0.00001f;
            a.sinceMove = moved ? 0 : a.sinceMove + (float)delta;
            a.moving = a.sinceMove < 0.12f;
            a.time += (float)delta * (moved ? Mathf.Clamp((pos - a.last).Length() / (float)delta / 2.2f, 0.6f, 1.8f) : 1f);
            a.last = pos;
            _anim[e.Id] = a;
        }
        PlayerSwing = Mathf.Max(0, PlayerSwing - (float)delta);
        _now += delta;
        QueueRedraw();
    }

    void UpdateVisibility()
    {
        var w = World!;
        var map = w.Map;
        int n = map.Width * map.Height;
        if (_visible.Length != n)
        {
            _visible = new bool[n];
            _seen = new bool[n];
        }
        System.Array.Clear(_visible);
        var pt = w.Player.Tile;
        for (int y = Mathf.Max(0, pt.Y - ViewRadius); y <= Mathf.Min(map.Height - 1, pt.Y + ViewRadius); y++)
            for (int x = Mathf.Max(0, pt.X - ViewRadius); x <= Mathf.Min(map.Width - 1, pt.X + ViewRadius); x++)
            {
                var t = new TilePos(x, y, pt.Z);
                if (pt.DistanceTo(t) > ViewRadius) continue;
                // a tile counts as seen if you can see it, or the face of a wall on it
                bool vis = w.LineOfSight(pt, t);
                if (!vis)
                    foreach (var (dx, dy) in new[] { (0, 1), (1, 0), (0, -1), (-1, 0) })
                    {
                        var nb = new TilePos(x + dx, y + dy, pt.Z);
                        if (map.InBounds(nb) && w.LineOfSight(pt, nb) && map.At(t).Room != map.At(nb).Room) { vis = true; break; }
                    }
                _visible[y * map.Width + x] = vis;
                if (vis) _seen[y * map.Width + x] = true;
            }
    }

    bool Visible(int x, int y) => _visible.Length > 0 && _visible[y * World!.Map.Width + x];

    /// <summary>Light colour for a tile: daylight, night blue, warm where there's power.</summary>
    Color Light(TilePos p)
    {
        var w = World!;
        double h = w.Clock.HourOfDay;
        float day = h < 5 || h >= 21 ? 0f : h < 7 ? (float)(h - 5) / 2f : h >= 19 ? 1 - (float)(h - 19) / 2f : 1f;
        var sun = new Color(1.0f, 0.98f, 0.94f);
        var moon = new Color(0.16f, 0.19f, 0.30f);
        var c = moon.Lerp(sun, day);
        ref var t = ref w.Map.At(p);
        if (t.Room != 0)
        {
            bool lit = w.IsHome(p) ? w.HousePowered : w.Power.GridOn(w.Clock.Day);
            c = lit ? c.Lerp(new Color(1.0f, 0.88f, 0.68f), 1 - day * 0.6f) * 1f : c * 0.78f;
        }
        else if (w.Power.GridOn(w.Clock.Day) && day < 0.6f)
            c = c.Lerp(new Color(0.55f, 0.5f, 0.42f), 0.35f * (1 - day)); // streetlight spill
        if (!Visible(p.X, p.Y))
        {
            float g = (c.R + c.G + c.B) / 3f;
            c = new Color(g, g, g).Lerp(c, 0.35f) * (_seen.Length > 0 && _seen[p.Y * w.Map.Width + p.X] ? 0.5f : 0.38f);
        }
        return new Color(c.R, c.G, c.B, 1);
    }

    static Vector2 Screen(float x, float y) => Iso.ToScreen(x, y);

    public override void _Draw()
    {
        var w = World;
        var art = Art;
        if (w == null || art == null) return;
        var map = w.Map;
        var player = w.Player;
        float playerDepth = player.X + player.Y;
        var c = player.Tile;
        int r = 36;
        int x0 = Mathf.Max(0, c.X - r), x1 = Mathf.Min(map.Width - 1, c.X + r);
        int y0 = Mathf.Max(0, c.Y - r), y1 = Mathf.Min(map.Height - 1, c.Y + r);

        // 1. floors, lit per corner (light blends smoothly between tiles, like Zomboid's vertex lighting)
        int gw = x1 - x0 + 1, gh = y1 - y0 + 1;
        if (_tileLight.Length < gw * gh) _tileLight = new Color[gw * gh];
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
                _tileLight[(y - y0) * gw + (x - x0)] = Light(new TilePos(x, y));
        Color Corner(int cx, int cy)
        {
            // average of the (up to) four tiles touching this corner
            float r = 0, g = 0, b = 0;
            int n = 0;
            for (int ty = cy - 1; ty <= cy; ty++)
                for (int tx = cx - 1; tx <= cx; tx++)
                {
                    if (tx < x0 || ty < y0 || tx > x1 || ty > y1) continue;
                    var l = _tileLight[(ty - y0) * gw + (tx - x0)];
                    r += l.R; g += l.G; b += l.B; n++;
                }
            return n == 0 ? Colors.Black : new Color(r / n, g / n, b / n);
        }
        var pts = new Vector2[4];
        var cols = new Color[4];
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                ref var t = ref map.At(x, y);
                var floor = t.Floor ?? "grass";
                if (floor == "void") continue;
                if (!art.Floors.TryGetValue(floor, out var vars)) vars = art.Floors[floor = "grass"];
                var tex = art.FloorGrid.TryGetValue(floor, out int g)
                    ? vars[x % g + y % g * g]
                    : vars[(int)((uint)(x * 73856093 ^ y * 19349663) % vars.Length)];
                pts[0] = Screen(x, y); pts[1] = Screen(x + 1, y); pts[2] = Screen(x + 1, y + 1); pts[3] = Screen(x, y + 1);
                cols[0] = Corner(x, y); cols[1] = Corner(x + 1, y); cols[2] = Corner(x + 1, y + 1); cols[3] = Corner(x, y + 1);
                DrawPolygon(pts, cols, FloorUv, tex);
            }
        if (HoverTile is { } hv && map.InBounds(new TilePos(hv.X, hv.Y)))
            DrawPolyline(new[] { Screen(hv.X, hv.Y), Screen(hv.X + 1, hv.Y), Screen(hv.X + 1, hv.Y + 1), Screen(hv.X, hv.Y + 1), Screen(hv.X, hv.Y) }, new Color(1, 1, 1, 0.35f), 2);

        // 2. walls, objects and people, back to front
        _order.Clear();
        _draws.Clear();
        void Add(float depth, int order, System.Action draw)
        {
            _order.Add((depth, order, _draws.Count));
            _draws.Add(draw);
        }

        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var t = map.At(x, y);
                int xx = x, yy = y;
                if (t.North != Edge.None)
                {
                    float d = x + 0.5f + y;
                    Add(d, 0, () => DrawWall(xx, yy, 'N', playerDepth));
                }
                if (t.West != Edge.None)
                {
                    float d = x + y + 0.5f;
                    Add(d, 0, () => DrawWall(xx, yy, 'W', playerDepth));
                }
                if (t.Object != null && art.Objects.TryGetValue(ObjectSprite(t.Object, x, y), out var spr))
                {
                    bool flat = t.Object == "rug";
                    Add(flat ? -1000 + x + y : x + y + 1f, 1, () =>
                    {
                        var col = Light(new TilePos(xx, yy));
                        // trees and tall things go see-through when the player is behind them
                        if (t.Object!.StartsWith("tree") && xx + yy + 1 > playerDepth && Mathf.Abs((xx - yy) - (player.X - player.Y)) < 4 && xx + yy + 1 - playerDepth < 7)
                            col.A = 0.35f;
                        DrawTexture(spr.Tex, Screen(xx, yy) - spr.Origin, col);
                    });
                }
            }
        // roofs: every building tile except the building you're in (Zomboid takes the roof off)
        int inside = map.At(player.Tile).Building;
        for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                int bi = map.At(x, y).Building;
                if (bi == 0 || bi == inside) continue;
                var b = w.Buildings[bi - 1];
                if (!art.Roofs.TryGetValue(b.Roof, out var roofTex)) continue;
                int xx = x, yy = y;
                Add(x + y + 1.95f, 3, () => DrawRoofTile(b, roofTex, xx, yy));
            }
        foreach (var e in w.Entities)
        {
            if (Mathf.Abs(e.X - player.X) > r || Mathf.Abs(e.Y - player.Y) > r) continue;
            var tile = e.Tile;
            if (!map.InBounds(tile)) continue;
            if (e is Zombie && !Visible(tile.X, tile.Y)) continue; // you only see what you can see
            var ent = e;
            Add(e.X + e.Y + (e is MemereEntity ? 0.05f : 0), 2, () => DrawEntity(ent));
        }
        _order.Sort((a, b) => a.depth != b.depth ? a.depth.CompareTo(b.depth) : a.order.CompareTo(b.order));
        foreach (var o in _order) _draws[o.index]();
        // Zomboid-style silhouette: you can always see yourself, even behind walls and roofs
        _silhouette = true;
        DrawEntity(player);
        _silhouette = false;
        DrawSpeech();
    }

    const float WallM = 2.45f;       // storey height in metres (192 px)
    const float UpPx = 78.4f;        // px per vertical metre

    /// <summary>Roof height (m) at a tile corner: a hip roof rising from the outside walls,
    /// worked out from how far in each touching tile is.</summary>
    static float RoofHeight(ZTown.Core.Building b, int cx, int cy)
    {
        float d = float.MaxValue;
        for (int ty = cy - 1; ty <= cy; ty++)
            for (int tx = cx - 1; tx <= cx; tx++)
            {
                if (!b.RoofDist.TryGetValue((tx, ty), out var td)) return WallM; // corner on the outside edge
                d = Mathf.Min(d, td);
            }
        return WallM + Mathf.Max(0, d - 0.5f) * b.Pitch;
    }

    void DrawRoofTile(ZTown.Core.Building b, Texture2D tex, int x, int y)
    {
        var corners = new[] { (x, y), (x + 1, y), (x + 1, y + 1), (x, y + 1) };
        var pts = new Vector2[4];
        var hs = new float[4];
        for (int i = 0; i < 4; i++)
        {
            hs[i] = RoofHeight(b, corners[i].Item1, corners[i].Item2);
            pts[i] = Screen(corners[i].Item1, corners[i].Item2) - new Vector2(0, hs[i] * UpPx);
        }
        // shade by slope: faces tipped toward the camera/light are brighter
        float dx = (hs[1] + hs[2] - hs[0] - hs[3]) / 2f;   // rise along +x
        float dy = (hs[2] + hs[3] - hs[0] - hs[1]) / 2f;   // rise along +y
        var n = new Vector3(-dx, -dy, 1).Normalized();
        float lit = Mathf.Clamp(0.55f + 0.45f * n.Dot(new Vector3(0.25f, 0.70f, 0.67f).Normalized()), 0.45f, 1.08f);
        var light = Light(new TilePos(x, y));
        // roofs are outside: never darkened by "can't see", only by time of day
        var c = new Color(light.R, light.G, light.B) * lit;
        if (!Visible(x, y)) c = c * 1.6f;
        c.A = 1;
        var uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        DrawPolygon(pts, new[] { c, c, c, c }, uv, tex);
        // eave line along the outside edges you can see
        var eave = new Color(0.12f, 0.1f, 0.09f, 0.8f);
        if (!b.RoofDist.ContainsKey((x, y + 1))) DrawLine(pts[3], pts[2], eave, 2);
        if (!b.RoofDist.ContainsKey((x + 1, y))) DrawLine(pts[1], pts[2], eave, 2);
    }

    string ObjectSprite(string obj, int x, int y)
    {
        if (obj == "tv" && World!.Tv.On && World.HousePowered) return "tv_on";
        return obj;
    }

    void DrawWall(int x, int y, char side, float playerDepth)
    {
        var w = World!;
        var map = w.Map;
        var art = Art!;
        var owner = new TilePos(x, y);
        var other = side == 'N' ? new TilePos(x, y - 1) : new TilePos(x - 1, y);
        ref var to = ref map.At(owner);
        var edge = side == 'N' ? to.North : to.West;
        string style;
        if (to.Room != 0) style = InteriorStyle(to.Floor);
        else if (map.InBounds(other) && map.At(other).Room != 0) style = map.At(other).WallStyle ?? "siding_white";
        else if (map.InBounds(other) && map.At(other).Building != 0) style = w.Buildings[map.At(other).Building - 1].Exterior;
        else style = "fence_wood";
        string kind = edge switch
        {
            Edge.DoorClosed => "door_closed",
            Edge.DoorOpen => "door_open",
            Edge.DoorBroken => "door_broken",
            Edge.WindowClosed => "window_closed",
            Edge.WindowOpen => "window_open",
            Edge.WindowBroken => "window_broken",
            _ => "wall_closed",
        };
        if (style == "fence_wood") kind = "wall_closed";
        float depth = side == 'N' ? x + 0.5f + y : x + y + 0.5f;
        var p = w.Player;
        // Zomboid-style cutaway: walls between the camera and the player drop to a stub
        bool cut = style != "fence_wood" && depth > playerDepth - 0.2f && depth - playerDepth < 9
            && Mathf.Abs((x - y) - (p.X - p.Y)) < 9 && (w.Map.At(p.Tile).Building != 0 && (to.Building == w.Map.At(p.Tile).Building || (map.InBounds(other) && map.At(other).Building == w.Map.At(p.Tile).Building)) || depth - playerDepth < 2.5f);
        var key = $"{style}/{kind}_{side}{(cut ? "_cut" : "")}";
        if (!art.Walls.TryGetValue(key, out var tex)) return;
        // light the wall from the side you're looking at
        var lit = Light(owner);
        DrawTexture(tex, Screen(x, y) - art.TileOrigin, lit);
    }

    static string InteriorStyle(string? floor) => floor switch
    {
        "linoleum" => "wallpaper_kitchen",
        "tile_store" or "driveway" => "store_white",
        _ => "wallpaper_living",
    };

    readonly Dictionary<int, (string text, double until)> _speech = new();
    double _now;

    /// <summary>Shows a line of speech over someone's head, Zomboid-style.</summary>
    public void Speak(int entityId, string? text, double seconds)
    {
        if (!string.IsNullOrEmpty(text)) _speech[entityId] = (text, _now + seconds);
    }

    void DrawSpeech()
    {
        var font = ThemeDB.FallbackFont;
        foreach (var e in World!.Entities)
        {
            if (!_speech.TryGetValue(e.Id, out var sp) || sp.until < _now) continue;
            var pos = Screen(e.X, e.Y) + new Vector2(0, e is MemereEntity ? -150 : -175);
            const int size = 18;
            var w = font.GetStringSize(sp.text, HorizontalAlignment.Center, 420, size).X;
            float alpha = (float)Mathf.Clamp(sp.until - _now, 0, 1);
            DrawMultilineStringOutline(font, pos - new Vector2(Mathf.Min(w, 420) / 2, 0), sp.text, HorizontalAlignment.Center, 420, size, -1, 6, new Color(0, 0, 0, 0.85f * alpha));
            DrawMultilineString(font, pos - new Vector2(Mathf.Min(w, 420) / 2, 0), sp.text, HorizontalAlignment.Center, 420, size, -1, new Color(1f, 0.95f, 0.85f, alpha));
        }
    }

    /// <summary>Set by Main: the equipped weapon shows in the swing animation.</summary>
    public bool PlayerHasWeapon { get; set; }
    readonly Dictionary<int, Outfit> _zombieLooks = new();
    bool _silhouette;

    void DrawEntity(Entity e)
    {
        var art = Art!;
        var w = World!;
        var clothing = w.Data.Clothing;
        string anim;
        string? held = null;
        Outfit outfit;
        _anim.TryGetValue(e.Id, out var a);
        switch (e)
        {
            case Player pl:
                outfit = pl.Outfit;
                anim = PlayerSwing > 0 ? "swing" : !a.moving ? "idle"
                    : Input.IsActionPressed("run") ? "run" : Input.IsActionPressed("sneak") ? "sneak" : "walk";
                if (anim == "swing" && PlayerHasWeapon) held = "bat";
                break;
            case MemereEntity m:
                outfit = clothing.Presets.GetValueOrDefault("memere") ?? new Outfit();
                anim = m.Activity switch
                {
                    MemereActivity.WatchingTv => "watch_tv",
                    MemereActivity.Napping => "nap_chair",
                    MemereActivity.Smoking => "smoke",
                    MemereActivity.HavingAMepsi => "drink_mepsi",
                    _ => "idle_chair",
                };
                held = anim == "smoke" ? "cigarette" : anim == "drink_mepsi" ? "can" : null;
                break;
            case Zombie z:
                if (!_zombieLooks.TryGetValue(z.Look, out outfit!))
                    _zombieLooks[z.Look] = outfit = Wardrobe.ForZombie(clothing, z.Look);
                anim = z.IsDead ? "dead" : z.Brain.AttackTimer > w.Data.Zombies.AttackSeconds - 0.5f ? "attack" : a.moving ? "shamble" : "z_idle";
                break;
            case Dad d:
                outfit = d.Outfit;
                anim = a.moving ? "walk" : "idle";
                break;
            default:
                return;
        }

        int row = Mathf.PosMod(Mathf.RoundToInt(e.Facing / (Mathf.Pi / 4)), 8);
        int frames = CharacterPainter.Frames(art, anim);
        float fps = anim switch { "run" => 14, "swing" => 16, "shamble" => 7, "sneak" => 8, "walk" => 10, _ => 3 };
        int frame = frames <= 1 ? 0 : anim == "swing"
            ? Mathf.Clamp((int)((1 - PlayerSwing / 0.45f) * frames), 0, frames - 1)
            : (int)(a.time * fps) % frames;
        var feet = Screen(e.X, e.Y);
        if (_silhouette)
        {
            CharacterPainter.Draw(this, art, outfit, anim, frame, row, feet, new Color(0.75f, 0.85f, 1f, 0.28f), 1f, held, clothing.SlotOrder);
            return;
        }
        if (anim != "dead")
        {
            DrawSetTransform(feet, 0, new Vector2(1, 0.5f));
            DrawCircle(Vector2.Zero, 24, new Color(0, 0, 0, 0.26f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
        }
        CharacterPainter.Draw(this, art, outfit, anim, frame, row, feet, Light(e.Tile), 1f, held, clothing.SlotOrder);
    }
}
