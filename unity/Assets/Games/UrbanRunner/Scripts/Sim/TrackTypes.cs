namespace UrbanRunner.Sim
{
    /// <summary>What occupies one lane of one row.</summary>
    public enum Obstacle : byte
    {
        None = 0,
        Crate = 1,   // low crate stack: jump
        Banner = 2,  // hanging banner/sign: slide
        Stall = 3,   // market stall: change lane
        Van = 4,     // parked van: change lane
        Cart = 5,    // rolling cart moving toward the player: change lane (tier 3+)
    }

    public enum PowerUpKind : byte { Magnet = 0, Shield = 1, DoubleSparks = 2 }

    /// <summary>Tunable physical rules. Shared by the simulation, the generator and the guarantee tests.</summary>
    public static class TrackRules
    {
        public const int LaneCount = 3;
        public const float LaneWidth = 2.5f;

        public const float LaneChangeDuration = 0.12f;
        public const float InputBufferS = 0.15f;

        public const float JumpDuration = 0.62f;
        public const float JumpHeight = 1.6f;
        public const float SlideDuration = 0.65f;

        public const float PlayerHalfWidth = 0.35f;
        public const float PlayerHalfDepth = 0.4f;
        public const float StandingHeight = 1.8f;
        public const float SlidingHeight = 0.8f;
        public const float ObstacleHalfWidth = 0.9f;

        public const float CrateHeight = 0.9f;
        public const float BannerBottom = 1.0f;

        /// <summary>Minimum time between two required actions (HANDOVER §3.4: gap ≥ speed × 0.7 s).</summary>
        public const float MinActionGapS = 0.7f;

        /// <summary>The generator targets this per gap beat, leaving a margin above <see cref="MinActionGapS"/>.</summary>
        public const float BeatS = 0.8f;

        public const float CartSpeed = 4f;

        public const float SparkSpacing = 2f;

        /// <summary>
        /// Upper bound on sparks per metre a run can produce, with Double Sparks always on.
        /// Flutter uses this to sanity-check results (bridge contract rule 3). Sparks lines are at most
        /// one spark per <see cref="SparkSpacing"/> m, ×2 for Double Sparks, plus slack for arcs.
        /// </summary>
        public const float MaxSparksPerMetre = 1.2f;

        public static float LaneX(int lane) => (lane - 1) * LaneWidth;

        public static float Length(Obstacle o)
        {
            switch (o)
            {
                case Obstacle.Crate: return 1.0f;
                case Obstacle.Banner: return 0.3f;
                case Obstacle.Stall: return 2.5f;
                case Obstacle.Van: return 4.0f;
                case Obstacle.Cart: return 1.6f;
                default: return 0f;
            }
        }

        /// <summary>Lane can be passed by just running through it, or by a jump/slide.</summary>
        public static bool IsPassable(Obstacle o) => o == Obstacle.None || o == Obstacle.Crate || o == Obstacle.Banner;

        public static bool IsBlocking(Obstacle o) => o == Obstacle.Stall || o == Obstacle.Van || o == Obstacle.Cart;
    }

    /// <summary>One generated row of the track. Value type kept in a ring buffer: no allocations.</summary>
    public struct TrackRow
    {
        /// <summary>Absolute distance (m) where the row starts.</summary>
        public double Z;
        public Obstacle L0, L1, L2;
        /// <summary>Bit per lane cleared by a shield hit or a revive.</summary>
        public byte Cleared;
        public int Tier;
        public int Id;

        public Obstacle Get(int lane) => lane == 0 ? L0 : lane == 1 ? L1 : L2;

        public void Set(int lane, Obstacle o)
        {
            if (lane == 0) L0 = o;
            else if (lane == 1) L1 = o;
            else L2 = o;
        }

        /// <summary>The obstacle that actually blocks, accounting for cleared lanes.</summary>
        public Obstacle Effective(int lane) => (Cleared & (1 << lane)) != 0 ? Obstacle.None : Get(lane);

        /// <summary>Absolute distance where the longest obstacle in the row ends.</summary>
        public double End
        {
            get
            {
                var len = TrackRules.Length(L0);
                var l1 = TrackRules.Length(L1);
                var l2 = TrackRules.Length(L2);
                if (l1 > len) len = l1;
                if (l2 > len) len = l2;
                return Z + len;
            }
        }

        public bool IsEmpty => L0 == Obstacle.None && L1 == Obstacle.None && L2 == Obstacle.None;
    }

    public struct Spark
    {
        public int Id;
        public double Z;
        public byte Lane;
        public float Y;
        public bool Collected;
    }

    public struct PowerUpPickup
    {
        public int Id;
        public double Z;
        public byte Lane;
        public PowerUpKind Kind;
        public bool Collected;
    }
}
