using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core.Data;
using ZTown.Core.Entities;

namespace ZTown.Game;

/// <summary>
/// New-game screen: name and look (skin, hair, clothes and colours), with a live preview that
/// turns and walks. Traits and occupation come in Phase 2 (FEATURES.md).
/// </summary>
public partial class CharacterCreator : CanvasLayer
{
    public GameData Data { get; set; } = null!;
    public Art Art { get; set; } = null!;
    public Action<string, Outfit>? OnStart { get; set; }

    Outfit _outfit = new();
    LineEdit _name = null!;
    Preview _preview = null!;
    readonly Dictionary<string, OptionButton> _pickers = new();
    readonly Dictionary<string, HBoxContainer> _swatches = new();
    readonly Random _rng = new();

    static readonly string[] Slots = { "hair", "hat", "top", "outer", "bottom", "shoes" };
    static readonly Dictionary<string, string> SlotNames = new()
    {
        ["hair"] = "Hair", ["hat"] = "Hat", ["top"] = "Top", ["outer"] = "Jacket", ["bottom"] = "Bottoms", ["shoes"] = "Shoes",
    };

    public override void _Ready()
    {
        _outfit = (Data.Clothing.Presets.GetValueOrDefault("player_default") ?? new Outfit()).Clone();

        var bg = new ColorRect { Color = new Color(0.07f, 0.065f, 0.06f, 0.96f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);

        var root = new HBoxContainer();
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.OffsetLeft = 60; root.OffsetTop = 40; root.OffsetRight = -60; root.OffsetBottom = -40;
        root.AddThemeConstantOverride("separation", 40);
        AddChild(root);

        _preview = new Preview { Art = Art, CustomMinimumSize = new Vector2(420, 600), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _preview.Outfit = _outfit;
        root.AddChild(_preview);

        var form = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        form.AddThemeConstantOverride("separation", 10);
        root.AddChild(form);

        var title = new Label { Text = "Who are you?" };
        title.AddThemeFontSizeOverride("font_size", 30);
        form.AddChild(title);
        var sub = new Label { Text = "Memere's grandchild. Name yourself and get dressed.", Modulate = new Color(1, 1, 1, 0.65f) };
        form.AddChild(sub);

        form.AddChild(Caption("Name"));
        _name = new LineEdit { Text = "Riley", PlaceholderText = "Your name", MaxLength = 24, CustomMinimumSize = new Vector2(320, 0), SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin };
        form.AddChild(_name);

        form.AddChild(Caption("Skin"));
        var skinRow = new HBoxContainer();
        form.AddChild(skinRow);
        foreach (var c in Data.Clothing.Palettes.GetValueOrDefault("skin") ?? new())
            skinRow.AddChild(Swatch(c, () => { _outfit.Skin = c; Changed(); }));

        foreach (var slot in Slots)
        {
            form.AddChild(Caption(SlotNames[slot]));
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 12);
            form.AddChild(row);
            var pick = new OptionButton { CustomMinimumSize = new Vector2(200, 0) };
            pick.AddItem("None");
            pick.SetItemMetadata(0, "");
            int i = 1;
            foreach (var g in Data.Clothing.ForSlot(slot))
            {
                pick.AddItem(g.Name);
                pick.SetItemMetadata(i++, g.Id);
            }
            string s = slot;
            pick.ItemSelected += idx => SetGarment(s, (string)pick.GetItemMetadata((int)idx));
            row.AddChild(pick);
            var sw = new HBoxContainer();
            row.AddChild(sw);
            _pickers[slot] = pick;
            _swatches[slot] = sw;
        }

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 16);
        form.AddChild(new Control { CustomMinimumSize = new Vector2(0, 12) });
        form.AddChild(buttons);
        var rnd = new Button { Text = "Randomize" };
        rnd.Pressed += Randomize;
        buttons.AddChild(rnd);
        var start = new Button { Text = "Start  ▶", CustomMinimumSize = new Vector2(160, 40) };
        start.Pressed += () => OnStart?.Invoke(string.IsNullOrWhiteSpace(_name.Text) ? "Riley" : _name.Text.Trim(), _outfit.Clone());
        buttons.AddChild(start);

        SyncPickers();
    }

