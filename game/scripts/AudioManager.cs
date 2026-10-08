using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Godot;
using ZTown.Core;
using ZTown.Core.Entities;
using ZTown.Core.Map;
using ZTown.Core.Zombies;

namespace ZTown.Game;

/// <summary>
/// Sound: effects for what happens in the world (positional), loops for the generator and TV,
/// ambience by time and place, and music that changes between home, out, and danger.
/// Files and names come from assets/audio/manifest.json (tools/audio).
/// </summary>
public partial class AudioManager : Node2D
{
    public GameWorld? World { get; set; }
    public float SfxVolume { get; set; } = 1f;
    public float MusicVolume { get; set; } = 0.6f;

    readonly Dictionary<string, AudioStream[]> _sfx = new();
    readonly Dictionary<string, AudioStream> _loops = new();
    readonly Dictionary<string, AudioStream> _music = new();
    readonly List<AudioStreamPlayer2D> _pool = new();
    AudioStreamPlayer2D _gen = null!, _tv = null!, _engine = null!;
    AudioStreamPlayer _amb = null!, _ambIndoor = null!, _musA = null!, _musB = null!, _rain = null!;
    string _musicNow = "", _ambNow = "";
    float _stepTimer, _groanTimer;
    Vector2 _lastPlayer;
    readonly RandomNumberGenerator _rng = new();

    public override void _Ready()
    {
        using var f = FileAccess.Open("res://assets/audio/manifest.json", FileAccess.ModeFlags.Read);
        if (f == null) return;
        using var doc = JsonDocument.Parse(f.GetAsText());
        var root = doc.RootElement;
        foreach (var s in root.GetProperty("sfx").EnumerateObject())
            _sfx[s.Name] = s.Value.EnumerateArray().Select(v => GD.Load<AudioStream>(v.GetString()!)).ToArray();
        foreach (var l in root.GetProperty("loops").EnumerateObject()) _loops[l.Name] = Loop(l.Value.GetString()!);
        foreach (var m in root.GetProperty("music").EnumerateObject()) _music[m.Name] = Loop(m.Value.GetString()!);
        for (int i = 0; i < 12; i++)
        {
            var p = new AudioStreamPlayer2D { MaxDistance = 2600, Attenuation = 1.4f };
            AddChild(p);
            _pool.Add(p);
        }
        _gen = new AudioStreamPlayer2D { Stream = _loops.GetValueOrDefault("generator"), MaxDistance = 2600, Attenuation = 1.2f };
        _tv = new AudioStreamPlayer2D { Stream = _loops.GetValueOrDefault("tv"), MaxDistance = 1300, Attenuation = 1.6f };
        _amb = new AudioStreamPlayer();
        _ambIndoor = new AudioStreamPlayer { Stream = _loops.GetValueOrDefault("amb_indoor"), VolumeDb = -80 };
        _rain = new AudioStreamPlayer { Stream = _loops.GetValueOrDefault("rain"), VolumeDb = -80 };
        AddChild(_rain);
        _rain.Play();
        _musA = new AudioStreamPlayer { VolumeDb = -80 };
        _musB = new AudioStreamPlayer { VolumeDb = -80 };
        _engine = new AudioStreamPlayer2D { Stream = _loops.GetValueOrDefault("generator"), MaxDistance = 3000, Attenuation = 1.1f };
        foreach (var n in new Node[] { _gen, _tv, _engine, _amb, _ambIndoor, _musA, _musB }) AddChild(n);
        _ambIndoor.Play();
    }

    static AudioStream Loop(string path)
    {
        var s = GD.Load<AudioStream>(path);
        if (s is AudioStreamWav wav)
        {
            wav.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
            wav.LoopBegin = 0;
            wav.LoopEnd = (int)(wav.GetLength() * wav.MixRate);
        }
        return s;
    }

    public void Play(string kind, Vector2 screenPos, float volDb = 0)
    {
        if (!_sfx.TryGetValue(kind, out var vars) || vars.Length == 0) return;
        var p = _pool.FirstOrDefault(x => !x.Playing) ?? _pool[0];
        p.Stream = vars[_rng.RandiRange(0, vars.Length - 1)];
        p.Position = screenPos;
        p.VolumeDb = volDb + Mathf.LinearToDb(SfxVolume);
        p.PitchScale = _rng.RandfRange(0.92f, 1.08f);
        p.Play();
    }

    void PlayAt(string kind, TilePos t, float volDb = 0) => Play(kind, Iso.ToScreen(t.X + 0.5f, t.Y + 0.5f), volDb);

