using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;

namespace ZTown.Game;

/// <summary>Crafting and cooking (C), and your skills (K).</summary>
public partial class CraftingPanel : CanvasLayer
{
    public GameWorld? World { get; set; }
    public Art? Art { get; set; }
    public System.Action<string>? Toast { get; set; }
    PanelContainer _root = null!;
    VBoxContainer _list = null!;
    Label _title = null!;
    bool _skills;
    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        Layer = 3;
        _root = new PanelContainer { Visible = false };
        _root.AddThemeStyleboxOverride("panel", Hud.PanelStyle(0.94f));
        _root.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.Position = new Vector2(-300, -250);
        _root.CustomMinimumSize = new Vector2(600, 500);
        AddChild(_root);
        var v = new VBoxContainer();
        _root.AddChild(v);
        _title = new Label();
        _title.AddThemeFontSizeOverride("font_size", 20);
        v.AddChild(_title);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        v.AddChild(scroll);
        _list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(_list);
    }

    public void Show(bool skills)
    {
        if (_root.Visible && _skills == skills) { _root.Visible = false; return; }
        _skills = skills;
        _root.Visible = true;
        Refresh();
    }

    public void Close() => _root.Visible = false;

    void Refresh()
    {
        var w = World;
        if (w == null) return;
        foreach (var c in _list.GetChildren()) c.QueueFree();
        if (_skills)
        {
            var occ = w.Data.Character.Occupations.FirstOrDefault(o => o.Id == w.Profile.Occupation)?.Name ?? "";
            _title.Text = $"SKILLS  ·  {occ}  ·  K to close";
            foreach (var s in w.Data.Character.Skills)
            {
                int lvl = w.Skill(s);
                var row = new HBoxContainer();
                row.AddChild(new Label { Text = char.ToUpper(s[0]) + s[1..], CustomMinimumSize = new Vector2(160, 0) });
                row.AddChild(new Label { Text = new string('■', lvl) + new string('□', 10 - lvl) });
                _list.AddChild(row);
            }
            var traits = string.Join(", ", w.Profile.Traits.Select(t => w.Data.Character.Trait(t)?.Name ?? t));
            _list.AddChild(new Label { Text = traits.Length > 0 ? "Traits: " + traits : "No traits", AutowrapMode = TextServer.AutowrapMode.WordSmart });
            return;
        }
        _title.Text = "CRAFTING  ·  C to close";
        foreach (var r in w.Data.Recipes.Values)
        {
            var why = w.CannotCraft(r);
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 10);
            row.AddChild(new TextureRect
            {
                Texture = Art?.Items.GetValueOrDefault(r.Output.Item), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                CustomMinimumSize = new Vector2(40, 40), StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            });
            var info = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            info.AddChild(new Label { Text = r.Name });
            var needs = string.Join(" + ", r.Inputs.Select(i => $"{i.Count}× {w.Data.Item(i.Item)?.Name ?? i.Item}"));
            if (r.Tools.Count > 0) needs += "   (tools: " + string.Join(", ", r.Tools.Select(t => w.Data.Item(t)?.Name ?? t)) + ")";
            if (r.Station != null) needs += $"   at a {r.Station}";
            var nl = new Label { Text = needs, Modulate = new Color(1, 1, 1, 0.6f) };
            nl.AddThemeFontSizeOverride("font_size", 12);
            info.AddChild(nl);
            row.AddChild(info);
            var b = new Button { Text = why == null ? "Make" : why, Disabled = why != null, CustomMinimumSize = new Vector2(170, 0) };
            var recipe = r;
            b.Pressed += () =>
            {
                if (w.Craft(recipe)) Toast?.Invoke($"Made: {w.Data.Item(recipe.Output.Item)?.Name}");
                Refresh();
            };
            row.AddChild(b);
            _list.AddChild(row);
        }
    }
}
