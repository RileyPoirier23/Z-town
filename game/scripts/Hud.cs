using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Dialogue;
using ZTown.Core.Memere;
using ZTown.Core.Needs;
using Container = Godot.Container;

namespace ZTown.Game;

/// <summary>
/// On-screen display, Zomboid-style: clock top right, moodle icons down the right edge,
/// memere's card top left, health and equipped item bottom left, prompts and subtitles
/// bottom centre. Spoken lines appear over the speaker's head (IsoView).
/// </summary>
public partial class Hud : CanvasLayer
{
    public GameWorld? World { get; set; }
    public DialogueDb? Dialogue { get; set; }
    public Art? Art { get; set; }
    public string Version { get; set; } = "";
    public System.Func<string?>? EquippedItem { get; set; }

    Label _time = null!, _date = null!, _power = null!, _memereLine = null!, _asking = null!, _prompt = null!, _subtitle = null!, _help = null!, _equipName = null!;
    ProgressBar _comfort = null!, _health = null!;
    VBoxContainer _moodles = null!;
    TextureRect _equipIcon = null!;
    PanelContainer _subtitleBox = null!;
    readonly Dictionary<string, (ProgressBar bar, Label days)> _supplies = new();
    double _subtitleTime;
    string _moodleKey = "";

    public static StyleBoxFlat PanelStyle(float alpha = 0.78f) => new()
    {
        BgColor = new Color(0.075f, 0.07f, 0.065f, alpha),
        BorderColor = new Color(0.38f, 0.34f, 0.29f, 0.55f),
        BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1,
        CornerRadiusBottomLeft = 3, CornerRadiusBottomRight = 3, CornerRadiusTopLeft = 3, CornerRadiusTopRight = 3,
        ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 8, ContentMarginBottom = 8,
    };

    static Color Muted => new(0.82f, 0.76f, 0.66f);

