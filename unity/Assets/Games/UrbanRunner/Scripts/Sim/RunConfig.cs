using System;

namespace UrbanRunner.Sim
{
    public enum RunMode { Normal, Daily, Tutorial }

    /// <summary>Gameplay tuning from session.start → config (Remote Config values, already clamped by Flutter).</summary>
    public struct RunConfig
    {
        public float StartSpeed;
        public float MaxSpeed;
        public float SpeedRampPerMin;
        public int RevivesPerRun;
        public bool ReviveEnabled;

        public static RunConfig Defaults => new RunConfig
        {
            StartSpeed = 8f,
            MaxSpeed = 22f,
            SpeedRampPerMin = 3f,
            RevivesPerRun = 1,
            ReviveEnabled = true,
        };

        /// <summary>Defence in depth: Unity clamps again to the HANDOVER §7.3 ranges.</summary>
        public RunConfig Clamped()
        {
            var c = this;
            c.StartSpeed = Clamp(c.StartSpeed, 5f, 12f, 8f);
            c.MaxSpeed = Clamp(c.MaxSpeed, 12f, 30f, 22f);
            if (c.MaxSpeed < c.StartSpeed) c.MaxSpeed = c.StartSpeed;
            c.SpeedRampPerMin = Clamp(c.SpeedRampPerMin, 0.5f, 6f, 3f);
            c.RevivesPerRun = Math.Max(0, Math.Min(2, c.RevivesPerRun));
            return c;
        }

        static float Clamp(float v, float min, float max, float fallback) =>
            float.IsNaN(v) || float.IsInfinity(v) ? fallback : Math.Max(min, Math.Min(max, v));
    }

    /// <summary>Power-up upgrade levels 0–5 (bought in Flutter).</summary>
    public struct Upgrades
    {
        public int Magnet;
        public int Shield;
        public int Double;

        public Upgrades Clamped() => new Upgrades
        {
            Magnet = Math.Max(0, Math.Min(5, Magnet)),
            Shield = Math.Max(0, Math.Min(5, Shield)),
            Double = Math.Max(0, Math.Min(5, Double)),
        };
    }

    /// <summary>HANDOVER §3.6.</summary>
    public static class DifficultyCurve
    {
        /// <summary>speed = min(start + ramp × minutes, max)</summary>
        public static float Speed(in RunConfig c, double seconds) =>
            (float)Math.Min(c.StartSpeed + c.SpeedRampPerMin * (seconds / 60.0), c.MaxSpeed);

        /// <summary>Tier 1 at 0–500 m, tier 2 at 500–1,500 m, tier 3 beyond.</summary>
        public static int Tier(double distance) => distance < 500 ? 1 : distance < 1500 ? 2 : 3;

        /// <summary>Distance covered after <paramref name="seconds"/> (closed form of ∫speed dt).</summary>
        public static double DistanceAt(in RunConfig c, double seconds)
        {
            var a = c.SpeedRampPerMin / 60.0;
            var tCap = a > 0 ? (c.MaxSpeed - c.StartSpeed) / a : double.MaxValue;
            if (seconds <= tCap) return c.StartSpeed * seconds + 0.5 * a * seconds * seconds;
            return c.StartSpeed * tCap + 0.5 * a * tCap * tCap + c.MaxSpeed * (seconds - tCap);
        }

        /// <summary>Inverse of <see cref="DistanceAt"/>: the run time at which the player reaches a distance.</summary>
        public static double TimeAt(in RunConfig c, double distance)
        {
            var a = c.SpeedRampPerMin / 60.0;
            var tCap = a > 0 ? (c.MaxSpeed - c.StartSpeed) / a : double.MaxValue;
            var dCap = a > 0 ? DistanceAt(c, tCap) : double.MaxValue;
            if (distance <= dCap)
            {
                if (a <= 0) return distance / c.StartSpeed;
                // 0.5 a t² + v0 t − d = 0
                return (-c.StartSpeed + Math.Sqrt(c.StartSpeed * c.StartSpeed + 2 * a * distance)) / a;
            }
            return tCap + (distance - dCap) / c.MaxSpeed;
        }
    }

    /// <summary>Power-up durations (HANDOVER §3.5).</summary>
    public static class PowerUpRules
    {
        public static float Duration(PowerUpKind kind, in Upgrades u)
        {
            switch (kind)
            {
                case PowerUpKind.Magnet: return 8f + 1.5f * u.Magnet;
                case PowerUpKind.Shield: return 20f + 3f * u.Shield;
                default: return 10f + 2f * u.Double;
            }
        }

        public const float MinSpacingM = 250f;
        public const float MaxSpacingM = 400f;
    }
}
