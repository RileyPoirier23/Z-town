using System.Reflection;
using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Protection;

namespace ZTown.Core.Tests.Protection;

/// <summary>
/// Memere protection, part 2: aim every harm source, every harm kind and every world event
/// straight at her. Nothing may change.
/// </summary>
public class GateTests
{
    static readonly Assembly Core = typeof(GameWorld).Assembly;

    public static IEnumerable<object[]> HarmSources() =>
        Core.GetTypes().Where(t => typeof(IHarmSource).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => new object[] { t.Name });

    public static IEnumerable<object[]> WorldEvents() =>
        Core.GetTypes().Where(t => typeof(IWorldEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).Select(t => new object[] { t.Name });

    public static IEnumerable<object[]> HarmKinds() => Enum.GetValues<HarmKind>().Select(k => new object[] { k });

    [Theory]
    [MemberData(nameof(HarmSources))]
    public void Harm_source_cannot_touch_memere(string typeName)
    {
        var w = TestData.World();
        w.Harm.Log = new();
        var before = MemereSnapshot.Of(w.Memere);
        var src = (IHarmSource)Activator.CreateInstance(Core.GetTypes().Single(t => t.Name == typeName))!;
        for (int i = 0; i < 50; i++) src.ApplyTo(w, w.Memere);
        Assert.Equal(before, MemereSnapshot.Of(w.Memere));
        Assert.DoesNotContain(w.Harm.Log, r => r.TargetId == w.Memere.Id && r.Outcome == HarmOutcome.Applied);
    }

    [Theory]
    [MemberData(nameof(HarmSources))]
    public void Harm_source_actually_goes_through_the_gate(string typeName)
    {
        // proves the tests above mean something: the same source does reach a normal person,
        // through the gate (so the gate saw the memere attempt too)
        var w = TestData.World();
        w.Harm.Log = new();
        w.Player.Infection.GetType(); // player exists
        var src = (IHarmSource)Activator.CreateInstance(Core.GetTypes().Single(t => t.Name == typeName))!;
        src.ApplyTo(w, w.Player);
        src.ApplyTo(w, w.Memere);
        Assert.Contains(w.Harm.Log, r => r.TargetId == w.Memere.Id && r.Outcome == HarmOutcome.RefusedProtected);
    }

    [Theory]
    [MemberData(nameof(HarmKinds))]
    public void Every_kind_of_harm_is_refused(HarmKind kind)
    {
        var w = TestData.World();
        var before = MemereSnapshot.Of(w.Memere);
        var outcome = w.Harm.Apply(w.Memere, new HarmEvent(kind, 1000, "test") { InfectionChance = 1 });
        Assert.Equal(HarmOutcome.RefusedProtected, outcome);
        Assert.Equal(before, MemereSnapshot.Of(w.Memere));
    }

    [Theory]
    [MemberData(nameof(WorldEvents))]
    public void World_event_at_memere_leaves_her_untouched(string typeName)
    {
        var w = TestData.World(seed: 7);
        w.Harm.Log = new();
        var before = MemereSnapshot.Of(w.Memere);
        var ev = (IWorldEvent)Activator.CreateInstance(Core.GetTypes().Single(t => t.Name == typeName))!;
        for (int i = 0; i < 10; i++) ev.Fire(w, w.Memere.Tile);
        for (int i = 0; i < 400; i++) w.Tick(0.25f); // let any spawned zombies act
        Assert.Equal(before.X, w.Memere.X);
        Assert.Equal(before.Y, w.Memere.Y);
        Assert.DoesNotContain(w.Harm.Log, r => r.TargetId == w.Memere.Id && r.Outcome == HarmOutcome.Applied);
        Assert.DoesNotContain(w.Zombies, z => w.ProtectedZones.Contains(z.Tile));
        Assert.DoesNotContain(w.Zombies, z => z.Brain.Target == w.Memere);
    }

    [Fact]
    public void Zombies_cannot_target_memere()
    {
        var w = TestData.World();
        var z = w.TrySpawnZombie(new(12, 10))!;
        Assert.False(Targeting.CanTarget(z, w.Memere));
        Assert.False(Targeting.TrySetTarget(z, w.Memere));
        Assert.Null(z.Brain.Target);
    }

    [Fact]
    public void Zombies_cannot_spawn_or_path_into_her_room()
    {
        var w = TestData.World();
        Assert.Null(w.TrySpawnZombie(w.Memere.Tile));
        foreach (var r in w.ProtectedZones.Rects)
            for (int y = r.MinY; y < r.MaxY; y++)
                for (int x = r.MinX; x < r.MaxX; x++)
                    Assert.Null(w.TrySpawnZombie(new(x, y)));

        // even with every door open, no path for a hostile ends in or crosses her room
        var path = Map.Pathfinder.Find(w.Map, new(12, 10), w.Memere.Tile, p => Targeting.HostileMayEnter(w.ProtectedZones, p));
        Assert.Null(path);
    }
}
