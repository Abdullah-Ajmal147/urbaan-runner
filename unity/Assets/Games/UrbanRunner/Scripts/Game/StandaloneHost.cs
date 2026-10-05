using System;
using GameFactory.Core;
using GameFactory.Core.Autoplay;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Plays Flutter's part when Unity runs on its own (Editor, standalone emulator APK): starts sessions,
    /// grants revive ads, and loops. In builds it autoplays; in the Editor you play with the arrow keys
    /// (tick <see cref="autoplayInEditor"/> to watch the bot). Inactive inside the Flutter app.
    /// </summary>
    public sealed class StandaloneHost : MonoBehaviour
    {
        public bool autoplayInEditor;
        [Range(0, 1)] public float skill = 0.97f;
        [Tooltip("Each autoplay session ends (cause=test) after this many seconds.")]
        public float sessionSeconds = 120f;
        public float delayBetweenSessions = 2f;

        StandaloneHostTransport _transport;
        float _nextStartAt = 1f;
        bool _inSession;
        int _sessions;
        string _pendingAdSession;
        float _adGrantAt;

        void Start()
        {
            _transport = FlutterBridge.Instance.Transport as StandaloneHostTransport;
            if (_transport == null || FlutterBridge.TransportOverride != null)
            {
                enabled = false; // embedded in Flutter, or a test drives the bridge itself
                return;
            }
            _transport.HostReceived += OnUnityMessage;
            GameLog.Write(GameLog.Game, "standalone host active");
        }

        bool Autoplay => !Application.isEditor || autoplayInEditor;

        void Update()
        {
            if (_pendingAdSession != null && Time.unscaledTime >= _adGrantAt)
            {
                Send("ad.result", _pendingAdSession, new JObject { ["placement"] = "revive", ["granted"] = true });
                _pendingAdSession = null;
            }
            if (!_inSession && Time.unscaledTime >= _nextStartAt) StartSession();
        }

        void StartSession()
        {
            _inSession = true;
            _sessions++;
            var test = new TestMode
            {
                Autoplay = Autoplay,
                Skill = skill,
                EndAfterSeconds = Autoplay ? sessionSeconds : 0,
                // Every third autoplay session crashes on purpose to exercise the revive flow.
                ForceCrashAtSeconds = Autoplay && _sessions % 3 == 0 ? 30 : 0,
            };
            Send("session.start", Guid.NewGuid().ToString(), new JObject
            {
                ["seed"] = Autoplay ? _sessions : Environment.TickCount & 0x7fffffff,
                ["mode"] = "normal",
                ["character"] = "mira",
                ["trail"] = "none",
                ["upgrades"] = new JObject { ["magnet"] = 0, ["shield"] = 0, ["double"] = 0 },
                ["config"] = new JObject
                {
                    ["start_speed"] = 8, ["max_speed"] = 22, ["speed_ramp_per_min"] = 3,
                    ["revives_per_run"] = 1, ["revive_enabled"] = true,
                },
                ["settings"] = new JObject { ["music"] = true, ["sfx"] = true, ["haptics"] = true, ["reduceMotion"] = false },
                ["testMode"] = test.ToJson(),
            });
        }

        void OnUnityMessage(string json)
        {
            var msg = BridgeEnvelope.Parse(json);
            switch (msg.Type)
            {
                case "ad.request":
                    _pendingAdSession = msg.SessionId; // a fake rewarded ad that always completes after 1 s
                    _adGrantAt = Time.unscaledTime + 1f;
                    break;
                case "session.ended":
                    _inSession = false;
                    _nextStartAt = Time.unscaledTime + delayBetweenSessions;
                    break;
            }
        }

        void Send(string type, string sid, JObject payload) =>
            _transport.HostSend(new BridgeEnvelope(type, sid, payload).ToJson());
    }
}
