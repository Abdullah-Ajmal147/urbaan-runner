namespace GameFactory.Core.World
{
    /// <summary>
    /// Keeps render coordinates small on endless tracks. Logic uses absolute distance (double);
    /// rendering uses <see cref="ToWorld"/>, which subtracts an offset that jumps every <see cref="Interval"/> metres.
    /// </summary>
    public sealed class FloatingOrigin
    {
        public double Interval { get; }
        public double Offset { get; private set; }
        public int Shifts { get; private set; }

        public FloatingOrigin(double interval = 500.0) { Interval = interval; }

        /// <summary>Call once per frame with the player's absolute position. Returns true if the origin moved.</summary>
        public bool Update(double playerAbsolute)
        {
            if (playerAbsolute - Offset < Interval) return false;
            while (playerAbsolute - Offset >= Interval) Offset += Interval;
            Shifts++;
            return true;
        }

        public float ToWorld(double absolute) => (float)(absolute - Offset);

        public void Reset()
        {
            Offset = 0;
            Shifts = 0;
        }
    }
}