    public override void _Ready()
    {
        // ---- clock (top right)
        var clock = Panel(Control.LayoutPreset.TopRight, new Vector2(-180, 12), 168);
        var cv = (VBoxContainer)clock.GetChild(0);
        _time = Label(cv, 30, HorizontalAlignment.Right);
        _date = Label(cv, 13, HorizontalAlignment.Right, Muted);
        _power = Label(cv, 12, HorizontalAlignment.Right, Muted);

        // ---- moodles (right edge, under the clock)
        _moodles = new VBoxContainer();
        _moodles.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        _moodles.Position = new Vector2(-72, 128);
        _moodles.AddThemeConstantOverride("separation", 6);
        AddChild(_moodles);

        // ---- memere (top left)
        var mem = Panel(Control.LayoutPreset.TopLeft, new Vector2(12, 12), 290);
        var mv = (VBoxContainer)mem.GetChild(0);
        Label(mv, 12, HorizontalAlignment.Left, Muted).Text = "MEMERE";
        _memereLine = Label(mv, 15);
        _comfort = Bar(mv, new Color("c9a66b"), 10);
        foreach (var (id, icon) in new[] { ("mepsi", "mepsi_can"), ("cigarettes", "cigarettes_pack"), ("puffers", "puffer") })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);
            mv.AddChild(row);
            row.AddChild(new TextureRect { Texture = Art?.Items.GetValueOrDefault(icon), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, CustomMinimumSize = new Vector2(22, 22), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered });
            var bar = Bar(row, new Color("8a9a6a"), 8);
            bar.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            var days = Label(row, 12, HorizontalAlignment.Right, Muted);
            days.CustomMinimumSize = new Vector2(64, 0);
            _supplies[id] = (bar, days);
        }
        _asking = Label(mv, 13, HorizontalAlignment.Left, new Color("e0b060"));

        // ---- health + equipped (bottom left)
        var bl = Panel(Control.LayoutPreset.BottomLeft, new Vector2(12, -96), 250);
        var bv = (VBoxContainer)bl.GetChild(0);
        var hrow = new HBoxContainer();
        bv.AddChild(hrow);
        _equipIcon = new TextureRect { ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, CustomMinimumSize = new Vector2(44, 44), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered };
        var slot = new PanelContainer();
        slot.AddThemeStyleboxOverride("panel", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.35f), BorderColor = new Color(0.4f, 0.36f, 0.3f), BorderWidthBottom = 1, BorderWidthTop = 1, BorderWidthLeft = 1, BorderWidthRight = 1 });
        slot.AddChild(_equipIcon);
        hrow.AddChild(slot);
        var hv = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        hrow.AddChild(hv);
        _equipName = Label(hv, 13, HorizontalAlignment.Left, Muted);
        _health = Bar(hv, new Color("a8423b"), 10);

        // ---- prompt + subtitles (bottom centre)
        _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _prompt.AddThemeFontSizeOverride("font_size", 17);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 6);
        _prompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _prompt.Position = new Vector2(-300, -160);
        _prompt.Size = new Vector2(600, 30);
        AddChild(_prompt);

        _subtitleBox = new PanelContainer { Visible = false };
        _subtitleBox.AddThemeStyleboxOverride("panel", PanelStyle(0.7f));
        _subtitleBox.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _subtitleBox.Position = new Vector2(-360, -118);
        _subtitleBox.CustomMinimumSize = new Vector2(720, 0);
        _subtitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
        _subtitle.AddThemeFontSizeOverride("font_size", 18);
        _subtitleBox.AddChild(_subtitle);
        AddChild(_subtitleBox);

        _help = new Label
        {
            Text = "WASD move · Shift run · Ctrl sneak · E use · Space swing · Tab inventory · Z sleep · F5 save · F9 load · +/- speed · F1 hide",
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        _help.AddThemeFontSizeOverride("font_size", 12);
        _help.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _help.Position = new Vector2(-330, -26);
        AddChild(_help);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.F1 }) _help.Visible = !_help.Visible;
    }

    PanelContainer Panel(Control.LayoutPreset anchor, Vector2 pos, float width)
    {
        var p = new PanelContainer();
        p.AddThemeStyleboxOverride("panel", PanelStyle());
        p.SetAnchorsPreset(anchor);
        p.Position = pos;
        p.CustomMinimumSize = new Vector2(width, 0);
        var v = new VBoxContainer();
        v.AddThemeConstantOverride("separation", 4);
        p.AddChild(v);
        AddChild(p);
        return p;
    }

    static Label Label(Container parent, int size, HorizontalAlignment align = HorizontalAlignment.Left, Color? color = null)
    {
        var l = new Label { HorizontalAlignment = align, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is { } c) l.AddThemeColorOverride("font_color", c);
        parent.AddChild(l);
        return l;
    }

    static ProgressBar Bar(Container parent, Color c, float height)
    {
        var b = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, height) };
        b.AddThemeStyleboxOverride("fill", new StyleBoxFlat { BgColor = c, CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 });
        b.AddThemeStyleboxOverride("background", new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f), CornerRadiusBottomLeft = 2, CornerRadiusBottomRight = 2, CornerRadiusTopLeft = 2, CornerRadiusTopRight = 2 });
        parent.AddChild(b);
        return b;
    }

    public void SetPrompt(string text) => _prompt.Text = text;

    public void Say(string speaker, string? text)
    {
        if (string.IsNullOrEmpty(text)) return;
        _subtitle.Text = string.IsNullOrEmpty(speaker) ? text : $"{speaker}: {text}";
        _subtitleBox.Visible = true;
        _subtitleTime = 5;
    }

    static readonly Color[] MoodleBg =
    {
        new(0.62f, 0.6f, 0.36f), new(0.72f, 0.5f, 0.25f), new(0.66f, 0.3f, 0.2f), new(0.5f, 0.14f, 0.12f),
    };

    void RefreshMoodles(List<Moodle> moodles)
    {
        var key = string.Join(",", moodles.Select(m => $"{m.Kind}{m.Level}"));
        if (key == _moodleKey) return;
        _moodleKey = key;
        foreach (var c in _moodles.GetChildren()) c.QueueFree();
        foreach (var m in moodles)
        {
            var badge = new PanelContainer { TooltipText = $"{m.Kind} ({m.Level}/4)" };
            badge.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = MoodleBg[Mathf.Clamp(m.Level - 1, 0, 3)],
                BorderColor = new Color(0, 0, 0, 0.6f), BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2,
                CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6,
                ContentMarginLeft = 4, ContentMarginRight = 4, ContentMarginTop = 4, ContentMarginBottom = 4,
            });
            badge.AddChild(new TextureRect
            {
                Texture = Art?.Moodles.GetValueOrDefault(m.Kind.ToString().ToLowerInvariant()),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, CustomMinimumSize = new Vector2(48, 48),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            _moodles.AddChild(badge);
        }
    }

    public override void _Process(double delta)
    {
        var w = World;
        if (w == null) return;
        _subtitleTime -= delta;
        if (_subtitleTime <= 0) _subtitleBox.Visible = false;

        var p = w.Player;
        _time.Text = $"{w.Clock.Hour:00}:{w.Clock.Minute:00}";
        _date.Text = $"{w.Clock.DayOfWeek}, day {w.Clock.Day + 1}" + (w.Data.Sim.TimeScale > 0 ? "" : "");
        var g = w.Power.Generator;
        _power.Text = w.Power.GridOn(w.Clock.Day) ? "Power: grid"
            : w.HousePowered ? $"Power: generator ({g.Fuel:0.0} L)" : "Power: OUT";
        RefreshMoodles(Moodles.For(p.Needs, p.Health.Value / 100f, w.Data.Needs.Moodles)
            .Concat(p.Infection.Infected ? new[] { new Moodle(MoodleKind.Infected, 3) } : System.Array.Empty<Moodle>()).ToList());

        var m = w.Memere;
        _comfort.Value = m.Comfort.Value;
        _memereLine.Text = $"{Capital(m.Comfort.Mood)} · {Activity(m.Activity)}";
        foreach (var (id, (bar, days)) in _supplies)
        {
            if (!w.Data.Supplies.TryGetValue(id, out var def)) continue;
            float d = Comfort.SupplyDays(m.Supplies, def);
            bar.Value = Mathf.Clamp(d / def.ComfortableDays * 100, 0, 100);
            days.Text = d >= 10 ? "plenty" : $"{d:0.0} days";
        }
        var asking = Comfort.Asking(m.Supplies, w.Data.Supplies.Values);
        _asking.Text = asking.Count > 0 ? "She's asking about: " + string.Join(", ", asking.Select(a => w.Data.Supplies[a].Label.ToLowerInvariant())) : "";
        _asking.Visible = asking.Count > 0;

        _health.Value = p.Health.Value;
        var eq = EquippedItem?.Invoke();
        _equipIcon.Texture = eq != null ? Art?.Items.GetValueOrDefault(eq) : null;
        _equipName.Text = eq != null ? w.Data.Item(eq)?.Name ?? eq : "Bare hands";
    }

    static string Capital(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    static string Activity(MemereActivity a) => a switch
    {
        MemereActivity.WatchingTv => "watching her show",
        MemereActivity.Napping => "napping in her chair",
        MemereActivity.Smoking => "having a smoke",
        MemereActivity.HavingAMepsi => "having a Mepsi",
        MemereActivity.UsingPuffer => "using her puffer",
        MemereActivity.Talking => "talking",
        _ => "in her chair",
    };
}
