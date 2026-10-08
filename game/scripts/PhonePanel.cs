using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Dialogue;

namespace ZTown.Game;

/// <summary>The phone (P): battery, service, texts and alerts while the network lasts.</summary>
public partial class PhonePanel : CanvasLayer
{
    public GameWorld? World { get; set; }
    public DialogueDb? Dialogue { get; set; }
    PanelContainer _root = null!;
    VBoxContainer _list = null!;
    Label _status = null!;
    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        Layer = 4;
        _root = new PanelContainer { Visible = false };
        _root.AddThemeStyleboxOverride("panel", new StyleBoxFlat
        {
            BgColor = new Color(0.06f, 0.06f, 0.07f, 0.97f), BorderColor = new Color(0.3f, 0.3f, 0.32f),
            BorderWidthBottom = 6, BorderWidthTop = 18, BorderWidthLeft = 6, BorderWidthRight = 6,
            CornerRadiusBottomLeft = 22, CornerRadiusBottomRight = 22, CornerRadiusTopLeft = 22, CornerRadiusTopRight = 22,
            ContentMarginLeft = 14, ContentMarginRight = 14, ContentMarginTop = 10, ContentMarginBottom = 14,
        });
        _root.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        _root.Position = new Vector2(-380, -300);
        _root.CustomMinimumSize = new Vector2(330, 600);
        AddChild(_root);
        var v = new VBoxContainer();
        _root.AddChild(v);
        _status = new Label();
        _status.AddThemeFontSizeOverride("font_size", 12);
        v.AddChild(_status);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        v.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 8);
        scroll.AddChild(_list);
    }

    public void Toggle()
    {
        _root.Visible = !_root.Visible;
        if (_root.Visible) Refresh();
    }

    public void Close() => _root.Visible = false;

    static string From(string f) => f switch
    {
        "memere" => "Memere",
        "news" => "News",
        "alert" => "Emergency alert",
        "social" => "Feed",
        "carrier" => "Carrier",
        _ => f,
    };

    void Refresh()
    {
        var w = World;
        if (w == null) return;
        var ph = w.Phone;
        _status.Text = $"{(w.PhoneHasService ? "▂▄▆ service" : "No service")}      {ph.Battery * 100:0}%{(w.PlayerIsHome && w.HousePowered ? " ⚡" : "")}";
        foreach (var c in _list.GetChildren()) c.QueueFree();
        if (ph.Battery <= 0)
        {
            _list.AddChild(new Label { Text = "Dead battery. Charge it at home when there's power." });
            return;
        }
        foreach (var m in ph.Inbox.AsEnumerable().Reverse())
        {
            var card = new PanelContainer();
            card.AddThemeStyleboxOverride("panel", new StyleBoxFlat
            {
                BgColor = m.From == "memere" ? new Color(0.35f, 0.18f, 0.2f) : m.From == "alert" ? new Color(0.4f, 0.12f, 0.1f) : new Color(0.16f, 0.17f, 0.2f),
                CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10, CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
                ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 6, ContentMarginBottom = 8,
            });
            var cv = new VBoxContainer();
            card.AddChild(cv);
            var head = new Label { Text = $"{From(m.From)}  ·  day {m.Day + 1}, {m.Hour:00}:{m.Minute:00}", Modulate = new Color(1, 1, 1, 0.6f) };
            head.AddThemeFontSizeOverride("font_size", 11);
            cv.AddChild(head);
            var body = new Label { Text = Dialogue?.Text(m.Line) ?? m.Line, AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(270, 0) };
            body.AddThemeFontSizeOverride("font_size", m.From == "memere" ? 26 : 14);
            cv.AddChild(body);
            _list.AddChild(card);
            m.Read = true;
        }
    }
}
