using GameFactory.Core;
using GameFactory.Core.Bridge;
using GameFactory.Core.Input;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UrbanRunner.Spike
{
    /// <summary>
    /// M0 bridge spike: a rotating cube that answers pings, reports swipes, and obeys pause/resume.
    /// </summary>
    public sealed class SpikeController : MonoBehaviour
    {
        [SerializeField] Transform cube;
        [SerializeField] Renderer cubeRenderer;
        [SerializeField] PointerSwipeSource swipes;

        static readonly Color[] SwipeColors =
        {
            new Color32(0xFF, 0x3D, 0xA6, 0xFF), // neon-magenta
            new Color32(0x2E, 0xE6, 0xFF, 0xFF), // neon-cyan
            new Color32(0xFF, 0xB5, 0x47, 0xFF), // lantern-amber
            new Color32(0x3D, 0xFF, 0xA2, 0xFF), // success
        };

        bool _paused;
        string _lastSwipe = "-";
        int _pings;
        int _swipeCount;
        MaterialPropertyBlock _mpb;

        void Start()
        {
            Application.targetFrameRate = 60;
            _mpb = new MaterialPropertyBlock();
            var router = FlutterBridge.Instance.Router;
            router.On("spike.ping", env =>
            {
                _pings++;
                // Echo the payload untouched so Flutter can measure the round trip with its own clock.
                router.Send("spike.pong", env.SessionId, new JObject
                {
                    ["echo"] = env.Payload,
                    ["frame"] = Time.frameCount,
                });
            });
            router.On("game.pause", _ => SetPaused(true));
            router.On("game.resume", _ => SetPaused(false));
            swipes.Swiped += OnSwipe;
        }

        void OnSwipe(SwipeDirection d)
        {
            _swipeCount++;
            _lastSwipe = d.ToString();
            _mpb.SetColor("_BaseColor", SwipeColors[(int)d % SwipeColors.Length]);
            cubeRenderer.SetPropertyBlock(_mpb);
            FlutterBridge.Instance.Router.Send("spike.swipe", null, new JObject { ["dir"] = d.ToString().ToLowerInvariant() });
            GameLog.Write(GameLog.Game, "swipe " + d);
        }

        void SetPaused(bool paused)
        {
            _paused = paused;
            Time.timeScale = paused ? 0f : 1f;
            FlutterBridge.Instance.Router.Send(paused ? "spike.paused" : "spike.resumed");
        }

        void Update()
        {
            cube.Rotate(new Vector3(30f, 45f, 15f) * Time.deltaTime, Space.World);
        }

        void OnApplicationPause(bool paused) =>
            GameLog.Write(GameLog.Game, "OnApplicationPause " + paused);

        GUIStyle _style;

        void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(Screen.height / 40f) };
                _style.normal.textColor = new Color32(0xF4, 0xF6, 0xFF, 0xFF);
            }
            var embedded = FlutterBridge.Instance != null && FlutterBridge.Instance.IsEmbedded;
            GUI.Label(new Rect(20, Screen.height * 0.70f, Screen.width - 40, Screen.height * 0.3f),
                $"UNITY SPIKE  ({(embedded ? "embedded" : "standalone")})\n" +
                $"swipes: {_swipeCount}  last: {_lastSwipe}\npings: {_pings}\npaused: {_paused}  frame: {Time.frameCount}",
                _style);
        }
    }
}
