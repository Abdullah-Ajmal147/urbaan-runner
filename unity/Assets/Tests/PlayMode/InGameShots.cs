using System.Collections;
using System.IO;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UrbanRunner.Game;
using UrbanRunner.Sim;

namespace Tests.PlayMode
{
    /// <summary>
    /// Design tool, not a gate: plays the real Run scene and saves phone-sized frames (1080×2400, HUD included)
    /// to build/shots/ingame/. [Explicit], so test_all skips it. Run:
    ///   ur_unity shots -runTests -testPlatform PlayMode -testFilter InGameShots -testResults build/test-results/shots.xml
    /// </summary>
    [Explicit]
    public class InGameShots
    {
        const int W = 1080, H = 2400;
        StandaloneHostTransport _host;
        GameRoot _root;
        string _dir;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _host = new StandaloneHostTransport();
            FlutterBridge.TransportOverride = _host;
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
            _root = Object.FindAnyObjectByType<GameRoot>();
            _dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../build/shots/ingame"));
            Directory.CreateDirectory(_dir);
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            FlutterBridge.TransportOverride = null;
            if (FlutterBridge.Instance != null) Object.Destroy(FlutterBridge.Instance.gameObject);
            yield return null;
        }

        const int SW = 432, SH = 960, Cols = 8;
        Texture2D _sheet;
        int _sheetCount;

        /// <summary>Which roots the visible draws come from (renderers in the frustum × sub-meshes × passes).</summary>
        void LogDrawBudget()
        {
            var cam = Camera.main;
            var planes = GeometryUtility.CalculateFrustumPlanes(cam);
            var byRoot = new System.Collections.Generic.Dictionary<string, int>();
            var total = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || r is ParticleSystemRenderer && !((ParticleSystemRenderer)r).isVisible) continue;
                if (!GeometryUtility.TestPlanesAABB(planes, r.bounds)) continue;
                var draws = 0;
                foreach (var m in r.sharedMaterials) if (m != null) draws += m.passCount > 1 && m.shader.name.Contains("Outline") ? 2 : 1;
                var t = r.transform;
                while (t.parent != null && t.parent.parent != null && t.parent.name != "Track") t = t.parent;
                var key = t.name.StartsWith("Segment") ? "Segment" : t.name;
                byRoot[key] = (byRoot.TryGetValue(key, out var v) ? v : 0) + draws;
                total += draws;
            }
            var sb = new System.Text.StringBuilder("UR_PERF draw_budget total=" + total);
            foreach (var kv in byRoot) sb.Append(' ').Append(kv.Key).Append('=').Append(kv.Value);
            Debug.Log(sb.ToString());
        }

        IEnumerator Shot(string name)
        {
            // WaitForEndOfFrame never fires in batchmode; queue this frame's instanced draws (sparks) by hand.
            yield return null;
            _root.Track.DrawSparks();
            Capture(name);
        }

        void Capture(string name)
        {
            File.WriteAllBytes(Path.Combine(_dir, name + ".png"), Render(W, H).EncodeToPNG());
            var small = Render(SW, SH);
            _sheet ??= new Texture2D(SW * Cols, SH, TextureFormat.RGB24, false);
            if (_sheetCount < Cols) _sheet.SetPixels(_sheetCount++ * SW, 0, SW, SH, small.GetPixels());
            Debug.Log("UR_GAME shot " + name);
        }

        void SaveSheet()
        {
            _sheet.Apply();
            File.WriteAllBytes(Path.Combine(_dir, "sheet.png"), _sheet.EncodeToPNG());
        }

        Texture2D Render(int w, int h)
        {
            var cam = Camera.main;
            var canvas = _root.Hud.GetComponentInChildren<Canvas>();
            var mode = canvas.renderMode;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = cam.nearClipPlane + 0.05f;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            canvas.renderMode = mode;
            rt.Release();
            return tex;
        }

        IEnumerator Until(System.Func<bool> c, float realSeconds = 60)
        {
            var end = Time.realtimeSinceStartup + realSeconds;
            while (!c() && Time.realtimeSinceStartup < end) yield return null;
        }

        void Start(JObject testMode, int seed = 2026)
        {
            _host.HostSend(new BridgeEnvelope("session.start", "shots-" + seed, new JObject
            {
                ["seed"] = seed, ["mode"] = "normal", ["character"] = "mira", ["trail"] = "trail_neon",
                ["bestDistance"] = 420,
                ["hunt"] = new JObject { ["colour"] = 1, ["found"] = 1 },
                ["upgrades"] = new JObject { ["magnet"] = 0, ["shield"] = 0, ["double"] = 0 },
                ["config"] = new JObject { ["start_speed"] = 8, ["max_speed"] = 22, ["speed_ramp_per_min"] = 3, ["revives_per_run"] = 1, ["revive_enabled"] = true },
                ["settings"] = new JObject { ["music"] = false, ["sfx"] = false, ["haptics"] = false, ["reduceMotion"] = false },
                ["testMode"] = testMode,
            }).ToJson());
        }

        [UnityTest]
        public IEnumerator Shots()
        {
            yield return null;
            Capture("00_idle");
            Start(new JObject { ["autoplay"] = true, ["skill"] = 1.0, ["endAfterSeconds"] = 400 });
            var sim = (System.Func<RunSimulation>)(() => _root.Session.Sim);
            yield return Until(() => sim() != null && sim().Time > 1.5);
            yield return Shot("01_start"); // the Warden's opening chase
            yield return Until(() => sim().Airborne && sim().JumpProgress > 0.4f);
            yield return Shot("02_jump");
            yield return Until(() => sim().Sliding && sim().SlideProgress > 0.3f);
            yield return Shot("03_slide");
            yield return Until(() => sim().OnRoof && sim().Y > 2f, 90);
            yield return Shot("04_roof");
            Time.timeScale = 6f;
            yield return Until(() => sim().Time > 150, 120);
            Time.timeScale = 1f;
            yield return null;
            yield return Shot("05_fast");
            LogDrawBudget();
            if (sim().Distance > 0) sim().ForceCrash();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("06_crash");
            SaveSheet();
        }
    }
}
