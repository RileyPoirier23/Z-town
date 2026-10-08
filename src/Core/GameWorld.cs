using ZTown.Core.Data;
using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Memere;
using ZTown.Core.Power;
using ZTown.Core.Protection;
using ZTown.Core.Sim;
using ZTown.Core.Tv;
using ZTown.Core.Zombies;

namespace ZTown.Core;

public sealed record Building(string Id, TileRect Footprint, string Roof = "shingle_grey", float Pitch = 0.5f);

public enum MoveMode
{
    Walk,
    Run,
    Sneak,
}

/// <summary>
/// The whole simulated game: map, people, zombies, memere's house. The Godot layer reads from
/// this and calls its actions; it never changes game state directly.
/// </summary>
public sealed partial class GameWorld
{
    public GameData Data { get; }
    public GameClock Clock { get; }
    public Rng Rng { get; }
    public TileMap Map { get; }
    public HarmGate Harm { get; }
    public NoiseBus Noise { get; } = new();
    public ProtectedZones ProtectedZones { get; } = new();
    /// <summary>Building footprints (for roofs and "am I inside").</summary>
    public List<Building> Buildings { get; } = new();
    /// <summary>Memere's house: being inside counts as "home".</summary>
    public List<TileRect> HomeArea { get; } = new();
    public HousePower Power { get; } = new();
    public TvState Tv { get; } = new();
    public Dictionary<string, Container> Containers { get; } = new();
    public List<GameMessage> Messages { get; } = new();

    readonly List<Entity> _entities = new();
    public IReadOnlyList<Entity> Entities => _entities;
    public IEnumerable<Zombie> Zombies => _entities.OfType<Zombie>();

    public Player Player { get; private set; } = null!;
    public MemereEntity Memere { get; private set; } = null!;

    /// <summary>Wear on doors/windows from zombies banging, 0..100. Key: (tile, 'N' or 'W').</summary>
    public Dictionary<(TilePos tile, char side), float> EdgeDamage { get; } = new();

    public double HoursSincePlayerHome { get; set; }
    /// <summary>0..1, decays: did she get food she likes lately.</summary>
    public float FoodLiked { get; set; } = 0.5f;
    public bool PlayerDied { get; private set; }

    int _nextId = 1;
    double _hourAccumulator;

    /// <summary>The seed the world was created from (a save rebuilds the same base world from it).</summary>
    public ulong Seed { get; }

    public GameWorld(GameData data, TileMap map, ulong seed)
    {
        Seed = seed;
        Data = data;
        Map = map;
        Rng = new Rng(seed);
        Clock = new GameClock(data.Sim.StartHour * 3600);
        Harm = new HarmGate(Rng.NextDouble);
        Power.GridShutoffDay = Rng.Range(data.Power.GridShutoffDayMin, data.Power.GridShutoffDayMax + 1);
    }

    // ------------------------------------------------------------------ entities

    public T Add<T>(T e) where T : Entity
    {
        e.Id = _nextId++;
        _entities.Add(e);
        if (e is Player p) Player = p;
        if (e is MemereEntity m) Memere = m;
        return e;
    }

    public void Remove(Entity e) => _entities.Remove(e);

    internal void RestoreNextId(int next) => _nextId = next;
    internal int NextId => _nextId;

    /// <summary>Spawns a zombie unless the tile is blocked or protected. Returns null if refused.</summary>
    public Zombie? TrySpawnZombie(TilePos p)
    {
        if (!Map.InBounds(p) || Map.At(p).Solid || ProtectedZones.Contains(p)) return null;
        var z = new Zombie { Speed = Data.Zombies.ShamblerSpeed, Look = Rng.Range(0, int.MaxValue) };
        z.PlaceAt(p);
        z.Facing = Rng.Range(0f, MathF.Tau);
        z.Health.Restore(Data.Zombies.Health);
        return Add(z);
    }

    public IEnumerable<Entity> EntitiesNear(TilePos at, float radius) =>
        _entities.Where(e => e.Z == at.Z && MathF.Sqrt((e.X - at.X - 0.5f) * (e.X - at.X - 0.5f) + (e.Y - at.Y - 0.5f) * (e.Y - at.Y - 0.5f)) <= radius);