    static Label Caption(string text)
    {
        var l = new Label { Text = text.ToUpperInvariant(), Modulate = new Color(0.85f, 0.75f, 0.55f) };
        l.AddThemeFontSizeOverride("font_size", 13);
        return l;
    }

    static Button Swatch(string hex, Action onClick)
    {
        var b = new Button { CustomMinimumSize = new Vector2(28, 28), TooltipText = hex };
        var style = new StyleBoxFlat { BgColor = CharacterPainter.Hex(hex), CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4 };
        b.AddThemeStyleboxOverride("normal", style);
        b.AddThemeStyleboxOverride("hover", new StyleBoxFlat { BgColor = CharacterPainter.Hex(hex).Lightened(0.15f), BorderColor = Colors.White, BorderWidthBottom = 2, BorderWidthTop = 2, BorderWidthLeft = 2, BorderWidthRight = 2 });
        b.AddThemeStyleboxOverride("pressed", style);
        b.Pressed += onClick;
        return b;
    }

    void SetGarment(string slot, string id)
    {
        if (string.IsNullOrEmpty(id)) _outfit.Slots.Remove(slot);
        else
        {
            var old = _outfit.In(slot);
            var pal = Data.Clothing.Palettes.GetValueOrDefault(Data.Clothing.Garment(id)?.Palette ?? "") ?? new() { "#808080" };
            _outfit.Slots[slot] = new Worn { Id = id, Color = old != null && pal.Contains(old.Color) ? old.Color : pal[0] };
        }
        Changed();
    }

    void Changed()
    {
        SyncPickers();
        _preview.Outfit = _outfit;
    }

    void SyncPickers()
    {
        foreach (var slot in Slots)
        {
            var pick = _pickers[slot];
            var worn = _outfit.In(slot);
            for (int i = 0; i < pick.ItemCount; i++)
                if ((string)pick.GetItemMetadata(i) == (worn?.Id ?? "")) pick.Select(i);
            var sw = _swatches[slot];
            foreach (var c in sw.GetChildren()) c.QueueFree();
            if (worn == null) continue;
            var pal = Data.Clothing.Palettes.GetValueOrDefault(Data.Clothing.Garment(worn.Id)?.Palette ?? "") ?? new();
            string s = slot;
            foreach (var hex in pal)
                sw.AddChild(Swatch(hex, () => { if (_outfit.In(s) is { } w) w.Color = hex; Changed(); }));
        }
    }

    void Randomize()
    {
        var c = Data.Clothing;
        string Pick(string pal) => c.Palettes.TryGetValue(pal, out var l) && l.Count > 0 ? l[_rng.Next(l.Count)] : "#808080";
        _outfit = new Outfit { Skin = Pick("skin") };
        foreach (var slot in Slots)
        {
            if (slot is "hat" or "outer" && _rng.NextDouble() < 0.6) continue;
            var opts = c.ForSlot(slot).ToList();
            var g = opts[_rng.Next(opts.Count)];
            _outfit.Slots[slot] = new Worn { Id = g.Id, Color = Pick(g.Palette) };
        }
        Changed();
    }

    /// <summary>The turning, walking preview.</summary>
    partial class Preview : Control
    {
        public Art Art { get; set; } = null!;
        public Outfit Outfit { get; set; } = new();
        double _t;

        public override void _Process(double delta)
        {
            _t += delta;
            QueueRedraw();
        }

        public override void _Draw()
        {
            var feet = new Vector2(Size.X / 2, Size.Y * 0.86f);
            DrawSetTransform(feet, 0, new Vector2(1, 0.45f));
            DrawCircle(Vector2.Zero, 90, new Color(0.18f, 0.2f, 0.14f));
            DrawCircle(Vector2.Zero, 52, new Color(0, 0, 0, 0.3f));
            DrawSetTransform(Vector2.Zero, 0, Vector2.One);
            int row = (int)(_t / 1.6) % 8;
            bool walking = (int)(_t / 6.4) % 2 == 1;
            string anim = walking ? "walk" : "idle";
            int frames = CharacterPainter.Frames(Art, anim);
            int frame = (int)(_t * (walking ? 10 : 3)) % frames;
            CharacterPainter.Draw(this, Art, Outfit, anim, frame, row, feet, Colors.White, 2.4f);
        }
    }
}
