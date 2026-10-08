using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace ZTown.Game;

/// <summary>Loads generated sprites listed in assets/gen/manifest.json (see tools/art).</summary>
public sealed class Art
{
    public sealed record Sprite(Texture2D Tex, Vector2 Origin);
    public sealed record Layer(string Slot, Dictionary<string, Texture2D> Anims);

    public readonly Dictionary<string, Texture2D[]> Floors = new();
    /// <summary>Seamless grounds: N means the variants form an N x N repeating grid.</summary>
    public readonly Dictionary<string, int> FloorGrid = new();
    public readonly Dictionary<string, Texture2D> Walls = new();
    public readonly Dictionary<string, Texture2D> Roofs = new();
    public readonly Dictionary<string, Texture2D> Moodles = new();
    public readonly Dictionary<string, Texture2D> Items = new();
    public readonly Dictionary<string, Sprite> Objects = new();
    /// <summary>Character layers (body, garments, held items) -> anim -> sheet.</summary>
    public readonly Dictionary<string, Layer> Layers = new();
    public readonly Dictionary<string, int> AnimFrames = new();
    public int FrameW = 192, FrameH = 224;
    public Vector2 Feet = new(96, 196);
    public Vector2 TileOrigin = new(64, 192);

    public static Art Load(string path = "res://assets/gen/manifest.json")
    {
        var art = new Art();
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PushError("art manifest missing: run tools/art/generate.py");
            return art;
        }
        using var doc = JsonDocument.Parse(f.GetAsText());
        var root = doc.RootElement;
        static Texture2D T(string p) => GD.Load<Texture2D>(p);

        foreach (var fl in root.GetProperty("floors").EnumerateObject())
        {
            var list = new List<Texture2D>();
            foreach (var v in fl.Value.EnumerateArray()) list.Add(T(v.GetString()!));
            art.Floors[fl.Name] = list.ToArray();
        }
        foreach (var w in root.GetProperty("walls").EnumerateObject()) art.Walls[w.Name] = T(w.Value.GetString()!);
        if (root.TryGetProperty("moodles", out var md))
            foreach (var x in md.EnumerateObject()) art.Moodles[x.Name] = T(x.Value.GetString()!);
        if (root.TryGetProperty("items", out var its))
            foreach (var x in its.EnumerateObject()) art.Items[x.Name] = T(x.Value.GetString()!);
        if (root.TryGetProperty("roofs", out var roofs))
            foreach (var r in roofs.EnumerateObject()) art.Roofs[r.Name] = T(r.Value.GetString()!);
        foreach (var o in root.GetProperty("objects").EnumerateObject())
        {
            var origin = o.Value.GetProperty("origin");
            art.Objects[o.Name] = new Sprite(T(o.Value.GetProperty("file").GetString()!), new Vector2(origin[0].GetSingle(), origin[1].GetSingle()));
        }
        var ch = root.GetProperty("characters");
        var frame = ch.GetProperty("frame");
        art.FrameW = frame.GetProperty("w").GetInt32();
        art.FrameH = frame.GetProperty("h").GetInt32();
        var ft = frame.GetProperty("feet");
        art.Feet = new Vector2(ft[0].GetSingle(), ft[1].GetSingle());
        foreach (var a in ch.GetProperty("anims").EnumerateObject()) art.AnimFrames[a.Name] = a.Value.GetInt32();
        foreach (var l in ch.GetProperty("layers").EnumerateObject())
        {
            var anims = new Dictionary<string, Texture2D>();
            foreach (var a in l.Value.GetProperty("anims").EnumerateObject()) anims[a.Name] = T(a.Value.GetString()!);
            art.Layers[l.Name] = new Layer(l.Value.GetProperty("slot").GetString()!, anims);
        }
        if (root.TryGetProperty("floorGrid", out var fg))
            foreach (var g in fg.EnumerateObject()) art.FloorGrid[g.Name] = g.Value.GetInt32();
        if (root.TryGetProperty("tileOrigin", out var to)) art.TileOrigin = new Vector2(to[0].GetSingle(), to[1].GetSingle());
        return art;
    }
}