    public bool IsHome(TilePos p) => HomeArea.Any(r => r.Contains(p));

    public bool PlayerIsHome => Player != null && IsHome(Player.Tile);

    public bool HousePowered => Power.IsPowered(Clock.Day);

    public ShowDef? NowOnTv => HousePowered ? TvGuide.WhatsOn(Tv, Data.Tv, Data.Shows, Clock) : null;

    // ------------------------------------------------------------------ tick

    /// <summary>Advances the game by realSeconds of real time (game time runs TimeScale faster).</summary>
    public void Tick(float realSeconds)
    {
        if (realSeconds <= 0) return;
        double gameSeconds = realSeconds * Data.Sim.TimeScale;
        Clock.Advance(gameSeconds);
        double hours = gameSeconds / 3600.0;

        TickZombies(realSeconds);

        _hourAccumulator += hours;
        // slow systems run in ~1 game-minute steps
        while (_hourAccumulator >= 1 / 60.0)
        {
            _hourAccumulator -= 1 / 60.0;
            TickSlow(1 / 60.0);
        }
        Noise.Clear();
    }

    void TickSlow(double hours)
    {
        int day = Clock.Day;

        // power
        if (Power.Tick(hours, day, Data.Power.Generator) && Power.Generator.Present)
            Noise.Emit(GeneratorPos, Data.Power.Generator.NoiseRadius, "generator");

        // food going off
        foreach (var c in Containers.Values)
        {
            bool cold = c.Refrigerated && HousePowered;
            foreach (var s in c.Inventory.Stacks) Age(s, hours, cold);
        }
        if (Player != null) foreach (var s in Player.Inventory.Stacks) Age(s, hours, false);

        TickPlayer(hours);
        TickMemere(hours);
    }

    void Age(ItemStack s, double hours, bool cold)
    {
        var def = Data.Item(s.ItemId);
        if (def == null || def.FreshHours <= 0) return;
        float rate = cold && def.FreshHoursFridge > 0 ? def.FreshHours / def.FreshHoursFridge : 1f;
        s.AgeHours += (float)(hours * rate);
    }

    public bool IsSpoiled(ItemStack s)
    {
        var def = Data.Item(s.ItemId);
        return def != null && def.FreshHours > 0 && s.AgeHours > def.FreshHours;
    }

    public TilePos GeneratorPos { get; set; }

    void TickPlayer(double hours)
    {
        var p = Player;
        if (p == null || p.IsDead) return;
        var n = p.Needs;
        var cfg = Data.Needs;
        float mult = p.Asleep ? cfg.SleepNeedsMultiplier : 1f;
        n.Hunger += (float)(cfg.HungerPerHour * hours * mult);
        n.Thirst += (float)(cfg.ThirstPerHour * hours * mult);
        if (p.Asleep)
        {
            n.Fatigue -= (float)(cfg.FatigueRecoveryPerHourAsleep * hours);
            if (n.Fatigue <= 0) p.Asleep = false;
        }
        else
        {
            n.Fatigue += (float)(cfg.FatiguePerHourAwake * hours);
            n.Boredom += (float)(cfg.BoredomPerHour * hours);
        }

        if (PlayerIsHome)
        {
            HoursSincePlayerHome = 0;
            // memere's comfort is the player's comfort
            float c01 = Memere != null ? Memere.Comfort.Value / 100f : 0;
            float relief = (float)(cfg.MemereMoraleReliefPerHour * hours * c01);
            n.Stress -= relief;
            n.Unhappiness -= relief;
            if (Tv.On && HousePowered) n.Boredom -= (float)(0.05 * hours);
        }
        else
        {
            HoursSincePlayerHome += hours;
            n.Stress += (float)(cfg.AwayFromHomeStressPerHour * hours);
        }
        n.ClampAll();

        if (n.Hunger >= 1) new Starvation { Hours = (float)hours }.ApplyTo(this, p);
        if (n.Thirst >= 1) new Dehydration { Hours = (float)hours }.ApplyTo(this, p);
        if (p.Infection.Infected) new InfectionProgress { Hours = (float)hours }.ApplyTo(this, p);
        if (p.IsDead) PlayerDied = true;
    }

