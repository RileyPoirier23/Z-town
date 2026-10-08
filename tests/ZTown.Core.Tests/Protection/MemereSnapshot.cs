using System.Text.Json;
using ZTown.Core.Entities;

namespace ZTown.Core.Tests.Protection;

/// <summary>Everything about memere that harm could conceivably touch, captured for comparison.</summary>
public sealed record MemereSnapshot(float X, float Y, int Z, bool Protected, string Supplies, string Type)
{
    public static MemereSnapshot Of(MemereEntity m) =>
        new(m.X, m.Y, m.Z, m.IsProtected, JsonSerializer.Serialize(m.Supplies.Amounts.OrderBy(k => k.Key)), m.GetType().FullName!);
}
