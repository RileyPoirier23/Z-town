using ZTown.Core.Entities;
using ZTown.Core.Map;
using ZTown.Core.Zombies;

namespace ZTown.Core.Protection;

/// <summary>
/// The only place AI picks who to go after. Protected entities are never valid targets, and
/// protected zones (memere's rooms) are never walkable for hostile AI.
/// </summary>
public static class Targeting
{
    public static bool CanTarget(Entity attacker, Entity target) =>
        !target.IsProtected && target is Living l && !l.IsDead && target != attacker && target.Kind != EntityKind.Zombie;

    /// <summary>Sets a zombie's target. Returns false (and clears the target) if not allowed.</summary>
    public static bool TrySetTarget(Zombie z, Entity? target)
    {
        if (target is null || !CanTarget(z, target))
        {
            z.Brain.Target = null;
            return target is null;
        }
        z.Brain.Target = target;
        return true;
    }

    public static bool HostileMayEnter(ProtectedZones zones, TilePos p) => !zones.Contains(p);
}

/// <summary>Areas hostile AI can never path into or stand in (memere's safe rooms).</summary>
public sealed class ProtectedZones
{
    readonly List<TileRect> _rects = new();
    public IReadOnlyList<TileRect> Rects => _rects;

    public void Add(TileRect r) => _rects.Add(r);

    public bool Contains(TilePos p)
    {
        foreach (var r in _rects) if (r.Contains(p)) return true;
        return false;
    }
}