    void TickMemere(double hours)
    {
        var m = Memere;
        if (m == null) return;
        Comfort.Consume(m.Supplies, Data.Supplies.Values, hours);
        FoodLiked = MathF.Max(0, FoodLiked - (float)(hours / 48.0));

        // she puts her show on when it's on, if there's power
        if (HousePowered && m.Activity != MemereActivity.Napping)
        {
            var ch = TvGuide.ChannelWithHerShow(Data.Tv, Data.Shows, Clock);
            if (ch != null)
            {
                Tv.On = true;
                Tv.Channel = ch;
            }
            else if (Clock.Day >= Data.Tv.BroadcastEndsDay && Tv.HasRerunsBox) Tv.On = true;
        }

        var show = NowOnTv;
        var input = new ComfortInputs
        {
            Powered = HousePowered,
            Warmth01 = HousePowered ? 1f : WarmthWithoutPower(),
            FoodLiked01 = FoodLiked,
            TvOn = show != null && show.Id != Data.Tv.StaticShow,
            HerShowOn = show?.HerShow == true,
            HouseTidy01 = 1f,
            HoursSincePlayerHome = HoursSincePlayerHome,
        };
        Comfort.Update(m.Comfort, input, m.Supplies, Data.Supplies, Data.Comfort, hours);
        m.Activity = Comfort.ChooseActivity(Clock.HourOfDay, input, m.Supplies, Data.Supplies, Data.Comfort, Rng.NextDouble());
    }

    /// <summary>Placeholder until seasons/temperature: colder nights without power.</summary>
    float WarmthWithoutPower() => Clock.IsNight ? 0.3f : 0.6f;

    // ------------------------------------------------------------------ player movement

