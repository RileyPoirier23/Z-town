using Godot;
using ZTown.Core.Entities;

namespace ZTown.Game;

/// <summary>Draws a layered character (body, then each worn garment tinted its colour, then
/// any held item), Zomboid paper-doll style. Used by the world view and the character creator.</summary>
public static class CharacterPainter
{
    public static readonly string[] DefaultOrder = { "bottom", "shoes", "top", "outer", "hair", "hat" };

    public static int Frames(Art art, string anim) => art.AnimFrames.TryGetValue(anim, out var n) ? n : 1;

    /// <param name="feet">screen position of the character's feet</param>
    /// <param name="row">facing row 0..7 (world angle row*45°)</param>
    public static void Draw(CanvasItem ci, Art art, Outfit outfit, string anim, int frame, int row, Vector2 feet,
        Color light, float scale = 1f, string? held = null, System.Collections.Generic.IList<string>? order = null)
    {
        var src = new Rect2(frame * art.FrameW, row * art.FrameH, art.FrameW, art.FrameH);
        float s = scale * outfit.Height;
        var size = new Vector2(art.FrameW, art.FrameH) * s;
        var rect = new Rect2(feet - art.Feet * s, size);

        void Layer(string id, Color tint)
        {
            if (!art.Layers.TryGetValue(id, out var l) || !l.Anims.TryGetValue(anim, out var tex)) return;
            ci.DrawTextureRectRegion(tex, rect, src, tint * light);
        }

        Layer("body", Hex(outfit.Skin));
        foreach (var slot in order ?? DefaultOrder)
            if (outfit.In(slot) is { } w) Layer(w.Id, Hex(w.Color));
        if (held != null) Layer(held, Colors.White);
    }

    public static Color Hex(string hex) => Color.FromString(hex, new Color(0.5f, 0.5f, 0.5f));
}
