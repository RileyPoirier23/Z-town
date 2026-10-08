using System;
using Godot;

namespace ZTown.Game;

/// <summary>Esc menu: resume, save, settings (volume, fullscreen, text size), quit to title.
/// Settings persist in user://settings.cfg.</summary>
public partial class PauseMenu : CanvasLayer
{
    public Action? OnSave { get; set; }
    public Action? OnQuitToTitle { get; set; }
    public Action<float, float>? OnVolume { get; set; }
    public Action<float>? OnUiScale { get; set; }

    Control _root = null!;
    public bool IsOpen => _root.Visible;
    readonly ConfigFile _cfg = new();
    const string Path = "user://settings.cfg";

    public float Music => (float)_cfg.GetValue("audio", "music", 0.6f);
    public float Sfx => (float)_cfg.GetValue("audio", "sfx", 1f);
    public float UiScale => (float)_cfg.GetValue("display", "ui_scale", 1f);

    public override void _Ready()
    {
        Layer = 9;
        _cfg.Load(Path);
        _root = new Control { Visible = false };
        _root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_root);
        var dim = new ColorRect { Color = new Color(0, 0, 0, 0.6f) };
        dim.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        _root.AddChild(dim);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", Hud.PanelStyle(0.95f));
        panel.SetAnchorsPreset(Control.LayoutPreset.Center);
        panel.Position = new Vector2(-190, -230);
        panel.CustomMinimumSize = new Vector2(380, 0);
        _root.AddChild(panel);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 10);
        panel.AddChild(v);
        var title = new Label { Text = "Paused", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 28);
        v.AddChild(title);
        Btn(v, "Resume", Toggle);
        Btn(v, "Save", () => { OnSave?.Invoke(); Toggle(); });

        v.AddChild(new HSeparator());
        Slider(v, "Music", Music, x => { _cfg.SetValue("audio", "music", x); Apply(); });
        Slider(v, "Sound effects", Sfx, x => { _cfg.SetValue("audio", "sfx", x); Apply(); });
        Slider(v, "Text and UI size", (UiScale - 0.8f) / 0.7f, x => { _cfg.SetValue("display", "ui_scale", 0.8f + x * 0.7f); Apply(); });
        var fs = new CheckButton { Text = "Fullscreen", ButtonPressed = DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen };
        fs.Toggled += on => DisplayServer.WindowSetMode(on ? DisplayServer.WindowMode.Fullscreen : DisplayServer.WindowMode.Windowed);
        v.AddChild(fs);

        v.AddChild(new HSeparator());
        Btn(v, "Quit to title", () => { Toggle(); OnQuitToTitle?.Invoke(); });
        Btn(v, "Quit game", () => { OnSave?.Invoke(); GetTree().Quit(); });
        var credits = new Label
        {
            Text = "Made in memory of memere.\nMap data © OpenStreetMap contributors (ODbL). All brands are parodies.",
            HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.55f), AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        credits.AddThemeFontSizeOverride("font_size", 12);
        v.AddChild(credits);
    }

    public void Apply()
    {
        _cfg.Save(Path);
        OnVolume?.Invoke(Music, Sfx);
        OnUiScale?.Invoke(UiScale);
    }

    static void Btn(Container parent, string text, Action click)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(0, 36) };
        b.Pressed += click;
        parent.AddChild(b);
    }

    static void Slider(Container parent, string label, float value, Action<float> changed)
    {
        var h = new HBoxContainer();
        h.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(150, 0) });
        var s = new HSlider { MinValue = 0, MaxValue = 1, Step = 0.05, Value = value, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.ValueChanged += x => changed((float)x);
        h.AddChild(s);
        parent.AddChild(h);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // the rest of the game is paused, so the menu closes itself on Esc
        if (_root.Visible && e is InputEventKey { Pressed: true, Keycode: Key.Escape })
        {
            Toggle();
            GetViewport().SetInputAsHandled();
        }
    }

    public void Toggle()
    {
        _root.Visible = !_root.Visible;
        GetTree().Paused = _root.Visible;
    }
}