    /// <summary>Moves the player by a direction (tiles, roughly unit length) for realSeconds. Slides along walls.</summary>
    public void MovePlayer(float dx, float dy, MoveMode mode, float realSeconds)
    {
        var p = Player;
        if (p == null || p.IsDead) return;
        p.Asleep = false;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.0001f) return;
        dx /= len;
        dy /= len;
        float speed = mode switch
        {
            MoveMode.Run => Data.Sim.PlayerRunSpeed,
            MoveMode.Sneak => Data.Sim.PlayerSneakSpeed,
            _ => Data.Sim.PlayerWalkSpeed,
        };
        float step = speed * realSeconds;
        p.Facing = MathF.Atan2(dy, dx);
        TryMove(p, dx * step, dy * step, PlayerMayEnter);
        if (mode == MoveMode.Run) Noise.Emit(p.Tile, Data.Sim.RunNoiseRadius, "footsteps");
        else if (mode == MoveMode.Walk) Noise.Emit(p.Tile, Data.Sim.WalkNoiseRadius, "footsteps");
    }

    bool PlayerMayEnter(TilePos t) => true;

    bool ZombieMayEnter(TilePos t) => Targeting.HostileMayEnter(ProtectedZones, t);

    /// <summary>Moves an entity, blocked by solid tiles and closed edges; slides on each axis.</summary>
    internal bool TryMove(Entity e, float mx, float my, Func<TilePos, bool> mayEnter)
    {
        bool moved = false;
        if (CanMoveTo(e, e.X + mx, e.Y, mayEnter)) { e.X += mx; moved = true; }
        if (CanMoveTo(e, e.X, e.Y + my, mayEnter)) { e.Y += my; moved = true; }
        return moved;
    }

    bool CanMoveTo(Entity e, float nx, float ny, Func<TilePos, bool> mayEnter)
    {
        var from = e.Tile;
        var to = new TilePos((int)MathF.Floor(nx), (int)MathF.Floor(ny), e.Z);
        if (to == from) return true;
        return Pathfinder.CanStep(Map, from, to, mayEnter);
    }

    // ------------------------------------------------------------------ zombies

    void TickZombies(float dt)
    {
        var cfg = Data.Zombies;
        foreach (var z in Zombies.ToList())
        {
            if (z.IsDead) continue;
            var b = z.Brain;
            b.ThinkTimer -= dt;
            b.AttackTimer -= dt;
            if (b.ThinkTimer <= 0)
            {
                b.ThinkTimer = 0.5f + Rng.NextFloat() * 0.3f;
                Think(z);
            }

            if (b.Target is { } t && Targeting.CanTarget(z, t) && z.DistanceTo(t) <= cfg.AttackRange)
            {
                if (b.AttackTimer <= 0)
                {
                    b.AttackTimer = cfg.AttackSeconds;
                    new ZombieAttack { Attacker = z }.ApplyTo(this, t);
                    if (t is Player pl && pl.IsDead) PlayerDied = true;
                }
                continue;
            }

            if (b.Mode == ZombieMode.Thump && b.ThumpEdge is { } edge)
            {
                Thump(z, edge.from, edge.to, dt);
                continue;
            }
            FollowPath(z, dt);
        }
        if (RemoveDeadZombies) _entities.RemoveAll(e => e is Zombie zz && zz.IsDead);
    }

    /// <summary>Dead zombies stay as corpses unless this is set (tests turn it on to keep lists short).</summary>
    public bool RemoveDeadZombies { get; set; }

    void Think(Zombie z)
    {
        var cfg = Data.Zombies;
        var b = z.Brain;
        float sight = Clock.IsNight ? cfg.NightSightRange : cfg.SightRange;

        // 1. see someone?
        Entity? seen = null;
        float best = float.MaxValue;
        foreach (var e in _entities)
        {
            if (!Targeting.CanTarget(z, e)) continue;
            float d = z.DistanceTo(e);
            if (d > sight || d >= best) continue;
            if (!LineOfSight(z.Tile, e.Tile)) continue;
            seen = e;
            best = d;
        }
        if (seen != null)
        {
            Targeting.TrySetTarget(z, seen);
            b.Mode = ZombieMode.Chase;
            SetGoal(z, seen.Tile);
            return;
        }
        if (b.Target != null && !Targeting.CanTarget(z, b.Target)) Targeting.TrySetTarget(z, null);
        if (b.Target != null)
        {
            // lost sight: go to where they were last
            b.Mode = ZombieMode.Investigate;
            Targeting.TrySetTarget(z, null);
            return;
        }

        // 2. hear something?
        foreach (var n in Noise.Current)
        {
            if (n.At.Z != z.Z) continue;
            if (z.Tile.DistanceTo(n.At) > n.Radius * cfg.HearingMultiplier) continue;
            b.Mode = ZombieMode.Investigate;
            SetGoal(z, n.At);
            return;
        }

        // 3. wander
        if (b.Path == null || b.PathIndex >= b.Path.Count)
        {
            if (Rng.Chance(0.15))
            {
                b.Mode = ZombieMode.Wander;
                SetGoal(z, new TilePos(z.Tile.X + Rng.Range(-6, 7), z.Tile.Y + Rng.Range(-6, 7), z.Z));
            }
            else b.Mode = ZombieMode.Idle;
        }
    }

    void SetGoal(Zombie z, TilePos goal)
    {
        var b = z.Brain;
        if (b.Goal == goal && b.Path != null && b.PathIndex < b.Path.Count) return;
        b.Goal = goal;
        b.Path = Pathfinder.Find(Map, z.Tile, goal, ZombieMayEnter, 1500);
        b.PathIndex = 0;
        if (b.Path == null && b.Mode is ZombieMode.Chase or ZombieMode.Investigate)
        {
            // blocked: path as if doors and windows were open, and bang on the first one in the way
            var through = FindPathThroughBreakables(z.Tile, goal);
            if (through != null)
            {
                b.Path = through;
                b.PathIndex = 0;
            }
        }
    }

    List<TilePos>? FindPathThroughBreakables(TilePos from, TilePos to)
    {
        // temporarily treat closed doors/windows as open by pathing on a predicate copy
        var map = Map;
        var opened = new List<(TilePos a, TilePos b, Edge e)>();
        // cheap approach: search a box around the route for breakable edges and open them for the search
        int minX = Math.Max(0, Math.Min(from.X, to.X) - 10), maxX = Math.Min(map.Width - 1, Math.Max(from.X, to.X) + 10);
        int minY = Math.Max(0, Math.Min(from.Y, to.Y) - 10), maxY = Math.Min(map.Height - 1, Math.Max(from.Y, to.Y) + 10);
        for (int y = minY; y <= maxY; y++)
            for (int x = minX; x <= maxX; x++)
            {
                ref var t = ref map.At(x, y, from.Z);
                if (t.North.IsBreakable() && y > 0) { opened.Add((new(x, y, from.Z), new(x, y - 1, from.Z), t.North)); t.North = Edge.DoorOpen; }
                if (t.West.IsBreakable() && x > 0) { opened.Add((new(x, y, from.Z), new(x - 1, y, from.Z), t.West)); t.West = Edge.DoorOpen; }
            }
        var path = Pathfinder.Find(map, from, to, ZombieMayEnter, 2500);
        foreach (var (a, b, e) in opened) map.SetEdgeBetween(a, b, e);
        return path;
    }

    void FollowPath(Zombie z, float dt)
    {
        var b = z.Brain;
        if (b.Path == null || b.PathIndex >= b.Path.Count) return;
        var next = b.Path[b.PathIndex];
        var cur = z.Tile;
        if (next == cur)
        {
            b.PathIndex++;
            return;
        }

        // next step through a closed door/window? bang on it
        if (Math.Abs(next.X - cur.X) + Math.Abs(next.Y - cur.Y) == 1 && Map.EdgeBetween(cur, next).IsBreakable())
        {
            b.Mode = ZombieMode.Thump;
            b.ThumpEdge = (cur, next);
            return;
        }

        float tx = next.X + 0.5f - z.X, ty = next.Y + 0.5f - z.Y;
        float dist = MathF.Sqrt(tx * tx + ty * ty);
        float speed = z.Speed * (b.Mode == ZombieMode.Chase ? Data.Zombies.ChaseSpeedMultiplier : 1f);
        float step = MathF.Min(dist, speed * dt);
        if (dist > 0.0001f)
        {
            z.Facing = MathF.Atan2(ty, tx);
            if (!TryMove(z, tx / dist * step, ty / dist * step, ZombieMayEnter))
            {
                b.Path = null; // stuck: re-think
                return;
            }
        }
        if (dist <= step + 0.05f) b.PathIndex++;
    }

    void Thump(Zombie z, TilePos from, TilePos to, float dt)
    {
        var b = z.Brain;
        var edge = Map.EdgeBetween(from, to);
        if (!edge.IsBreakable())
        {
            b.Mode = ZombieMode.Chase;
            b.ThumpEdge = null;
            return;
        }
        var key = EdgeKey(from, to);
        float dmg = EdgeDamage.GetValueOrDefault(key) + Data.Zombies.ThumpPerSecond * dt;
        EdgeDamage[key] = dmg;
        Noise.Emit(from, Data.Zombies.ThumpNoiseRadius, "thump");
        if (dmg >= 100)
        {
            Map.SetEdgeBetween(from, to, edge == Edge.DoorClosed ? Edge.DoorBroken : Edge.WindowBroken);
            EdgeDamage.Remove(key);
            b.Mode = ZombieMode.Chase;
            b.ThumpEdge = null;
        }
    }

    public static (TilePos, char) EdgeKey(TilePos a, TilePos b)
    {
        int dx = b.X - a.X, dy = b.Y - a.Y;
        if (dy == -1) return (a, 'N');
        if (dy == 1) return (b, 'N');
        if (dx == -1) return (a, 'W');
        return (b, 'W');
    }

    /// <summary>Bresenham line; walls and closed doors block sight.</summary>
    public bool LineOfSight(TilePos a, TilePos b)
    {
        if (a.Z != b.Z) return false;
        int x = a.X, y = a.Y, dx = Math.Abs(b.X - a.X), dy = -Math.Abs(b.Y - a.Y);
        int sx = a.X < b.X ? 1 : -1, sy = a.Y < b.Y ? 1 : -1, err = dx + dy;
        while (x != b.X || y != b.Y)
        {
            int e2 = 2 * err;
            int nx = x, ny = y;
            if (e2 >= dy) { err += dy; nx += sx; }
            if (e2 <= dx) { err += dx; ny += sy; }
            var cur = new TilePos(x, y, a.Z);
            if (nx != x && Map.EdgeBetween(cur, new TilePos(nx, y, a.Z)).BlocksSight()) return false;
            if (ny != y && Map.EdgeBetween(new TilePos(nx, y, a.Z), new TilePos(nx, ny, a.Z)).BlocksSight()) return false;
            x = nx;
            y = ny;
        }
        return true;
    }
}
