namespace GameFactory.Core.Random
{
    /// <summary>
    /// PCG32 (O'Neill). Deterministic across platforms and Unity versions, unlike UnityEngine.Random,
    /// so a seed always reproduces the same run. A struct with no allocations.
    /// </summary>
    public struct SeededRandom
    {
        ulong _state;
        readonly ulong _inc;

        public SeededRandom(ulong seed, ulong stream = 54u)
        {
            _state = 0;
            _inc = (stream << 1) | 1u;
            NextUInt();
            _state += seed;
            NextUInt();
        }

        public uint NextUInt()
        {
            var old = _state;
            _state = unchecked(old * 6364136223846793005UL + _inc);
            var xorShifted = (uint)(((old >> 18) ^ old) >> 27);
            var rot = (int)(old >> 59);
            return (xorShifted >> rot) | (xorShifted << ((-rot) & 31));
        }

        /// <summary>Uniform int in [min, maxExclusive). Unbiased (rejection sampling).</summary>
        public int Range(int min, int maxExclusive)
        {
            if (maxExclusive <= min) return min;
            var bound = (uint)(maxExclusive - min);
            var threshold = unchecked((uint)-bound) % bound;
            uint r;
            do r = NextUInt(); while (r < threshold);
            return min + (int)(r % bound);
        }

        /// <summary>Uniform float in [0, 1).</summary>
        public float Value() => (NextUInt() >> 8) * (1f / 16777216f);

        public float Range(float min, float max) => min + (max - min) * Value();

        public bool Chance(float p) => Value() < p;

        /// <summary>Derives a seed from a UTC date, so every player gets the same Daily Run.</summary>
        public static ulong DailySeed(int year, int month, int day) =>
            unchecked((ulong)(year * 10000 + month * 100 + day) * 2654435761UL + 0x5DEECE66DUL);
    }
}
