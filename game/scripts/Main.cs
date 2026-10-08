using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Data;
using ZTown.Core.Dialogue;
using ZTown.Core.Map;
using ZTown.Core.Save;

namespace ZTown.Game;

/// <summary>
/// Boots the game: loads data and dialogue, builds the world, wires input to the simulation,
/// and handles saving. Phase 0 uses the stand-in test house; Phase 1 swaps in memere's real
/// house and block.
/// </summary>
public partial class Main : Node2D
{
    const string SavePath = "user://saves/slot1.sav";

    GameData _data = null!;
    DialogueDb _dialogue = null!;
    GameWorld _world = null!;
    IsoView _view = null!;
    Camera2D _camera = null!;
    Hud _hud = null!;
    InventoryPanel _inventory = null!;
    float _speed = 1f;
    bool _wasHome = true;
    int _messagesSeen;
    float _attackCooldown;

    public override void _Ready()
    {
        var src = new GodotDataSource();
        _data = GameData.Load(src);
        _dialogue = DialogueDb.Load(src);
        // unapproved lines always carry [DRAFT]; release builds can't contain any (CI's release check)
        _dialogue.ShowDraftTags = true;
        foreach (var p in _data.Problems.Concat(_dialogue.Problems)) GD.PushWarning(p);

        RegisterInput();

        _view = new IsoView();
        AddChild(_view);
        _camera = new Camera2D { Zoom = new Vector2(0.7f, 0.7f), PositionSmoothingEnabled = true, PositionSmoothingSpeed = 8 };
        AddChild(_camera);
        _hud = new Hud { Dialogue = _dialogue, Version = "v" + (string)ProjectSettings.GetSetting("application/config/version") };
        AddChild(_hud);
        _inventory = new InventoryPanel { Toast = t => _hud.Say("", t) };
        AddChild(_inventory);

        NewGame((ulong)DateTime.UtcNow.Ticks);

        // dev: `godot -- --screenshot=out.png` saves a frame and quits (used to preview builds)
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--screenshot=")) _screenshotPath = arg["--screenshot=".Length..];
    }

    string? _screenshotPath;
    int _frames;

    GameWorld CreateBase(ulong seed) => TestMaps.CreateTestWorld(_data, seed, zombies: 0);

    void NewGame(ulong seed)
    {
        Attach(TestMaps.CreateTestWorld(_data, seed, zombies: 10));
        _world.Player.Inventory.TryAdd(_data.Item("baseball_bat")!, 1, _data.Item);
        _inventory.Equipped = _world.Player.Inventory.Stacks.FirstOrDefault();
        Save();
    }

    void Attach(GameWorld w)
    {
        _world = w;
        _world.RemoveDeadZombies = false;
        _view.World = w;
        _hud.World = w;
        _inventory.World = w;
        _inventory.Close();
        _messagesSeen = w.Messages.Count;
        _camera.Position = Iso.ToScreen(w.Player.X, w.Player.Y, w.Player.Z) + new Vector2(0, -80);
        _camera.ResetSmoothing();
    }

    // ------------------------------------------------------------------ saving

    public override void _Notification(int what)
    {
        // closing the window (or the updater asking the game to close) saves first
        if (what == NotificationWMCloseRequest && _world != null && !_world.PlayerDied) Save();
    }

