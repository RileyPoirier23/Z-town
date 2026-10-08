using ZTown.Core.Entities;
using ZTown.Core.Items;
using ZTown.Core.Map;
using ZTown.Core.Protection;
using ZTown.Core.Sim;
using ZTown.Core.Vehicles;

namespace ZTown.Core;

/// <summary>Cars: getting in, driving, fuel, hitting things, the trunk.</summary>
public sealed partial class GameWorld
{
    public List<Vehicle> Vehicles { get; } = new();
    /// <summary>The car the player is driving, if any.</summary>
    public Vehicle? Driving { get; private set; }

    public Vehicle? VehicleNearPlayer(float range = 2.4f) =>
        Vehicles.Where(v => Dist(v.X, v.Y, Player.X, Player.Y) < v.Length / 2 + range).OrderBy(v => Dist(v.X, v.Y, Player.X, Player.Y)).FirstOrDefault();

    static float Dist(float ax, float ay, float bx, float by) => MathF.Sqrt((ax - bx) * (ax - bx) + (ay - by) * (ay - by));

    /// <summary>Get in (start it if it has keys and fuel) or get out.</summary>
    public string EnterOrExitVehicle()
    {
        if (Driving is { } d)
        {
            if (MathF.Abs(d.Speed) > 1.5f) return "Stop first.";
            d.EngineOn = false;
            d.Speed = 0;
            Driving = null;
            // step out on the driver's side (or anywhere free nearby)
            foreach (var (x, y) in new[] { d.DriverSide, (d.X + d.Forward.y * 2, d.Y - d.Forward.x * 2), d.Rear })
            {
                var t = new TilePos((int)MathF.Floor(x), (int)MathF.Floor(y));
                if (Map.InBounds(t) && !Map.At(t).Solid) { Player.X = x; Player.Y = y; return "Out of the car."; }
            }
            Player.X = d.X; Player.Y = d.Y;
            return "Out of the car.";
        }
        var v = VehicleNearPlayer();
        if (v == null) return "";
        Driving = v;
        if (!v.HasKeys)
        {
            // hotwiring: Mechanic job/trait or mechanics + electrical skill
            bool canHotwire = Profile.Has("canHotwire", Data.Character) || Skill("mechanics") >= 2 && Skill("electrical") >= 1;
            if (!canHotwire) return "No keys. It won't start. (A mechanic could hotwire it.)";
            PassTime(5);
            v.HasKeys = true;
            Practice("electrical", 5);
            if (Driving == null) return "";
        }
        if (v.Fuel <= 0) return "Out of gas.";
        if (v.Condition <= 0) return "It won't turn over.";
        v.EngineOn = true;
        Noise.Emit(v.Tile, Data.Vehicles.EngineNoise, "engine_start");
        return "It started.";
    }

    /// <summary>throttle -1..1 (reverse..forward), steer -1..1.</summary>
    public void DriveInput(float throttle, float steer, bool brake, float dt)
    {
        var v = Driving;
        if (v == null) return;
        var cfg = Data.Vehicles;
        if (v.EngineOn && v.Fuel > 0 && v.Condition > 0)
        {
            if (brake) v.Speed = MoveTowards(v.Speed, 0, cfg.Brake * dt);
            else if (throttle > 0) v.Speed = MathF.Min(cfg.MaxSpeed * (0.5f + 0.5f * v.Condition), v.Speed + (v.Speed < 0 ? cfg.Brake : cfg.Accel) * throttle * dt);
            else if (throttle < 0) v.Speed = MathF.Max(-cfg.MaxReverse, v.Speed + (v.Speed > 0 ? cfg.Brake : cfg.Accel) * throttle * dt);
        }
        v.Speed = MoveTowards(v.Speed, 0, cfg.Drag * dt * (v.EngineOn ? 1 : 3));
        if (MathF.Abs(v.Speed) > 0.2f) v.Angle += steer * cfg.Turn * dt * Math.Clamp(v.Speed / 6f, -1, 1);
    }

    static float MoveTowards(float a, float b, float step) => a < b ? MathF.Min(b, a + step) : MathF.Max(b, a - step);

