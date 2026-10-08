using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;
using ZTown.Core.Entities;
using ZTown.Core.Events;
using ZTown.Core.Protection;
using ZTown.Core.Save;
using ZTown.Core.Zombies;

namespace ZTown.Core.Tests.Protection;

/// <summary>
/// Memere protection, part 1: the code is shaped so she can't be hurt. These read the compiled
/// Core assembly and fail if someone adds a way around the harm gate.
/// </summary>
public class ArchitectureTests
{
    static readonly Assembly Core = typeof(GameWorld).Assembly;

    static readonly string[] HarmWords = { "health", "hp", "injur", "wound", "bleed", "infect", "sick", "ill", "pain", "dead", "death", "die" };

    [Fact]
    public void Memere_is_protected_and_nothing_else_is()
    {
        Assert.True(new MemereEntity().IsProtected);
        foreach (var t in Core.GetTypes().Where(t => typeof(Entity).IsAssignableFrom(t) && !t.IsAbstract && t != typeof(MemereEntity)))
        {
            var e = (Entity)Activator.CreateInstance(t)!;
            Assert.False(e.IsProtected, $"{t.Name} is protected; only memere should be");
        }
    }

    [Fact]
    public void Memere_has_no_body_that_can_be_hurt()
    {
        var t = typeof(MemereEntity);
        Assert.True(t.IsSealed, "MemereEntity must be sealed so nothing can subclass her into something harmable");
        Assert.False(typeof(Living).IsAssignableFrom(t), "Memere must not be Living (Living has Health and Infection)");

        // nothing reachable from her state is a Health/Infection, or is named like one
        var seen = new HashSet<Type>();
        void Check(Type type, string path)
        {
            if (!seen.Add(type) || type.Assembly != Core) return;
            Assert.False(type == typeof(Health) || type == typeof(Infection) || type == typeof(Wounds), $"memere state reaches {type.Name} via {path}");
            foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                Assert.False(HarmWords.Any(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase)),
                    $"memere state has a harm-like member {path}.{p.Name}");
                Check(p.PropertyType, $"{path}.{p.Name}");
            }
        }
        Check(t, "Memere");
        // her save format can't carry harm either
        foreach (var p in typeof(MemereSave).GetProperties())
            Assert.False(HarmWords.Any(w => p.Name.Contains(w, StringComparison.OrdinalIgnoreCase)), $"MemereSave.{p.Name}");
    }

    /// <summary>Methods (by "Type::Name") that may only be called from the listed types.</summary>
    static readonly Dictionary<string, string[]> Restricted = new()
    {
        ["ZTown.Core.Entities.Health::Reduce"] = new[] { "ZTown.Core.Protection.HarmGate" },
        ["ZTown.Core.Entities.Infection::Infect"] = new[] { "ZTown.Core.Protection.HarmGate" },
        ["ZTown.Core.Entities.Infection::Advance"] = new[] { "ZTown.Core.Protection.HarmGate" },
        ["ZTown.Core.Entities.Wounds::Add"] = new[] { "ZTown.Core.Protection.HarmGate" },
        // Restore sets values when loading a save or spawning
        ["ZTown.Core.Entities.Health::Restore"] = new[] { "ZTown.Core.Save.SaveSystem", "ZTown.Core.GameWorld" },
        ["ZTown.Core.Entities.Infection::Restore"] = new[] { "ZTown.Core.Save.SaveSystem" },
        // only Targeting decides who a zombie goes after
        ["ZTown.Core.Zombies.ZombieBrain::set_Target"] = new[] { "ZTown.Core.Protection.Targeting" },
    };

    [Fact]
    public void Only_the_harm_gate_hurts_and_only_targeting_aims()
    {
        using var asm = AssemblyDefinition.ReadAssembly(Core.Location);
        var violations = new List<string>();
        foreach (var type in AllTypes(asm.MainModule.Types))
            foreach (var method in type.Methods.Where(m => m.HasBody))
                foreach (var ins in method.Body.Instructions)
                {
                    if (ins.Operand is not MethodReference callee) continue;
                    var key = $"{callee.DeclaringType.FullName}::{callee.Name}";
                    if (!Restricted.TryGetValue(key, out var allowed)) continue;
                    var caller = Outer(type).FullName;
                    if (!allowed.Contains(caller)) violations.Add($"{type.FullName}.{method.Name} calls {key}");
                }
        Assert.True(violations.Count == 0, "Bypasses the protection gate:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void Restricted_members_still_exist()
    {
        // if one is renamed, the IL test above would silently stop checking it
        Assert.NotNull(typeof(Health).GetMethod("Reduce", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(typeof(Infection).GetMethod("Infect", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(typeof(Infection).GetMethod("Advance", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(typeof(Wounds).GetMethod("Add", BindingFlags.NonPublic | BindingFlags.Instance));
        var target = typeof(ZombieBrain).GetProperty(nameof(ZombieBrain.Target))!;
        Assert.False(target.SetMethod!.IsPublic, "ZombieBrain.Target must not be publicly settable");
        Assert.False(typeof(Health).GetProperty(nameof(Health.Value))!.SetMethod!.IsPublic);
    }

    [Fact]
    public void Every_harm_source_and_world_event_can_be_found_and_run_by_the_tests()
    {
        var harm = Core.GetTypes().Where(t => typeof(IHarmSource).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).ToList();
        var events = Core.GetTypes().Where(t => typeof(IWorldEvent).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface).ToList();
        Assert.NotEmpty(harm);
        Assert.NotEmpty(events);
        foreach (var t in harm.Concat(events))
            Assert.True(t.GetConstructor(Type.EmptyTypes) != null, $"{t.Name} needs a public parameterless constructor so the protection tests can run it");
    }

    static IEnumerable<TypeDefinition> AllTypes(IEnumerable<TypeDefinition> types)
    {
        foreach (var t in types)
        {
            yield return t;
            foreach (var n in AllTypes(t.NestedTypes)) yield return n;
        }
    }

    /// <summary>Lambdas and iterators compile into nested types; attribute them to the outer type.</summary>
    static TypeDefinition Outer(TypeDefinition t)
    {
        while (t.DeclaringType != null) t = t.DeclaringType;
        return t;
    }
}