    public override void _Process(double delta)
    {
        var w = World;
        if (w == null || _pool.Count == 0) return;
        float dt = (float)delta;
        var p = w.Player;

        // world sounds
        foreach (var n in w.Noise.Log)
        {
            switch (n.Cause)
            {
                case "thump": if (_rng.Randf() < 0.08f) PlayAt("thump", n.At, -4); break;
                case "glass": PlayAt("glass", n.At); break;
                case "door_break": PlayAt("thump", n.At, 3); break;
                case "door_open": PlayAt("door_open", n.At, -6); break;
                case "door_close": PlayAt("door_close", n.At, -6); break;
                case "melee": PlayAt("swing", n.At, -6); break;
                case "hit": PlayAt("swing", n.At, -8); PlayAt("hit", n.At); break;
                case "zombie_attack": PlayAt("hit", n.At, -2); PlayAt("groan", n.At, 2); break;
                case "hammering": PlayAt("hammer", n.At); break;
                case "crash": PlayAt("thump", n.At, 4); PlayAt("glass", n.At, -6); break;
                case "car_hit": PlayAt("hit", n.At, 4); break;
            }
        }
        w.Noise.Log.Clear();

        // footsteps
        var pos = new Vector2(p.X, p.Y);
        float speed = (pos - _lastPlayer).Length() / Mathf.Max(dt, 0.0001f);
        _lastPlayer = pos;
        if (speed > 0.5f && !p.Asleep)
        {
            _stepTimer -= dt;
            if (_stepTimer <= 0)
            {
                _stepTimer = speed > 3 ? 0.28f : speed < 1.5f ? 0.6f : 0.42f;
                var floor = w.Map.InBounds(p.Tile) ? w.Map.At(p.Tile).Floor : "grass";
                PlayAt(floor is "grass" or "field" or "dirt" or "carpet" ? "step_grass" : "step_hard", p.Tile, speed < 1.5f ? -16 : -9);
            }
        }

        // groans from nearby zombies you can see or hear
        _groanTimer -= dt;
        if (_groanTimer <= 0)
        {
            _groanTimer = _rng.RandfRange(0.8f, 2.4f);
            var near = w.Zombies.Where(z => !z.IsDead && z.DistanceTo(p) < 22).ToList();
            if (near.Count > 0) PlayAt("groan", near[_rng.RandiRange(0, near.Count - 1)].Tile, -6);
        }

        // generator and TV loops
        bool genOn = w.Power.Generator.Running;
        _gen.Position = Iso.ToScreen(w.GeneratorPos.X + 0.5f, w.GeneratorPos.Y + 0.5f);
        if (genOn && !_gen.Playing) _gen.Play();
        if (!genOn && _gen.Playing) _gen.Stop();
        var tvShow = w.NowOnTv;
        bool tvOn = tvShow != null && tvShow.Kind != "static";
        if (tvOn && TvTile(w) is { } tvt) _tv.Position = Iso.ToScreen(tvt.X + 0.5f, tvt.Y + 0.5f);
        if (tvOn && !_tv.Playing) _tv.Play();
        if (!tvOn && _tv.Playing) _tv.Stop();

        // engine: the generator hum, pitched up with speed
        var car = w.Driving;
        if (car != null && car.EngineOn)
        {
            _engine.Position = Iso.ToScreen(car.X, car.Y);
            _engine.PitchScale = 1.4f + Mathf.Abs(car.Speed) * 0.08f;
            if (!_engine.Playing) _engine.Play();
        }
        else if (_engine.Playing) _engine.Stop();
        foreach (var n in w.Noise.Log) { }

        // ambience
        bool night = w.Clock.IsNight;
        string amb = night ? "amb_night" : "amb_day";
        if (amb != _ambNow && _loops.TryGetValue(amb, out var ambStream))
        {
            _ambNow = amb;
            _amb.Stream = ambStream;
            _amb.Play();
        }
        bool inside = w.Map.InBounds(p.Tile) && w.Map.At(p.Tile).Building != 0;
        _amb.VolumeDb = Mathf.Lerp(_amb.VolumeDb, inside ? -24 : -10, dt * 2);
        float rainDb = !w.Weather.Raining ? -80 : (inside ? -22 : -8) + (w.Weather.Sky == ZTown.Core.Weather.Sky.HeavyRain ? 4 : 0);
        _rain.VolumeDb = Mathf.Lerp(_rain.VolumeDb, rainDb, dt * 1.5f);
        bool hum = inside && (w.IsHome(p.Tile) ? w.HousePowered : w.Power.GridOn(w.Clock.Day));
        _ambIndoor.VolumeDb = Mathf.Lerp(_ambIndoor.VolumeDb, hum ? -18 : -60, dt * 2);

        // music: danger if something's after you, home at memere's, otherwise out
        bool danger = w.Zombies.Any(z => !z.IsDead && z.Brain.Mode == ZombieMode.Chase && z.Brain.Target == p && z.DistanceTo(p) < 14);
        string cue = danger ? "danger" : w.PlayerIsHome ? "home" : "out";
        if (cue != _musicNow) SwitchMusic(cue);
        float target = Mathf.LinearToDb(MusicVolume) - 6;
        _musA.VolumeDb = Mathf.Lerp(_musA.VolumeDb, _musA.Playing ? target : -80, dt * 0.8f);
        _musB.VolumeDb = Mathf.Lerp(_musB.VolumeDb, -80, dt * 1.2f);
        if (_musB.Playing && _musB.VolumeDb < -60) _musB.Stop();
    }

    void SwitchMusic(string cue)
    {
        if (!_music.TryGetValue(cue, out var stream)) return;
        _musicNow = cue;
        // the old cue fades out on B while the new one fades in on A
        (_musA, _musB) = (_musB, _musA);
        _musA.Stream = stream;
        _musA.VolumeDb = -40;
        _musA.Play();
    }

    TilePos? _tvTile;

    TilePos? TvTile(GameWorld w)
    {
        if (_tvTile != null) return _tvTile;
        var home = w.HomeBuilding >= 0 ? w.Buildings[w.HomeBuilding].Tiles : w.HomeArea.SelectMany(r =>
            Enumerable.Range(r.MinY, r.Height).SelectMany(y => Enumerable.Range(r.MinX, r.Width).Select(x => new TilePos(x, y))));
        foreach (var t in home) if (w.Map.At(t).Object == "tv") return _tvTile = t;
        return null;
    }

    public void ResetWorld() => _tvTile = null;

    public void PhoneBuzz() => Play("phone", GetViewport().GetCamera2D()?.GetScreenCenterPosition() ?? Vector2.Zero, -4);
}
