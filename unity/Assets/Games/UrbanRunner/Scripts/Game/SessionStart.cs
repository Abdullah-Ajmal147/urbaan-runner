using GameFactory.Core.Autoplay;
using Newtonsoft.Json.Linq;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>Player settings forwarded from Flutter (session.start → settings, and settings.update).</summary>
    public struct RunSettings
    {
        public bool Music, Sfx, Haptics, ReduceMotion;

        public static RunSettings Defaults => new RunSettings { Music = true, Sfx = true, Haptics = true };

        public static RunSettings Parse(JToken t, RunSettings fallback)
        {
            var o = t as JObject;
            if (o == null) return fallback;
            return new RunSettings
            {
                Music = o.Value<bool?>("music") ?? fallback.Music,
                Sfx = o.Value<bool?>("sfx") ?? fallback.Sfx,
                Haptics = o.Value<bool?>("haptics") ?? fallback.Haptics,
                ReduceMotion = o.Value<bool?>("reduceMotion") ?? fallback.ReduceMotion,
            };
        }
    }

    /// <summary>Parsed session.start payload (docs/bridge-contract.md).</summary>
    public struct SessionStart
    {
        public string SessionId;
        public ulong Seed;
        public RunMode Mode;
        public string Character;
        public string Trail;
        public Upgrades Upgrades;
        public RunConfig Config;
        public RunSettings Settings;
        public TestMode Test;
        /// <summary>Player's best distance so far (v2, optional): the HUD shows "x m to your best".</summary>
        public int BestDistance;

        public static SessionStart Parse(string sessionId, JObject p)
        {
            var cfg = p["config"] as JObject ?? new JObject();
            var up = p["upgrades"] as JObject ?? new JObject();
            var d = RunConfig.Defaults;
            return new SessionStart
            {
                SessionId = sessionId,
                Seed = ParseSeed(p["seed"]),
                Mode = ParseMode((string)p["mode"]),
                Character = (string)p["character"] ?? "mira",
                Trail = (string)p["trail"] ?? "none",
                Upgrades = new Upgrades
                {
                    Magnet = up.Value<int?>("magnet") ?? 0,
                    Shield = up.Value<int?>("shield") ?? 0,
                    Double = up.Value<int?>("double") ?? 0,
                }.Clamped(),
                Config = new RunConfig
                {
                    StartSpeed = cfg.Value<float?>("start_speed") ?? d.StartSpeed,
                    MaxSpeed = cfg.Value<float?>("max_speed") ?? d.MaxSpeed,
                    SpeedRampPerMin = cfg.Value<float?>("speed_ramp_per_min") ?? d.SpeedRampPerMin,
                    RevivesPerRun = cfg.Value<int?>("revives_per_run") ?? d.RevivesPerRun,
                    ReviveEnabled = cfg.Value<bool?>("revive_enabled") ?? d.ReviveEnabled,
                }.Clamped(),
                Settings = RunSettings.Parse(p["settings"], RunSettings.Defaults),
                Test = TestMode.Parse(p["testMode"]),
                BestDistance = System.Math.Max(0, p.Value<int?>("bestDistance") ?? 0),
            };
        }

        static ulong ParseSeed(JToken t)
        {
            if (t == null) return 1;
            if (t.Type == JTokenType.Integer) return unchecked((ulong)(long)t);
            return ulong.TryParse((string)t, out var v) ? v : 1;
        }

        public static RunMode ParseMode(string m) =>
            m == "daily" ? RunMode.Daily : m == "tutorial" ? RunMode.Tutorial : RunMode.Normal;

        public static string ModeName(RunMode m) =>
            m == RunMode.Daily ? "daily" : m == RunMode.Tutorial ? "tutorial" : "normal";
    }
}
