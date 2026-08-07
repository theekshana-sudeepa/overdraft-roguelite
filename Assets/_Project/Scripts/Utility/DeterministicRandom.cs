namespace Overdraft.Utility
{
    /// <summary>
    /// xorshift32. Used for gameplay jitter that must stay reproducible from a run seed
    /// (weapon spread, spawn scatter) — UnityEngine.Random is a shared global stream and would
    /// desync a replay the moment any unrelated system rolled a number.
    /// <para>
    /// This is NOT the generator used for waves. Wave generation lives in the pure
    /// Overdraft.Generation assembly and must stay there.
    /// </para>
    /// </summary>
    public struct DeterministicRandom
    {
        uint state;

        public DeterministicRandom(int seed)
        {
            // Zero is an absorbing state for xorshift, so it is remapped rather than allowed.
            state = seed == 0 ? 0x9E3779B9u : unchecked((uint)seed);
        }

        public uint NextUInt()
        {
            uint x = state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            state = x;
            return x;
        }

        /// <summary>Uniform in [0,1).</summary>
        public float NextFloat()
        {
            const float inv = 1f / 4294967296f;
            return NextUInt() * inv;
        }

        /// <summary>Uniform in [min,max).</summary>
        public float Range(float min, float max)
        {
            return min + (max - min) * NextFloat();
        }

        /// <summary>Uniform in [min,max).</summary>
        public int Range(int min, int max)
        {
            if (max <= min) { return min; }
            return min + (int)(NextUInt() % (uint)(max - min));
        }
    }
}
