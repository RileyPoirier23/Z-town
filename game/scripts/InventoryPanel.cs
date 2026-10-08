using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Items;
using Container = Godot.Container;
using LootContainer = ZTown.Core.Items.Container;

namespace ZTown.Game;

/// <summary>Placeholder inventory: your stuff on the left, the open container on the right.
/// Drag-and-drop and the real look come in Phase 1's UI pass.</summary>
public partial class InventoryPanel : CanvasLayer
{
    public GameWorld World { get; set; } = null!;
    public LootContainer? Open { get; private set; }
    public ItemStack? Equipped { get; set; }
    public Action<string>? Toast { get; set; }
    public Art? Art { get; set; }
    public Action<string>? Sound { get; set; }

    PanelContainer _root = null!;
    ItemList _mine = null!, _theirs = null!;
    Label _mineTitle = null!, _theirsTitle = null!;

    public bool IsOpen => _root.Visible;

    public override void _Ready()
    {
        _root = new PanelContainer { Visible = false };
        _root.AddThemeStyleboxOverride("panel", Hud.PanelStyle(0.92f));
        _root.SetAnchorsPreset(Control.LayoutPreset.Center);
        _root.Position = new Vector2(-420, -230);
        _root.CustomMinimumSize = new Vector2(840, 460);
        AddChild(_root);
        var h = new HBoxContainer();
        _root.AddChild(h);

        (_mineTitle, _mine) = Column(h);
        (_theirsTitle, _theirs) = Column(h);

        var buttons = new VBoxContainer { CustomMinimumSize = new Vector2(170, 0) };
        h.AddChild(buttons);
        Button(buttons, "Take →  /  ← Put", Transfer);
        Button(buttons, "Eat / drink", () => Act(s =>
        {
            bool drink = World.Data.Item(s.ItemId)?.Thirst > World.Data.Item(s.ItemId)?.Hunger;
            bool ok = World.Consume(s);
            if (ok) Sound?.Invoke(drink ? "drink" : "eat");
            return ok;
        }, "Can't eat or drink that."));
        Button(buttons, "Give to memere", () => Act(s => World.GiveToMemere(s), "Get closer to memere, or that's not for her."));
        Button(buttons, "Equip / unequip", Equip);
        Button(buttons, "Pour gas (generator / car)", () => Act(s =>
            World.Refuel(s) || (World.VehicleNearPlayer() is { } car && World.RefuelVehicle(car, s)), "Needs a gas can, next to the generator or a car."));
        Button(buttons, "Close (Tab)", Close);
    }

    (Label, ItemList) Column(HBoxContainer parent)
    {
        var v = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var l = new Label();
        var list = new ItemList { SizeFlagsVertical = Control.SizeFlags.ExpandFill, CustomMinimumSize = new Vector2(300, 380), FixedIconSize = new Vector2I(32, 32) };
        v.AddChild(l);
        v.AddChild(list);
        parent.AddChild(v);
        return (l, list);
    }

    static void Button(Container parent, string text, Action onClick)
    {
        var b = new Button { Text = text };
        b.Pressed += onClick;
        parent.AddChild(b);
    }

    public void Show(LootContainer? c)
    {
        Open = c;
        _root.Visible = true;
        Refresh();
    }

    public void Close()
    {
        _root.Visible = false;
        Open = null;
    }

    public override void _Process(double delta)
    {
        if (!_root.Visible) return;
        if (Open != null && !World.PlayerCanReach(Open.Pos)) Open = null;
        Refresh();
    }

    string Describe(ItemStack s)
    {
        var d = World.Data.Item(s.ItemId);
        var name = d?.Name ?? s.ItemId;
        if (d?.Brand != null && World.Data.Brands.TryGetValue(d.Brand, out var b) && b.Status != "approved") name += " [DRAFT BRAND]";
        var extra = s.Count > 1 ? $" ×{s.Count}" : "";
        if (d != null && d.Uses > 1) extra += $" ({s.UsesLeft}/{d.Uses})";
        if (d?.Weapon != null) extra += $" ({s.Condition * 100:0}%)";
        if (d?.Fuel > 0) extra += $" ({d.Fuel * s.Condition:0.0} L)";
        if (World.IsSpoiled(s)) extra += " — spoiled";
        if (s == Equipped) extra += "  [equipped]";
        return name + extra;
    }

    int _lastMine = -1, _lastTheirs = -1;

    void Refresh()
    {
        var inv = World.Player.Inventory;
        _mineTitle.Text = $"You  ({inv.Weight(World.Data.Item):0.0} / {inv.Capacity:0} kg)";
        Fill(_mine, inv, ref _lastMine);
        if (Open != null)
        {
            _theirsTitle.Text = $"{Open.Kind}  ({Open.Inventory.Weight(World.Data.Item):0.0} kg)";
            Fill(_theirs, Open.Inventory, ref _lastTheirs);
        }
        else
        {
            _theirsTitle.Text = "Nothing open (E on a container)";
            _theirs.Clear();
            _lastTheirs = -1;
        }
    }

    void Fill(ItemList list, Inventory inv, ref int lastHash)
    {
        int hash = inv.Stacks.Aggregate(17, (h, s) => h * 31 + Describe(s).GetHashCode());
        if (hash == lastHash) return;
        lastHash = hash;
        int sel = list.GetSelectedItems().FirstOrDefault(-1);
        list.Clear();
        foreach (var s in inv.Stacks) list.AddItem(Describe(s), Art?.Items.GetValueOrDefault(s.ItemId));
        if (sel >= 0 && sel < list.ItemCount) list.Select(sel);
    }

    ItemStack? Selected(ItemList list, Inventory inv)
    {
        var sel = list.GetSelectedItems();
        return sel.Length > 0 && sel[0] < inv.Stacks.Count ? inv.Stacks[sel[0]] : null;
    }

    void Transfer()
    {
        if (Open != null && Selected(_theirs, Open.Inventory) is { } take)
        {
            if (!World.TakeFrom(Open, take)) Toast?.Invoke("Too heavy to carry.");
            else Sound?.Invoke("pickup");
            _theirs.DeselectAll();
            return;
        }
        if (Open != null && Selected(_mine, World.Player.Inventory) is { } put)
        {
            if (put == Equipped) Equipped = null;
            if (!World.PutInto(Open, put)) Toast?.Invoke("Doesn't fit.");
            _mine.DeselectAll();
        }
    }

    void Act(Func<ItemStack, bool> action, string fail)
    {
        if (Selected(_mine, World.Player.Inventory) is not { } s) return;
        if (!action(s)) Toast?.Invoke(fail);
        if (!World.Player.Inventory.Stacks.Contains(s) && s == Equipped) Equipped = null;
        _lastMine = -1;
    }

    void Equip()
    {
        if (Selected(_mine, World.Player.Inventory) is not { } s) return;
        Equipped = Equipped == s ? null : World.Data.Item(s.ItemId)?.Weapon != null ? s : Equipped;
        _lastMine = -1;
    }
}
