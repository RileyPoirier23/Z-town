using System.Reflection;
using ZTown.Core.Events;
using ZTown.Core.Map;
using ZTown.Core.Protection;

namespace ZTown.Core.Tests.Protection;

/// <summary>
/// Memere protection, part 3: run the world for days with hordes, breaches, fires, gunfire and
/// car crashes going off around (and on) her, and check every tick that she's untouched.
/// SOAK_DAYS / SOAK_SEEDS make it longer (CI's nightly run uses more).
/// </summary>
public class SoakTests
{
    static int Env(string name, int fallback) => int.TryParse(Environment.GetEnvironmentVariable(name), out var v) ? v : fallback;

    [Fact]
    public void Days_of_chaos_never_touch_memere()
    {
        int days = Env("SOAK_DAYS", 2), seeds = Env("SOAK_SEEDS", 3);
        var events = typeof(GameWorld).Assembly.GetTypes()
            .Where(t => typeof(IWorldEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface)
            .Select(t => (IWorldEvent)Activator.CreateInstance(t)!).ToList();

        for (ulong seed = 1; seed <= (ulong)seeds; seed++)
        {
            var w = TestData.World(seed, zombies: 25);
            w.Harm.Log = null;
            w.RemoveDeadZombies = true;
            var start = MemereSnapshot.Of(w.Memere);
            int refusalsSeen = 0;
            const float dt = 0.25f;
            int ticks = (int)(days * 86400 / w.Data.Sim.TimeScale / dt);

            for (int i = 0; i < ticks; i++)
            {
                // chaos: an event every ~30 real seconds, a third of them right on memere
                if (i % 120 == 0)
                {
                    var ev = w.Rng.Pick(events);
                    var at = w.Rng.Chance(0.33) ? w.Memere.Tile : new TilePos(w.Rng.Range(0, w.Map.Width), w.Rng.Range(0, w.Map.Height));
                    ev.Fire(w, at);
                }
                // the player wanders around and gets back up if they go down (we're testing memere, not them)
                w.MovePlayer(MathF.Cos(i * 0.01f), MathF.Sin(i * 0.013f), MoveMode.Walk, dt);
                if (w.Player.IsDead) w.Player.Health.Heal(100);
                if (w.Zombies.Count() > 120) foreach (var z in w.Zombies.Skip(120).ToList()) w.Remove(z);

                w.Tick(dt);

                foreach (var z in w.Zombies)
                {
                    Assert.False(z.Brain.Target == w.Memere, $"seed {seed} tick {i}: a zombie targeted memere");
                    Assert.False(w.ProtectedZones.Contains(z.Tile), $"seed {seed} tick {i}: a zombie is in memere's room at {z.Tile}");
                }
                Assert.Equal(start.X, w.Memere.X);
                Assert.Equal(start.Y, w.Memere.Y);
                Assert.True(w.Memere.IsProtected);
            }
            refusalsSeen = w.Harm.ProtectedRefusals.Count;
            // the chaos really did aim at her (otherwise this test proves nothing)
            Assert.True(refusalsSeen > 0, $"seed {seed}: nothing was ever aimed at memere; soak test isn't testing");
            Assert.All(w.Harm.ProtectedRefusals, r => Assert.Equal(HarmOutcome.RefusedProtected, r.Outcome));
        }
    }
}
