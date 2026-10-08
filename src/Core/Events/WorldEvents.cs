using ZTown.Core.Map;
using ZTown.Core.Protection;

namespace ZTown.Core.Events;

/// <summary>
/// A world event (helicopter, alarm, gunshots, fire, radio chatter). Like harm sources, every
/// implementation is found by reflection and fired at memere's position in the protection
/// tests, so new events need a public parameterless constructor.
/// </summary>
public interface IWorldEvent
{
    string Id { get; }

    void Fire(GameWorld world, TilePos at);
}

public sealed class HelicopterPass : IWorldEvent
{
    public string Id => "helicopter";

    public void Fire(GameWorld world, TilePos at) => world.Noise.Emit(at, 60, Id);
}

public sealed class HouseAlarm : IWorldEvent
{
    public string Id => "house_alarm";

    public void Fire(GameWorld world, TilePos at) => world.Noise.Emit(at, 35, Id);
}

public sealed class Gunshots : IWorldEvent
{
    public string Id => "gunshots";

    public void Fire(GameWorld world, TilePos at)
    {
        world.Noise.Emit(at, 45, Id);
        // stray shots: anyone living within 3 tiles may get hit (through the gate)
        foreach (var e in world.EntitiesNear(at, 3f).ToList())
            if (world.Rng.Chance(0.3)) new Gunshot { Damage = 25 }.ApplyTo(world, e);
    }
}

public sealed class FireOutbreak : IWorldEvent
{
    public string Id => "fire";

    public void Fire(GameWorld world, TilePos at)
    {
        world.Noise.Emit(at, 15, Id);
        foreach (var e in world.EntitiesNear(at, 2.5f).ToList())
            new FireBurn { Damage = 20 }.ApplyTo(world, e);
    }
}

public sealed class CarCrash : IWorldEvent
{
    public string Id => "car_crash";

    public void Fire(GameWorld world, TilePos at)
    {
        world.Noise.Emit(at, 30, Id);
        foreach (var e in world.EntitiesNear(at, 1.5f).ToList())
            new VehicleImpact { Speed = 50 }.ApplyTo(world, e);
    }
}

public sealed class RadioChatter : IWorldEvent
{
    public string Id => "radio_chatter";

    public void Fire(GameWorld world, TilePos at) => world.Messages.Add(new GameMessage("radio", "radio.chatter.placeholder"));
}

/// <summary>A wall of zombies shows up: spawns a horde near the point (never in protected zones).</summary>
public sealed class HordeArrives : IWorldEvent
{
    public string Id => "horde";
    public int Count { get; init; } = 12;

    public void Fire(GameWorld world, TilePos at)
    {
        for (int i = 0; i < Count; i++)
        {
            var p = new TilePos(at.X + world.Rng.Range(-6, 7), at.Y + world.Rng.Range(-6, 7), at.Z);
            world.TrySpawnZombie(p);
        }
        world.Noise.Emit(at, 20, Id);
    }
}

/// <summary>Zombies break into the house: every closed door and window within range breaks.</summary>
public sealed class Breach : IWorldEvent
{
    public string Id => "breach";

    public void Fire(GameWorld world, TilePos at)
    {
        var map = world.Map;
        for (int y = Math.Max(0, at.Y - 8); y < Math.Min(map.Height, at.Y + 8); y++)
            for (int x = Math.Max(0, at.X - 8); x < Math.Min(map.Width, at.X + 8); x++)
            {
                ref var t = ref map.At(x, y, at.Z);
                t.North = Broken(t.North);
                t.West = Broken(t.West);
            }
        for (int i = 0; i < 6; i++)
            world.TrySpawnZombie(new TilePos(at.X + world.Rng.Range(-5, 6), at.Y + world.Rng.Range(-5, 6), at.Z));
    }

    static Edge Broken(Edge e) => e switch
    {
        Edge.DoorClosed or Edge.DoorOpen => Edge.DoorBroken,
        Edge.WindowClosed or Edge.WindowOpen => Edge.WindowBroken,
        _ => e,
    };
}

public readonly record struct GameMessage(string Channel, string DialogueId);