    void Save()
    {
        DirAccess.MakeDirRecursiveAbsolute("user://saves");
        var json = SaveSystem.ToJson(SaveSystem.Capture(_world, (string)ProjectSettings.GetSetting("application/config/version")));
        using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Write);
        f.StoreBuffer(SaveSystem.Compress(json));
    }

    bool LoadLastSave()
    {
        if (!FileAccess.FileExists(SavePath)) return false;
        try
        {
            using var f = FileAccess.Open(SavePath, FileAccess.ModeFlags.Read);
            var json = SaveSystem.Decompress(f.GetBuffer((long)f.GetLength()));
            Attach(SaveSystem.Restore(SaveSystem.FromJson(json), CreateBase));
            _inventory.Equipped = _world.Player.Inventory.Stacks.FirstOrDefault(s => _data.Item(s.ItemId)?.Weapon != null);
            return true;
        }
        catch (Exception e)
        {
            GD.PushError("Couldn't load save: " + e.Message);
            return false;
        }
    }

    // ------------------------------------------------------------------ input

    static void RegisterInput()
    {
        void Key(string action, params Key[] keys)
        {
            if (!InputMap.HasAction(action)) InputMap.AddAction(action);
            foreach (var k in keys) InputMap.ActionAddEvent(action, new InputEventKey { PhysicalKeycode = k });
        }
        Key("move_up", Godot.Key.W, Godot.Key.Up);
        Key("move_down", Godot.Key.S, Godot.Key.Down);
        Key("move_left", Godot.Key.A, Godot.Key.Left);
        Key("move_right", Godot.Key.D, Godot.Key.Right);
        Key("run", Godot.Key.Shift);
        Key("sneak", Godot.Key.Ctrl);
        Key("use", Godot.Key.E);
        Key("attack", Godot.Key.Space);
        Key("inventory", Godot.Key.Tab, Godot.Key.I);
        Key("channel", Godot.Key.C);
        Key("sleep", Godot.Key.Z);
        Key("quicksave", Godot.Key.F5);
        Key("quickload", Godot.Key.F9);
        Key("faster", Godot.Key.Equal, Godot.Key.KpAdd);
        Key("slower", Godot.Key.Minus, Godot.Key.KpSubtract);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventMouseButton mb && mb.Pressed)
        {
            if (mb.ButtonIndex == MouseButton.WheelUp) _camera.Zoom = (_camera.Zoom * 1.1f).Clamp(new Vector2(0.3f, 0.3f), new Vector2(2, 2));
            if (mb.ButtonIndex == MouseButton.WheelDown) _camera.Zoom = (_camera.Zoom / 1.1f).Clamp(new Vector2(0.3f, 0.3f), new Vector2(2, 2));
        }
        if (e is InputEventKey { Pressed: true } && _world.Player.Asleep && !e.IsAction("sleep")) _world.Player.Asleep = false;
        if (e.IsActionPressed("inventory"))
        {
            if (_inventory.IsOpen) _inventory.Close();
            else _inventory.Show(null);
        }
        if (e.IsActionPressed("use")) Use();
        if (e.IsActionPressed("channel") && NearTv()) _world.NextChannel();
        if (e.IsActionPressed("sleep"))
        {
            if (_world.PlayerIsHome) { Save(); _world.Sleep(); }
            else _hud.Say("", "Not here. Sleep at memere's.");
        }
        if (e.IsActionPressed("quicksave")) { Save(); _hud.Say("", "Saved."); }
        if (e.IsActionPressed("quickload") && LoadLastSave()) _hud.Say("", "Loaded your last save.");
        if (e.IsActionPressed("faster")) _speed = Mathf.Min(_speed * 2, 16);
        if (e.IsActionPressed("slower")) _speed = Mathf.Max(_speed / 2, 1);
    }

    // ------------------------------------------------------------------ frame

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        var w = _world;
        var p = w.Player;

        if (!_inventory.IsOpen && !p.Asleep)
        {
            // screen directions → tile directions (screen up = tile -x -y)
            var input = Input.GetVector("move_left", "move_right", "move_up", "move_down");
            if (input.LengthSquared() > 0.01f)
            {
                var mode = Input.IsActionPressed("run") ? MoveMode.Run : Input.IsActionPressed("sneak") ? MoveMode.Sneak : MoveMode.Walk;
                float tx = input.X + input.Y, ty = input.Y - input.X;
                w.MovePlayer(tx, ty, mode, dt);
            }
            _attackCooldown -= dt;
            if (Input.IsActionPressed("attack") && _attackCooldown <= 0)
            {
                var weapon = _inventory.Equipped;
                _attackCooldown = weapon != null ? _data.Item(weapon.ItemId)?.Weapon?.SwingSeconds ?? 0.8f : 0.6f;
                w.PlayerAttack(weapon);
                if (weapon != null && !p.Inventory.Stacks.Contains(weapon)) { _inventory.Equipped = null; _hud.Say("", "Your weapon broke."); }
            }
        }

        float scale = p.Asleep ? 30f : _speed;
        w.Tick(dt * scale);

        // memere and the world talking
        for (; _messagesSeen < w.Messages.Count; _messagesSeen++)
        {
            var m = w.Messages[_messagesSeen];
            var line = _dialogue.Lines.GetValueOrDefault(m.DialogueId);
            _hud.Say(line?.Speaker == "memere" ? "Memere" : line?.Speaker ?? "", _dialogue.Text(m.DialogueId));
        }

        // coming home saves the game
        bool home = w.PlayerIsHome;
        if (home && !_wasHome) Save();
        _wasHome = home;

        if (w.PlayerDied)
        {
            if (!LoadLastSave()) NewGame((ulong)DateTime.UtcNow.Ticks);
            _hud.Say("", "You died. Back to your last save.");
            return;
        }

        _camera.Position = Iso.ToScreen(p.X, p.Y, p.Z) + new Vector2(0, -80);
        var mouseTile = Iso.ToTile(GetGlobalMousePosition());
        _view.HoverTile = new Vector2I(Mathf.FloorToInt(mouseTile.X), Mathf.FloorToInt(mouseTile.Y));
        _hud.SetPrompt(Prompt());

        if (_screenshotPath != null && ++_frames == 90)
        {
            GetViewport().GetTexture().GetImage().SavePng(_screenshotPath);
            GetTree().Quit();
        }
    }

    // ------------------------------------------------------------------ interaction

    bool NearTv() => _world.Map.AllTiles().Any(t => _world.Map.At(t).Object == "tv" && _world.PlayerCanReach(t));

    ZTown.Core.Items.Container? NearestContainer() =>
        _world.Containers.Values.Where(c => _world.PlayerCanReach(c.Pos))
            .OrderBy(c => new Vector2(c.Pos.X + 0.5f - _world.Player.X, c.Pos.Y + 0.5f - _world.Player.Y).Length()).FirstOrDefault();

    /// <summary>The door/window edge on the player's tile in the direction they're facing.</summary>
    (TilePos a, TilePos b)? FacingEdge()
    {
        var p = _world.Player;
        var t = p.Tile;
        var dirs = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
        var fx = Mathf.Cos(p.Facing);
        var fy = Mathf.Sin(p.Facing);
        foreach (var (dx, dy) in dirs.OrderByDescending(d => d.Item1 * fx + d.Item2 * fy))
        {
            var n = new TilePos(t.X + dx, t.Y + dy, t.Z);
            if (!_world.Map.InBounds(n)) continue;
            var e = _world.Map.EdgeBetween(t, n);
            if (e is Edge.DoorClosed or Edge.DoorOpen or Edge.WindowClosed or Edge.WindowOpen) return (t, n);
        }
        return null;
    }

    string Prompt()
    {
        if (_world.Player.Asleep) return "Sleeping…";
        if (_world.PlayerNearMemere) return "E: give memere something";
        if (_world.PlayerNearGenerator && _world.Power.Generator.Present) return "E: generator (on/off, connect)";
        if (NearTv()) return "E: TV on/off   C: change channel";
        if (NearestContainer() is { } c) return $"E: search {c.Kind}";
        if (FacingEdge() is { } edge) return $"E: {(_world.Map.EdgeBetween(edge.a, edge.b).ToString().Contains("Closed") ? "open" : "close")} {(_world.Map.EdgeBetween(edge.a, edge.b).ToString().StartsWith("Door") ? "door" : "window")}";
        return "";
    }

    void Use()
    {
        var w = _world;
        if (w.PlayerNearMemere) { _inventory.Show(null); _hud.Say("", "Pick something and press \"Give to memere\"."); return; }
        if (w.PlayerNearGenerator && w.Power.Generator.Present)
        {
            if (!w.Power.Generator.Connected) { w.ToggleGeneratorConnected(); _hud.Say("", "Generator connected to the house."); }
            else if (!w.ToggleGenerator()) _hud.Say("", w.Power.Generator.Fuel <= 0 ? "No gas in it." : "It won't start.");
            return;
        }
        if (NearTv()) { w.ToggleTv(); return; }
        if (NearestContainer() is { } c) { _inventory.Show(w.OpenContainer(c.Id)); return; }
        if (FacingEdge() is { } edge) w.ToggleEdge(edge.a, edge.b);
    }
}
