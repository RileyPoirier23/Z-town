namespace ZTown.Core.Sim;

/// <summary>Deterministic RNG (PCG32). Its state is saved so a loaded game rolls the same.</summary>
public sealed class Rng
{
    const ulong Mult = 6364136223846793005UL;
    const ulong Inc = 1442695040888963407UL;

    public ulong State { get; private set; }

    public Rng(ulong seed)
    {
        State = 0;
        NextUInt();
        State += seed;
        NextUInt();
    }

    public static Rng FromState(ulong state) => new(0) { State = state };

    internal void Restore(ulong state) => State = state;

    public uint NextUInt()
    {
        ulong old = State;
        State = old * Mult + Inc;
        uint xorshifted = (uint)(((old >> 18) ^ old) >> 27);
        int rot = (int)(old >> 59);
        return (xorshifted >> rot) | (xorshifted << (-rot & 31));
    }

    /// <summary>[0, 1)</summary>
    public double NextDouble() => NextUInt() / 4294967296.0;

    public float NextFloat() => (float)NextDouble();

    /// <summary>[min, max)</summary>
    public int Range(int min, int max) => max <= min ? min : min + (int)(NextDouble() * (max - min));

    public float Range(float min, float max) => min + (float)NextDouble() * (max - min);

    public bool Chance(double p) => NextDouble() < p;

    public T Pick<T>(IReadOnlyList<T> items) => items[Range(0, items.Count)];
}
