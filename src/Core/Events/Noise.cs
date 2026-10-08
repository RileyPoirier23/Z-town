using ZTown.Core.Map;

namespace ZTown.Core.Events;

/// <summary>A sound zombies can hear. Radius is in tiles (hearing range, not loudness).</summary>
public readonly record struct Noise(TilePos At, float Radius, string Cause);

public sealed class NoiseBus
{
    readonly List<Noise> _noises = new();
    public IReadOnlyList<Noise> Current => _noises;

    public void Emit(TilePos at, float radius, string cause) => _noises.Add(new Noise(at, radius, cause));

    public void Clear() => _noises.Clear();
}
