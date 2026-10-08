using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Dialogue;
using ZTown.Core.Items;
using ZTown.Core.Memere;
using ZTown.Core.Needs;
using Container = Godot.Container;

namespace ZTown.Game;

/// <summary>On-screen info: clock, needs, moodles, memere, power, TV, prompts, subtitles.
/// Plain placeholder UI built in code; the real UI comes with the art pass.</summary>
public partial class Hud : CanvasLayer
{
    public GameWorld? World { get; set; }
    public DialogueDb? Dialogue { get; set; }
    public string Version { get; set; } = "";

    Label _clock = null!, _moodles = null!, _memere = null!, _power = null!, _prompt = null!, _subtitle = null!, _help = null!;
    ProgressBar _health = null!, _hunger = null!, _thirst = null!, _fatigue = null!, _stress = null!, _comfort = null!;
    PanelContainer _subtitleBox = null!;
    double _subtitleTime;

    public override void _Ready()
    {
        var left = Panel(new Vector2(12, 12), 300);
        var v = (VBoxContainer)left.GetChild(0);
        _clock = Text(v, 20);
        _health = Bar(v, "Health", new Color("b8473e"));
        _hunger = Bar(v, "Hunger", new Color("c98a14"));
        _thirst = Bar(v, "Thirst", new Color("4a86b5"));
        _fatigue = Bar(v, "Tired", new Color("7a6fa8"));
        _stress = Bar(v, "Stress", new Color("a8423b"));
        _moodles = Text(v, 14);

        var right = Panel(new Vector2(-332, 12), 320, anchorRight: true);
        var rv = (VBoxContainer)right.GetChild(0);
        Text(rv, 18).Text = "Memere";
        _comfort = Bar(rv, "Comfort", new Color("c9a66b"));
        _memere = Text(rv, 14);
        _power = Text(rv, 14);

        _prompt = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        _prompt.AddThemeFontSizeOverride("font_size", 18);
        _prompt.AddThemeColorOverride("font_outline_color", Colors.Black);
        _prompt.AddThemeConstantOverride("outline_size", 6);
        _prompt.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _prompt.Position = new Vector2(-300, -150);
        _prompt.Size = new Vector2(600, 30);
        AddChild(_prompt);

        _subtitleBox = new PanelContainer { Visible = false };
        _subtitleBox.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        _subtitleBox.Position = new Vector2(-380, -110);
        _subtitleBox.Size = new Vector2(760, 60);
        _subtitle = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart, HorizontalAlignment = HorizontalAlignment.Center };
        _subtitle.AddThemeFontSizeOverride("font_size", 20);
        _subtitleBox.AddChild(_subtitle);
        AddChild(_subtitleBox);

        _help = new Label
        {
            Text = "WASD move · Shift run · Ctrl sneak · E use · Space swing · Tab inventory · F5 save · F9 load · Z sleep · +/- speed",
            Modulate = new Color(1, 1, 1, 0.6f),
        };
        _help.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        _help.Position = new Vector2(12, -30);
        AddChild(_help);
    }

    PanelContainer Panel(Vector2 pos, float width, bool anchorRight = false)
    {
        var p = new PanelContainer();
        if (anchorRight) p.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        p.Position = pos;
        p.CustomMinimumSize = new Vector2(width, 0);
        var v = new VBoxContainer();
        p.AddChild(v);
        AddChild(p);
        return p;
    }

    static Label Text(Container parent, int size)
    {
        var l = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size);
        parent.AddChild(l);
        return l;
    }

    static ProgressBar Bar(Container parent, string label, Color c)
    {
        var h = new HBoxContainer();
        var l = new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) };
        l.AddThemeFontSizeOverride("font_size", 13);
        var b = new ProgressBar { MinValue = 0, MaxValue = 100, ShowPercentage = false, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(0, 14) };
        var fill = new StyleBoxFlat { BgColor = c };
        b.AddThemeStyleboxOverride("fill", fill);
        h.AddChild(l);
        h.AddChild(b);
        parent.AddChild(h);
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

    public override void _Process(double delta)
    {
        var w = World;
        if (w == null) return;
        _subtitleTime -= delta;
        if (_subtitleTime <= 0) _subtitleBox.Visible = false;

        var p = w.Player;
        var n = p.Needs;
        _clock.Text = $"{w.Clock}  ·  {w.Clock.DayOfWeek}   x{w.Data.Sim.TimeScale:0}";
        _health.Value = p.Health.Value;
        _hunger.Value = n.Hunger * 100;
        _thirst.Value = n.Thirst * 100;
        _fatigue.Value = n.Fatigue * 100;
        _stress.Value = n.Stress * 100;
        var moodles = Moodles.For(n, p.Health.Value / 100f, w.Data.Needs.Moodles);
        _moodles.Text = (moodles.Count == 0 ? "Feeling fine" : string.Join("  ", moodles.Select(m => $"{m.Kind} {new string('●', m.Level)}")))
            + (p.Asleep ? "\nAsleep (any key to wake)" : "") + (p.Infection.Infected ? "\nInfected" : "");

        var m = w.Memere;
        _comfort.Value = m.Comfort.Value;
        var supplies = string.Join("\n", w.Data.Supplies.Values.Select(d =>
            $"  {d.Label}: {Comfort.SupplyDays(m.Supplies, d):0.0} days"));
        var asking = Comfort.Asking(m.Supplies, w.Data.Supplies.Values);
        var show = w.NowOnTv;
        _memere.Text = $"Mood: {m.Comfort.Mood}  ·  {Activity(m.Activity)}\n{supplies}"
            + (asking.Count > 0 ? $"\nShe's asking about: {string.Join(", ", asking.Select(a => w.Data.Supplies[a].Label))}" : "")
            + $"\nTV: {(show == null ? "off" : show.Name + (show.NameStatus == "approved" ? "" : " [DRAFT NAME]"))}";
        var g = w.Power.Generator;
        _power.Text = $"Power: {(w.Power.GridOn(w.Clock.Day) ? "grid on" : w.HousePowered ? "generator" : "OUT")}"
            + $"\nGenerator: {(g.Running ? "running" : "off")}{(g.Connected ? ", connected" : ", not connected")}, {g.Fuel:0.0} L"
            + $"\n{Version}";
    }

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
