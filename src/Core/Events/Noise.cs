using ZTown.Core.Map;

namespace ZTown.Core.Events;

/// <summary>A sound zombies can hear. Radius is in tiles (hearing range, not loudness).</summary>
public readonly record struct Noise(TilePos At, float Radius, string Cause);

public sealed class NoiseBus
{
    readonly List<Noise> _noises = new();
    public IReadOnlyList<Noise> Current => _noises;

    /// <summary>Everything that made a sound since the game layer last drained it (for audio).</summary>
    public List<Noise> Log { get; } = new();

    public void Emit(TilePos at, float radius, string cause)
    {
        var n = new Noise(at, radius, cause);
        _noises.Add(n);
        if (cause != "footsteps" && Log.Count < 256) Log.Add(n);
    }

    public void Clear() => _noises.Clear();
}
