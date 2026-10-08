using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using ZTown.Core;
using ZTown.Core.Data;
using ZTown.Core.Dialogue;
using ZTown.Core.Map;
using ZTown.Core.Save;
using ZTown.Core.Story;

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
    bool Playing => _world != null && _menu == null;
    IsoView _view = null!;
    Camera2D _camera = null!;
    Hud _hud = null!;
    InventoryPanel _inventory = null!;
    WorldMap _map = null!;
    AudioManager _audio = null!;
    PhonePanel _phone = null!;
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
        _worldCfg = MapBuilder.LoadConfig(src);

        _view = new IsoView { Art = Art.Load() };
        AddChild(_view);
        _camera = new Camera2D { Zoom = new Vector2(0.85f, 0.85f), PositionSmoothingEnabled = true, PositionSmoothingSpeed = 8 };
        AddChild(_camera);
        _hud = new Hud { Dialogue = _dialogue, Art = _view.Art, Version = "v" + (string)ProjectSettings.GetSetting("application/config/version") };
        _hud.EquippedItem = () => _inventory?.Equipped?.ItemId;
        AddChild(_hud);
        _inventory = new InventoryPanel { Toast = t => _hud.Say("", t), Art = _view.Art };
        AddChild(_inventory);
        _map = new WorldMap();
        AddChild(_map);
        _audio = new AudioManager();
        AddChild(_audio);
        _inventory.Sound = k => _audio.Play(k, Iso.ToScreen(_world.Player.X, _world.Player.Y), -4);
        _phone = new PhonePanel { Dialogue = _dialogue };
        AddChild(_phone);

        var args = OS.GetCmdlineUserArgs();
        if (args.Contains("--quickstart") || args.Contains("--demo") || args.Contains("--demo-inside") || args.Contains("--demo-car")) NewGame(42, "Riley", null);
        else ShowTitle();
        if (args.Contains("--demo")) DemoSetup(true);
        if (args.Contains("--demo-inside")) DemoSetup(false);
        if (args.Contains("--demo-car"))
        {
            DemoSetup(true);
            var car = _world.Vehicles.OrderBy(v => Mathf.Abs(v.X - _world.Player.X) + Mathf.Abs(v.Y - _world.Player.Y)).First();
            _world.Player.X = car.X; _world.Player.Y = car.Y + 2;
            _world.EnterOrExitVehicle();
            _demoDrive = true;
        }
        if (args.Contains("--creator")) ShowCreator();

        // dev: `godot -- --screenshot=out.png` saves a frame and quits (used to preview builds)
        foreach (var arg in OS.GetCmdlineUserArgs())
        {
            if (arg.StartsWith("--screenshot=")) _screenshotPath = arg["--screenshot=".Length..];
            if (arg.StartsWith("--frames=")) _shotFrame = int.Parse(arg["--frames=".Length..]);
            if (arg == "--open-map") _openMapAt = 5;
            if (arg == "--open-phone") _openPhone = true;
        }
    }

    string? _screenshotPath;
    int _frames, _shotFrame = 90, _openMapAt = -1;
    bool _openPhone, _demoDrive;

    WorldConfig _worldCfg = new();
    MapFile? _mapFile;

    /// <summary>The real map (OpenStreetMap) if it's been converted, else the stand-in test street.</summary>
    GameWorld BuildWorld(ulong seed, string? mapName = null, bool zombies = true)
    {
        mapName ??= _worldCfg.Map;
        if (mapName != "test" && FileAccess.FileExists($"res://maps/{mapName}.map.json"))
        {
            if (_mapFile?.Name != mapName) _mapFile = MapFile.Load(new GodotDataSource(), mapName);
            return MapBuilder.Build(_data, _mapFile, _worldCfg, seed);
        }
        return TestMaps.CreateTestWorld(_data, seed, zombies ? 10 : 0);
    }

    GameWorld CreateBase(SaveData s) => BuildWorld(s.Seed, s.Map, zombies: false);

    CanvasLayer? _menu;

    /// <summary>dev: `-- --demo` puts the player on the street outside memere's with a few zombies
    /// around; `-- --demo-inside` leaves them in the house. For previews.</summary>
    void DemoSetup(bool outside)
    {
        var w = _world;
        w.Player.Needs.Hunger = 0.5f;
        w.Player.Needs.Fatigue = 0.3f;
        w.Player.Needs.Stress = 0.75f;
        w.Memere.Supplies.Amounts["cigarettes"] = 6;
        if (outside)
        {
            // nearest sidewalk to the house
            var start = w.Memere.Tile;
            var seen = new HashSet<TilePos> { start };
            var q = new Queue<TilePos>();
            q.Enqueue(start);
            TilePos spot = start;
            while (q.Count > 0)
            {
                var p = q.Dequeue();
                if (w.Map.At(p).Floor is "sidewalk" && w.Map.At(p).Building == 0 && !w.Map.At(p).Solid
                    && Enumerable.Range(1, 3).Any(k => w.Map.InBounds(new TilePos(p.X + k, p.Y)) && w.Map.At(p.X + k, p.Y).Floor is "asphalt" or "road_line" or "road_line_y"
                                                   || w.Map.InBounds(new TilePos(p.X, p.Y + k)) && w.Map.At(p.X, p.Y + k).Floor is "asphalt" or "road_line" or "road_line_y"))
                { spot = p; break; }
                foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    var n = new TilePos(p.X + dx, p.Y + dy);
                    if (w.Map.InBounds(n) && seen.Add(n) && n.DistanceTo(start) < 90) q.Enqueue(n);
                }
            }
            w.Player.PlaceAt(spot);
            w.Player.Facing = Mathf.Pi * 0.25f;
            foreach (var (dx, dy) in new[] { (12, 6), (14, -3), (6, 13), (16, 8), (-9, 12) })
            {
                var z = w.TrySpawnZombie(new TilePos(spot.X + dx, spot.Y + dy));
                if (z != null) z.Facing = Mathf.Atan2(-dy, -dx);
            }
            _wasHome = false;
        }
        _camera.Position = Iso.ToScreen(w.Player.X, w.Player.Y) + new Vector2(0, -80);
        _camera.ResetSmoothing();
    }

    void CloseMenu()
    {
        _menu?.QueueFree();
        _menu = null;
    }

    void ShowTitle()
    {
        CloseMenu();
        var layer = new CanvasLayer { Layer = 10 };
        var bg = new ColorRect { Color = new Color(0.07f, 0.065f, 0.06f, 0.9f) };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(bg);
        var box = new VBoxContainer();
        box.SetAnchorsPreset(Control.LayoutPreset.Center);
        box.Position = new Vector2(-160, -120);
        box.CustomMinimumSize = new Vector2(320, 0);
        box.AddThemeConstantOverride("separation", 12);
        layer.AddChild(box);
        var title = new Label { Text = "Memere", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 48);
        box.AddChild(title);
        var sub = new Label { Text = "(working title)", HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.5f) };
        box.AddChild(sub);
        if (FileAccess.FileExists(SavePath))
        {
            var cont = new Button { Text = "Continue" };
            cont.Pressed += () => { CloseMenu(); if (!LoadLastSave()) ShowCreator(); };
            box.AddChild(cont);
        }
        var nw = new Button { Text = "New game" };
        nw.Pressed += ShowCreator;
        box.AddChild(nw);
        var quit = new Button { Text = "Quit" };
        quit.Pressed += () => GetTree().Quit();
        box.AddChild(quit);
        var ver = new Label { Text = "v" + (string)ProjectSettings.GetSetting("application/config/version"), HorizontalAlignment = HorizontalAlignment.Center, Modulate = new Color(1, 1, 1, 0.4f) };
        box.AddChild(ver);
        AddChild(layer);
        _menu = layer;
    }

    void ShowCreator()
    {
        CloseMenu();
        var cc = new CharacterCreator { Data = _data, Art = _view.Art!, Layer = 10 };
        cc.OnStart = (name, outfit) => { CloseMenu(); NewGame((ulong)DateTime.UtcNow.Ticks, name, outfit); };
        AddChild(cc);
        _menu = cc;
    }

    void NewGame(ulong seed, string name, ZTown.Core.Entities.Outfit? outfit)
    {
        Attach(BuildWorld(seed));
        _world.Player.Name = name;
        _world.Player.Outfit = outfit ?? (_data.Clothing.Presets.GetValueOrDefault("player_default")?.Clone() ?? new());
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
        _map.World = w;
        _map.Close();
        _audio.World = w;
        _audio.ResetWorld();
        _phone.World = w;
        _phone.Close();
        _hud.ResetNotifications(w.Notifications.Count);
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
            var save = SaveSystem.FromJson(json);
            Attach(SaveSystem.Restore(save, sd => CreateBase(save)));
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
        Key("map", Godot.Key.M);
        Key("bandage", Godot.Key.Q);
        Key("phone", Godot.Key.P);
        Key("quest_next", Godot.Key.J);
        Key("barricade", Godot.Key.B);
        Key("unbarricade", Godot.Key.X);
        Key("close", Godot.Key.Escape);
        Key("slower", Godot.Key.Minus, Godot.Key.KpSubtract);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!Playing) return;
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
        if (e.IsActionPressed("map")) { _map.Toggle(); _phone.Close(); }
        if (e.IsActionPressed("phone")) { _phone.Toggle(); _map.Close(); }
        if (e.IsActionPressed("close")) { _map.Close(); _phone.Close(); _inventory.Close(); }
        if (e.IsActionPressed("quest_next"))
        {
            var act = _world.Quests.Active.Select(q => q.Id).ToList();
            if (act.Count > 0)
            {
                int i = act.IndexOf(_world.Quests.Tracked ?? "");
                _world.Quests.Tracked = act[(i + 1) % act.Count];
            }
        }
        if (e.IsActionPressed("barricade") && FacingEdge(includeBroken: true) is { } be)
        {
            if (!_world.Barricade(be.a, be.b))
                _hud.Say("", _world.Map.BarricadeBetween(be.a, be.b) >= 4 ? "Can't fit more planks." : "Need a hammer, a plank and nails (and the door shut).");
        }
        if (e.IsActionPressed("unbarricade") && FacingEdge(includeBroken: true) is { } ue && !_world.RemoveBarricade(ue.a, ue.b))
            _hud.Say("", "Need a hammer or crowbar to pry planks off.");
        if (e.IsActionPressed("bandage"))
        {
            var bw = _world.BandageSelf();
            _hud.Say("", bw != null ? $"Bandaged the {bw.Kind.ToString().ToLowerInvariant()} on your {bw.Part}." : _world.Player.Wounds.List.All(x => x.Bandaged) ? "Nothing to bandage." : "You need a bandage.");
        }
        if (_map.IsOpen) return;
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
        _hud.Visible = Playing;
        if (!Playing)
        {
            if (_screenshotPath != null && ++_frames == 90) { GetViewport().GetTexture().GetImage().SavePng(_screenshotPath); GetTree().Quit(); }
            return;
        }
        float dt = (float)delta;
        var w = _world;
        var p = w.Player;

        if (w.Driving != null && !_inventory.IsOpen && !_map.IsOpen)
        {
            float throttle = (Input.IsActionPressed("move_up") ? 1 : 0) - (Input.IsActionPressed("move_down") ? 1 : 0);
            float steer = (Input.IsActionPressed("move_right") ? 1 : 0) - (Input.IsActionPressed("move_left") ? 1 : 0);
            if (_demoDrive) throttle = 1;
            w.DriveInput(throttle, steer, Input.IsActionPressed("attack"), dt);
        }
        else if (!_inventory.IsOpen && !_map.IsOpen && !p.Asleep)
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
                _view.PlayerSwing = 0.45f;
                _view.PlayerHasWeapon = weapon != null;
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
            var text = _dialogue.Text(m.DialogueId);
            if (m.Channel == "phone") _audio.PhoneBuzz();
            if (m.Channel == "phone")
                _hud.Toast($"📱 {(line?.Speaker == "memere" ? "Memere" : "Phone")}: {text}");
            else if (line?.Speaker == "memere") _view.Speak(w.Memere.Id, text, 6);
            else if (m.Channel == "tv") _hud.Say("TV", text);
            else if (line?.Speaker == "dad" && w.Dad != null) _view.Speak(w.Dad.Id, text, 6);
            else _hud.Say(line?.Speaker ?? "", text);
        }

        // coming home saves the game
        bool home = w.PlayerIsHome;
        if (home && !_wasHome) Save();
        _wasHome = home;

        if (w.PlayerDied || w.DadDied)
        {
            bool dad = w.DadDied && !w.PlayerDied;
            if (!LoadLastSave()) NewGame((ulong)DateTime.UtcNow.Ticks, _world.Player.Name, _world.Player.Outfit);
            _hud.Say("", dad ? "Dad didn't make it. Back to your last save." : "You died. Back to your last save.");
            return;
        }

        // the tracked quest's marker
        var tq = w.Quests.Tracked != null ? w.Quests.Quests.GetValueOrDefault(w.Quests.Tracked) : null;
        var td = tq != null ? _data.Quests.GetValueOrDefault(tq.Id) : null;
        _view.QuestMarker = td != null && tq!.Step < td.Steps.Count && td.Steps[tq.Step].Marker is { } mk ? QuestLog.Marker(w, mk) : null;

        _camera.Position = Iso.ToScreen(p.X, p.Y, p.Z) + new Vector2(0, -80);
        var mouseTile = Iso.ToTile(GetGlobalMousePosition());
        _view.HoverTile = new Vector2I(Mathf.FloorToInt(mouseTile.X), Mathf.FloorToInt(mouseTile.Y));
        _hud.SetPrompt(Prompt());

        if (_frames == _openMapAt) _map.Toggle();
        if (_openPhone && _frames == _shotFrame - 3) _phone.Toggle();
        if (_screenshotPath != null && ++_frames == _shotFrame)
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
    (TilePos a, TilePos b)? FacingEdge(bool includeBroken = false)
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
            if (includeBroken && e is Edge.DoorBroken or Edge.WindowBroken) return (t, n);
        }
        return null;
    }

    string Prompt()
    {
        if (_world.Player.Asleep) return "Sleeping…";
        if (_world.Driving is { } car)
            return $"{Mathf.Abs(car.Speed) * 3.6f:0} km/h   ·   gas {car.Fuel:0.0}/{car.FuelMax:0} L   ·   {(car.EngineOn ? "W/S drive, A/D steer, Space brake" : "won't start")}   ·   E: get out";
        if (_world.PlayerNearDad && _world.Dad is { } dad)
            return dad.State switch
            {
                ZTown.Core.Entities.DadState.OutOfIt => "E: wake Dad up",
                ZTown.Core.Entities.DadState.Following => "E: tell Dad to wait here",
                ZTown.Core.Entities.DadState.Home => "E: talk to Dad",
                _ => "E: tell Dad to come with you",
            };
        if (_world.PlayerNearMemere) return "E: give memere something";
        if (_world.PlayerNearGenerator && _world.Power.Generator.Present) return "E: generator (on/off, connect)";
        if (NearTv()) return "E: TV on/off   C: change channel";
        if (NearestContainer() is { } c) return $"E: search {(c.Kind == "trunk" ? "the trunk" : c.Kind)}";
        if (_world.VehicleNearPlayer() is { } v) return $"E: get in the {v.Model}{(v.HasKeys ? " (keys inside)" : " (no keys)")}";
        if (FacingEdge(includeBroken: true) is { } edge)
        {
            var ed = _world.Map.EdgeBetween(edge.a, edge.b);
            int planks = _world.Map.BarricadeBetween(edge.a, edge.b);
            string what = ed.ToString().StartsWith("Door") ? "door" : "window";
            if (planks > 0) return $"Boarded ({planks}/4)   B: add plank   X: pry one off";
            if (ed is Edge.DoorBroken or Edge.WindowBroken) return $"Broken {what}   B: board it up";
            return $"E: {(ed.ToString().Contains("Closed") ? "open" : "close")} {what}   B: board up";
        }
        return "";
    }

    void Use()
    {
        var w = _world;
        if (w.Driving != null) { _hud.Say("", w.EnterOrExitVehicle()); return; }
        if (w.PlayerNearDad) { w.InteractDad(); return; }
        if (w.PlayerNearMemere) { _inventory.Show(null); _hud.Say("", "Pick something and press \"Give to memere\"."); return; }
        if (w.PlayerNearGenerator && w.Power.Generator.Present)
        {
            if (!w.Power.Generator.Connected) { w.ToggleGeneratorConnected(); _hud.Say("", "Generator connected to the house."); }
            else if (!w.ToggleGenerator()) _hud.Say("", w.Power.Generator.Fuel <= 0 ? "No gas in it." : "It won't start.");
            return;
        }
        if (NearTv()) { w.ToggleTv(); return; }
        if (NearestContainer() is { } c) { _inventory.Show(w.OpenContainer(c.Id)); return; }
        if (w.VehicleNearPlayer() != null) { _hud.Say("", w.EnterOrExitVehicle()); return; }
        if (FacingEdge() is { } edge) w.ToggleEdge(edge.a, edge.b);
    }
}
