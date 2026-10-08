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
    Label _questTitle = null!, _questStep = null!, _phoneBadge = null!;
    VBoxContainer _toasts = null!;
    ColorRect _sleep = null!;
    PanelContainer _wounds = null!;
    Label _woundsText = null!;
    int _notesSeen;
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
        Layer = 2; // above weather effects
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

        // ---- quest tracker (under memere's card)
        var qp = Panel(Control.LayoutPreset.TopLeft, new Vector2(12, 196), 290);
        var qv = (VBoxContainer)qp.GetChild(0);
        _questTitle = Label(qv, 12, HorizontalAlignment.Left, new Color("d06a50"));
        _questStep = Label(qv, 15);
        Label(qv, 11, HorizontalAlignment.Left, Muted).Text = "J: next quest  ·  M: map  ·  P: phone";

        // ---- phone badge (bottom right)
        _phoneBadge = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Right };
        _phoneBadge.AddThemeFontSizeOverride("font_size", 16);
        _phoneBadge.AddThemeColorOverride("font_outline_color", Colors.Black);
        _phoneBadge.AddThemeConstantOverride("outline_size", 5);
        _phoneBadge.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        _phoneBadge.Position = new Vector2(-260, -40);
        _phoneBadge.Size = new Vector2(240, 30);
        AddChild(_phoneBadge);

        // ---- sleep overlay
        _sleep = new ColorRect { Color = new Color(0, 0, 0.02f, 0), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        _sleep.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        var zzz = new Label { Text = "Sleeping…   (any key to wake)", HorizontalAlignment = HorizontalAlignment.Center };
        zzz.AddThemeFontSizeOverride("font_size", 22);
        zzz.SetAnchorsPreset(Control.LayoutPreset.Center);
        zzz.Position = new Vector2(-200, -20);
        zzz.Size = new Vector2(400, 40);
        _sleep.AddChild(zzz);
        AddChild(_sleep);
        MoveChild(_sleep, 0);

        // ---- wounds panel (H)
        _wounds = Panel(Control.LayoutPreset.CenterLeft, new Vector2(12, -120), 300);
        _wounds.Visible = false;
        var wv = (VBoxContainer)_wounds.GetChild(0);
        Label(wv, 12, HorizontalAlignment.Left, Muted).Text = "HEALTH  ·  H to close  ·  Q to bandage";
        _woundsText = Label(wv, 14);

        // ---- toasts (top centre)
        _toasts = new VBoxContainer();
        _toasts.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        _toasts.Position = new Vector2(-200, 16);
        _toasts.CustomMinimumSize = new Vector2(400, 0);
        AddChild(_toasts);

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
            Text = "WASD move · Shift run · Ctrl sneak · E use · Space swing · Tab inventory · B/X board up · Q bandage · C craft · K skills · H health · M map · P phone · J quest · Z sleep · F5/F9 save/load · F1 hide",
            Modulate = new Color(1, 1, 1, 0.5f),
        };
        _help.AddThemeFontSizeOverride("font_size", 12);
        _help.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _help.Position = new Vector2(-470, -26);
        AddChild(_help);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Keycode: Key.F1 }) _help.Visible = !_help.Visible;
        if (e is InputEventKey { Pressed: true, Keycode: Key.H }) _wounds.Visible = !_wounds.Visible;
    }

    static string WoundsText(GameWorld w)
    {
        var p = w.Player;
        var lines = new List<string> { $"Health {p.Health.Value:0}%" };
        if (p.Infection.Infected) lines.Add("Something's wrong. The bite's gone bad.");
        if (p.Wounds.List.Count == 0) lines.Add("No wounds.");
        foreach (var x in p.Wounds.List)
            lines.Add($"• {x.Kind} on the {x.Part}{(x.Bleeding ? " — bleeding" : "")}{(x.Bandaged ? " — bandaged" : "")}{(x.Disinfected ? ", clean" : "")}");
        return string.Join("\n", lines);
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
        var (month, _) = ZTown.Core.Weather.Climate.Date(w.Data.Weather, w.Clock);
        _date.Text = $"{w.Clock.DayOfWeek}, day {w.Clock.Day + 1} · {ZTown.Core.Weather.Climate.Season(month)}\n{w.Weather.Label}, {w.Weather.OutsideC:0}°C";
        var g = w.Power.Generator;
        _power.Text = w.Power.GridOn(w.Clock.Day) ? "Power: grid"
            : w.HousePowered ? $"Power: generator ({g.Fuel:0.0} L)" : "Power: OUT";
        var extra = new List<Moodle>();
        if (p.Wounds.Bleeding) extra.Add(new Moodle(MoodleKind.Bleeding, Mathf.Clamp(p.Wounds.List.Count(x => x.Bleeding) + 1, 2, 4)));
        if (p.Infection.Infected) extra.Add(new Moodle(MoodleKind.Infected, 3));
        RefreshMoodles(Moodles.For(p.Needs, p.Health.Value / 100f, w.Data.Needs.Moodles).Concat(extra).ToList());
        _sleep.Visible = p.Asleep;
        if (p.Asleep) _sleep.Color = new Color(0, 0, 0.02f, Mathf.Min(0.8f, _sleep.Color.A + (float)delta * 0.8f));
        else _sleep.Color = new Color(0, 0, 0.02f, 0);
        if (_wounds.Visible) _woundsText.Text = WoundsText(w);

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

        // quests
        var tracked = w.Quests.Tracked != null ? w.Quests.Quests.GetValueOrDefault(w.Quests.Tracked) : null;
        var qdef = tracked != null ? w.Data.Quests.GetValueOrDefault(tracked.Id) : null;
        if (qdef != null && tracked!.Step < qdef.Steps.Count)
        {
            var step = qdef.Steps[tracked.Step];
            string dist = "";
            if (step.Marker != null && ZTown.Core.Story.QuestLog.Marker(w, step.Marker) is { } mk)
            {
                float d = p.Tile.DistanceTo(mk);
                if (d > 4) dist = $"  ·  {d:0} m {Compass(mk.X + 0.5f - p.X, mk.Y + 0.5f - p.Y)}";
            }
            _questTitle.Text = qdef.Title.ToUpperInvariant() + $"   ({w.Quests.Active.Count()} active)";
            _questStep.Text = step.Text + dist;
        }
        else
        {
            _questTitle.Text = "QUESTS";
            _questStep.Text = "Nothing right now. Look after memere.";
        }
        // phone
        int unread = w.Phone.Unread;
        _phoneBadge.Text = unread > 0 ? $"📱 {unread} new  (P)" : "";
        // notifications
        for (; _notesSeen < w.Notifications.Count; _notesSeen++) Toast(w.Notifications[_notesSeen]);

        _health.Value = p.Health.Value;
        var eq = EquippedItem?.Invoke();
        _equipIcon.Texture = eq != null ? Art?.Items.GetValueOrDefault(eq) : null;
        _equipName.Text = eq != null ? w.Data.Item(eq)?.Name ?? eq : "Bare hands";
    }

    public void Toast(string text)
    {
        var box = new PanelContainer();
        box.AddThemeStyleboxOverride("panel", PanelStyle(0.85f));
        var l = new Label { Text = text, HorizontalAlignment = HorizontalAlignment.Center };
        l.AddThemeFontSizeOverride("font_size", 17);
        box.AddChild(l);
        _toasts.AddChild(box);
        var tw = box.CreateTween();
        tw.TweenInterval(3.5);
        tw.TweenProperty(box, "modulate:a", 0f, 0.8);
        tw.TweenCallback(Callable.From(box.QueueFree));
    }

    public void ResetNotifications(int count) => _notesSeen = count;

    /// <summary>Direction on screen terms (the map is turned to the street grid, so: up/down/left/right on screen).</summary>
    static string Compass(float dx, float dy)
    {
        // tile space -> screen direction
        float sx = dx - dy, sy = (dx + dy) * 0.5f;
        float a = Mathf.RadToDeg(Mathf.Atan2(sy, sx));
        string[] names = { "→", "↘", "↓", "↙", "←", "↖", "↑", "↗" };
        int i = Mathf.PosMod(Mathf.RoundToInt(a / 45f), 8);
        return names[i];
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
