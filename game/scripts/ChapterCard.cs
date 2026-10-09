using Godot;
using ZTown.Core.Story;

namespace ZTown.Game;

/// <summary>Full-screen title card between chapters ("Chapter 2 · Lights out · Six weeks later.").</summary>
public partial class ChapterCard : CanvasLayer
{
    ColorRect _bg = null!;
    Label _number = null!, _title = null!, _sub = null!;
    double _t = -1;
    const double Hold = 3.5, Fade = 1.2;

    /// <summary>True while the card covers the screen (Main pauses the world).</summary>
    public bool Showing => _t >= 0 && _t < Hold;

    public override void _Ready()
    {
        Layer = 8;
        _bg = new ColorRect { Color = new Color(0.02f, 0.02f, 0.025f), MouseFilter = Control.MouseFilterEnum.Ignore };
        _bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_bg);
        var box = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        box.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _bg.AddChild(box);
        Label L(int size, Color c)
        {
            var l = new Label { HorizontalAlignment = HorizontalAlignment.Center };
            l.AddThemeFontSizeOverride("font_size", size);
            l.AddThemeColorOverride("font_color", c);
            box.AddChild(l);
            return l;
        }
        _number = L(20, new Color(0.82f, 0.42f, 0.31f));
        _title = L(54, new Color(0.95f, 0.92f, 0.86f));
        _sub = L(22, new Color(0.7f, 0.68f, 0.64f));
        Visible = false;
    }

    public void Show(ChapterDef c)
    {
        _number.Text = $"CHAPTER {c.Number}";
        _title.Text = c.Title;
        _sub.Text = c.Card;
        _t = 0;
        Visible = true;
        _bg.Modulate = Colors.White;
    }

    public override void _Process(double delta)
    {
        if (_t < 0) return;
        _t += delta;
        if (_t > Hold) _bg.Modulate = new Color(1, 1, 1, (float)Mathf.Max(0, 1 - (_t - Hold) / Fade));
        if (_t > Hold + Fade) { _t = -1; Visible = false; }
    }
}
