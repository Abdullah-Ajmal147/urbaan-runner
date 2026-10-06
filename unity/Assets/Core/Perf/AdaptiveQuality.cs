using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameFactory.Core.Perf
{
    /// <summary>One rung of the quality ladder (D31).</summary>
    public readonly struct QualityStep
    {
        public QualityStep(float renderScale, bool postFx, int targetFps)
        {
            RenderScale = renderScale;
            PostFx = postFx;
            TargetFps = targetFps;
        }

        public readonly float RenderScale;
        public readonly bool PostFx;
        public readonly int TargetFps;
    }

    /// <summary>
    /// Pure decision logic for adaptive quality: feed it frame times, it says which step to use. Steps down fast
    /// when the game can't hold its target, steps up slowly when there is clear headroom. No allocations.
    /// </summary>
    public sealed class AdaptiveQualityPolicy
    {
        public static readonly QualityStep[] Ladder =
        {
            new QualityStep(0.85f, true, 60),
            new QualityStep(0.75f, true, 60),
            new QualityStep(0.65f, false, 60),
            new QualityStep(0.55f, false, 60),
            new QualityStep(0.55f, false, 30), // last resort: a steady 30 beats a ragged 40–55
        };

        public const float WindowS = 2f;
        public const int UpWindows = 4;      // 8 s of headroom before stepping up
        public const float DownRatio = 0.86f; // below 86 % of the target (e.g. < 51.6 fps at 60) → step down
        public const float UpRatio = 0.97f;   // at least 97 % of the target to count as headroom

        public int Level { get; private set; }
        public QualityStep Current => Ladder[Level];

        float _sum;
        int _frames;
        int _goodWindows;
        float _cooldown;

        public AdaptiveQualityPolicy(int startLevel = 0) => Level = Mathf.Clamp(startLevel, 0, Ladder.Length - 1);

        /// <summary>Restarts measuring (new run, or after a pause/load hitch that shouldn't count).</summary>
        public void ResetWindow(float cooldown = 1f)
        {
            _sum = 0f;
            _frames = 0;
            _goodWindows = 0;
            _cooldown = cooldown;
        }

        /// <summary>Feed one frame's real duration. Returns true when the level changed.</summary>
        public bool Sample(float frameSeconds)
        {
            if (frameSeconds <= 0f || frameSeconds > 0.5f) return false; // pauses, loads: not representative
            if (_cooldown > 0f) { _cooldown -= frameSeconds; return false; }
            _sum += frameSeconds;
            _frames++;
            if (_sum < WindowS) return false;

            var fps = _frames / _sum;
            _sum = 0f;
            _frames = 0;
            var target = Current.TargetFps;
            if (fps < target * DownRatio && Level < Ladder.Length - 1)
            {
                Level++;
                _goodWindows = 0;
                _cooldown = 1.5f; // let the new setting take effect before judging it
                return true;
            }
            // The 30-fps step can't show headroom (it's capped), so it never climbs back within a session.
            if (fps >= target * UpRatio && Level > 0 && Current.TargetFps == Ladder[Level - 1].TargetFps)
            {
                if (++_goodWindows >= UpWindows)
                {
                    Level--;
                    _goodWindows = 0;
                    _cooldown = 1.5f;
                    return true;
                }
            }
            else _goodWindows = 0;
            return false;
        }
    }

    /// <summary>
    /// Applies <see cref="AdaptiveQualityPolicy"/> to URP's render scale, the post-processing volume and the target
    /// frame rate. Remembers the last level between sessions, so a weak phone starts where it ended. In the Editor it
    /// decides but doesn't touch the URP asset (that would edit the project file).
    /// </summary>
    public sealed class AdaptiveQuality : MonoBehaviour
    {
        const string PrefKey = "gf.quality.level";

        AdaptiveQualityPolicy _policy;
        Volume _postFx;
        float _defaultScale = 1f;
        UniversalRenderPipelineAsset _urp;

        public bool Active { get; set; }
        public int Level => _policy?.Level ?? 0;
        public System.Action<QualityStep, int> Changed;

        public void Init(Volume postFx, int floorLevel)
        {
            _postFx = postFx;
            _urp = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (_urp != null) _defaultScale = _urp.renderScale;
            // Start where the last session ended, but never on the 30-fps rung: a hot phone last time shouldn't
            // lock the game to 30 forever. Each session re-earns that step if it's really needed.
            var saved = Mathf.Min(PlayerPrefs.GetInt(PrefKey, 0), AdaptiveQualityPolicy.Ladder.Length - 2);
            _policy = new AdaptiveQualityPolicy(Mathf.Max(saved, floorLevel));
            Apply();
        }

        /// <summary>Call when a run starts or resumes: the frames before don't count.</summary>
        public void Restart() => _policy?.ResetWindow();

        void Update()
        {
            if (!Active || _policy == null) return;
            if (_policy.Sample(Time.unscaledDeltaTime))
            {
                Apply();
                PlayerPrefs.SetInt(PrefKey, _policy.Level);
            }
        }

        void Apply()
        {
            var s = _policy.Current;
            Application.targetFrameRate = s.TargetFps;
            if (_postFx != null) _postFx.enabled = s.PostFx;
            // Pixel budget: very tall (1440p-class) screens get the same internal resolution as ~2400 px ones.
            var budget = Mathf.Min(1f, 2400f / Mathf.Max(1, Screen.height));
            if (_urp != null && !Application.isEditor) _urp.renderScale = s.RenderScale * budget;
            Debug.Log($"UR_PERF quality_level={_policy.Level} render_scale={s.RenderScale:0.00} postfx={(s.PostFx ? 1 : 0)} target_fps={s.TargetFps}");
            Changed?.Invoke(s, _policy.Level);
        }

        void OnDestroy()
        {
            if (_urp != null && !Application.isEditor) _urp.renderScale = _defaultScale;
        }
    }
}
