using Newtonsoft.Json.Linq;

namespace GameFactory.Core.Autoplay
{
    /// <summary>A bot that produces one input per frame for a game.</summary>
    public interface IAutoplayAgent<out TInput>
    {
        TInput Decide();
    }

    /// <summary>
    /// session.start → testMode (bridge contract): drives automated runs on any game.
    /// Missing values mean "off".
    /// </summary>
    public struct TestMode
    {
        public bool Autoplay;
        public float Skill;
        public float EndAfterSeconds;
        public float ForceCrashAtSeconds;

        public bool Active => Autoplay || EndAfterSeconds > 0 || ForceCrashAtSeconds > 0;

        public static TestMode Parse(JToken token)
        {
            var o = token as JObject;
            if (o == null) return default;
            return new TestMode
            {
                Autoplay = o.Value<bool?>("autoplay") ?? false,
                Skill = Clamp01(o.Value<float?>("skill") ?? 1f),
                EndAfterSeconds = o.Value<float?>("endAfterSeconds") ?? 0f,
                ForceCrashAtSeconds = o.Value<float?>("forceCrashAtSeconds") ?? 0f,
            };
        }

        public JObject ToJson() => new JObject
        {
            ["autoplay"] = Autoplay,
            ["skill"] = Skill,
            ["endAfterSeconds"] = EndAfterSeconds,
            ["forceCrashAtSeconds"] = ForceCrashAtSeconds,
        };

        static float Clamp01(float v) => v < 0 ? 0 : v > 1 ? 1 : v;
    }
}