    void TickVehicles(float dt)
    {
        foreach (var v in Vehicles)
        {
            if (MathF.Abs(v.Speed) < 0.01f) { v.Speed = 0; continue; }
            var (fx, fy) = v.Forward;
            float nx = v.X + fx * v.Speed * dt, ny = v.Y + fy * v.Speed * dt;
            if (Blocked(v, nx, ny, v.Angle))
            {
                // crash: stop, damage scales with speed
                float hit = MathF.Abs(v.Speed);
                if (hit > 4) { v.Condition = MathF.Max(0, v.Condition - hit * 0.008f); Noise.Emit(v.Tile, 20, "crash"); }
                v.Speed = 0;
                continue;
            }
            v.X = nx;
            v.Y = ny;
            if (v.EngineOn)
            {
                v.Fuel = MathF.Max(0, v.Fuel - MathF.Abs(v.Speed) * dt / 1000f * Data.Vehicles.LitresPerKm); // tiles are metres
                if (Rng.Chance(0.002)) Practice("mechanics", 1);
                if (v.Fuel <= 0) v.EngineOn = false;
            }
            // people and zombies in the way get hit (through the harm gate, like everything else)
            if (MathF.Abs(v.Speed) > 3)
                foreach (var e in _entities.ToList())
                {
                    if (e == Player || e.Z != 0) continue;
                    if (!v.Footprint(v.X, v.Y, v.Angle).Any(p => Dist(p.x, p.y, e.X, e.Y) < 0.8f)) continue;
                    new VehicleImpact { Speed = MathF.Abs(v.Speed) * 4 }.ApplyTo(this, e);
                    if (!e.IsProtected) { e.X += fx * 1.2f; e.Y += fy * 1.2f; }
                    v.Speed *= 0.8f;
                    v.Condition = MathF.Max(0, v.Condition - 0.004f);
                    Noise.Emit(e.Tile, 12, "car_hit");
                }
        }
        if (Driving is { } d)
        {
            Player.X = d.X;
            Player.Y = d.Y;
            Player.Facing = d.Angle;
            if (d.EngineOn && Rng.Chance(0.05)) Noise.Emit(d.Tile, Data.Vehicles.EngineNoise, "engine");
        }
        foreach (var v in Vehicles)
        {
            var (rx, ry) = v.Rear;
            v.Trunk.Pos = new TilePos((int)MathF.Floor(rx), (int)MathF.Floor(ry));
        }
    }

    bool Blocked(Vehicle v, float x, float y, float angle)
    {
        foreach (var (px, py) in v.Footprint(x, y, angle))
        {
            var t = new TilePos((int)MathF.Floor(px), (int)MathF.Floor(py));
            if (!Map.InBounds(t)) return true;
            ref var tile = ref Map.At(t);
            if (tile.Solid || tile.Building != 0 || ProtectedZones.Contains(t)) return true;
        }
        foreach (var o in Vehicles)
            if (o != v && Dist(o.X, o.Y, x, y) < (o.Length + v.Length) / 2 * 0.8f && Dist(o.X, o.Y, x, y) < Dist(o.X, o.Y, v.X, v.Y)) return true;
        return false;
    }

    /// <summary>Fill the tank from a gas can.</summary>
    public bool RefuelVehicle(Vehicle v, ItemStack can)
    {
        var def = Data.Item(can.ItemId);
        if (def == null || def.Fuel <= 0 || !Player.Inventory.Stacks.Contains(can)) return false;
        float room = v.FuelMax - v.Fuel;
        float have = def.Fuel * can.Condition;
        float pour = MathF.Min(room, have);
        if (pour <= 0.01f) return false;
        v.Fuel += pour;
        can.Condition = (have - pour) / def.Fuel;
        if (can.Condition <= 0.001f) Player.Inventory.RemoveStack(can);
        return true;
    }

    /// <summary>Parks cars along streets, in driveways and lots.</summary>
    public void SpawnParkedCars(Rng rng)
    {
        var cfg = Data.Vehicles;
        int road = 0;
        for (int y = 0; y < Map.Height; y++)
            for (int x = 0; x < Map.Width; x++)
                if (Map.At(x, y).Floor is "asphalt" or "parking") road++;
        int target = (int)(road / 1000f * cfg.CarsPer1000RoadTiles);
        string[] models = { "sedan", "sedan", "pickup", "van" };
        for (int tries = 0, placed = 0; placed < target && tries < target * 40; tries++)
        {
            int x = rng.Range(2, Map.Width - 2), y = rng.Range(2, Map.Height - 2);
            var f = Map.At(x, y).Floor;
            if (f is not ("asphalt" or "parking" or "driveway")) continue;
            // park along the street: next to a sidewalk/grass edge, lined up with the curb
            bool alongX = Map.At(x, y - 1).Floor is "sidewalk" or "grass" || Map.At(x, y + 1).Floor is "sidewalk" or "grass";
            bool alongY = Map.At(x - 1, y).Floor is "sidewalk" or "grass" || Map.At(x + 1, y).Floor is "sidewalk" or "grass";
            if (f == "asphalt" && !(alongX ^ alongY)) continue;
            float angle = alongX ? (rng.Chance(0.5) ? 0 : MathF.PI) : (rng.Chance(0.5) ? MathF.PI / 2 : -MathF.PI / 2);
            var v = new Vehicle
            {
                Id = $"car{Vehicles.Count}",
                Model = models[rng.Range(0, models.Length)],
                Color = cfg.Colors[rng.Range(0, cfg.Colors.Count)],
                X = x + 0.5f, Y = y + 0.5f, Angle = angle,
                Fuel = rng.Range(0f, 30f),
                Condition = rng.Range(0.3f, 1f),
                HasKeys = rng.Chance(cfg.KeysInsideChance),
            };
            if (v.Model == "van") { v.Length = 5f; v.Width = 2.1f; }
            if (Blocked(v, v.X, v.Y, v.Angle)) continue;
            if (Vehicles.Any(o => Dist(o.X, o.Y, v.X, v.Y) < (o.Length + v.Length) / 2 + 0.6f)) continue; // room to park
            v.Trunk = new Container { Id = v.Id + "_trunk", Kind = "trunk", LootTable = "car_trunk", Inventory = new Inventory(40f) };
            Containers[v.Trunk.Id] = v.Trunk;
            Vehicles.Add(v);
            placed++;
        }
    }

    internal void RestoreDriving(Vehicle? v) => Driving = v;
}
