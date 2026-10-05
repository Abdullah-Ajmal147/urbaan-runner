using System.Collections;
using System.Linq;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UrbanRunner.Game;

namespace Tests.PlayMode
{
    /// <summary>
    /// M1 exit tests on the real Run scene, with the test playing Flutter through a StandaloneHostTransport.
    /// Any logged error or exception fails the test (Unity Test Framework default).
    /// </summary>
    public class RunSceneTests
    {
        StandaloneHostTransport _host;
        GameRoot _root;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _host = new StandaloneHostTransport();
            FlutterBridge.TransportOverride = _host;
            yield return SceneManager.LoadSceneAsync("Run", LoadSceneMode.Single);
            yield return null;
            yield return null;
            _root = Object.FindAnyObjectByType<GameRoot>();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            FlutterBridge.TransportOverride = null;
            if (FlutterBridge.Instance != null) Object.Destroy(FlutterBridge.Instance.gameObject);
            yield return null;
        }

        JObject[] Sent(string type) =>
            _host.Sent.Select(JObject.Parse).Where(m => (string)m["type"] == type).ToArray();

        void Send(string type, string sid = null, JObject payload = null) =>
            _host.HostSend(new BridgeEnvelope(type, sid, payload).ToJson());

        static JObject Start(JObject testMode, bool reviveEnabled = true) => new JObject
        {
            ["seed"] = 2026,
            ["mode"] = "normal",
            ["character"] = "mira",
            ["trail"] = "none",
            ["upgrades"] = new JObject { ["magnet"] = 0, ["shield"] = 0, ["double"] = 0 },
            ["config"] = new JObject
            {
                ["start_speed"] = 8, ["max_speed"] = 22, ["speed_ramp_per_min"] = 3,
                ["revives_per_run"] = 1, ["revive_enabled"] = reviveEnabled,
            },
            ["settings"] = new JObject { ["music"] = true, ["sfx"] = true, ["haptics"] = true, ["reduceMotion"] = false },
            ["testMode"] = testMode,
        };

        IEnumerator WaitFor(System.Func<bool> condition, float realSeconds)
        {
            var until = Time.realtimeSinceStartup + realSeconds;
            while (!condition() && Time.realtimeSinceStartup < until) yield return null;
        }

        [Test]
        public void SceneLoad_SendsUnityReady_AndAnswersDebugPing()
        {
            var ready = Sent("unity.ready").Single();
            Assert.AreEqual(BridgeEnvelope.CurrentVersion, (int)ready["payload"]["bridgeVersion"]);
            Send("debug.ping", null, new JObject { ["id"] = 9 });
            Assert.AreEqual(9, (int)Sent("debug.pong").Single()["payload"]["echo"]["id"]);
        }

        [UnityTest]
        public IEnumerator Autoplay_Runs3Minutes_WithoutErrors()
        {
            Time.timeScale = 4f; // 180 game-seconds in ~45 s real time
            Send("session.start", "auto-1", Start(new JObject { ["autoplay"] = true, ["skill"] = 1.0, ["endAfterSeconds"] = 180 }));
            Assert.AreEqual(1, Sent("session.started").Length);
            yield return WaitFor(() => Sent("session.ended").Length > 0, 240);

            var ended = Sent("session.ended");
            Assert.AreEqual(1, ended.Length, "session did not end in time");
            var p = ended[0]["payload"];
            Assert.AreEqual("test", (string)p["cause"], "the perfect bot crashed");
            Assert.AreEqual(180.0, (double)p["durationS"], 0.5);
            Assert.Greater((double)p["distanceM"], 1800);
            Assert.Greater((int)p["sparks"], 50);
            Assert.AreEqual(0, _root.Track.PoolGrowth, "pools grew during the run (Instantiate mid-run)");
        }

        [UnityTest]
        public IEnumerator ForcedCrash_ProducesCorrectSessionEnded()
        {
            Send("session.start", "crash-1", Start(new JObject { ["autoplay"] = true, ["forceCrashAtSeconds"] = 4 }, reviveEnabled: false));
            yield return WaitFor(() => Sent("session.ended").Length > 0, 20);

            var ended = Sent("session.ended").Single();
            Assert.AreEqual("crash-1", (string)ended["sessionId"]);
            Assert.AreEqual(1, (int)ended["v"]);
            var p = (JObject)ended["payload"];
            Assert.AreEqual("obstacle", (string)p["cause"]);
            Assert.AreEqual("normal", (string)p["mode"]);
            Assert.AreEqual(2026, (long)p["seed"]);
            Assert.AreEqual(4.0, (double)p["durationS"], 0.1);
            Assert.Greater((double)p["distanceM"], 25);
            Assert.AreEqual(0, (int)p["revivesUsed"]);
            Assert.AreEqual(Application.version, (string)p["buildVersion"]);
            Assert.IsNotNull(p["stats"]["laneChanges"]);
            Assert.IsEmpty(Sent("ad.request"), "revive disabled: no ad must be requested");
        }

        [UnityTest]
        public IEnumerator Revive_ResumesTheRun()
        {
            Send("session.start", "rev-1", Start(new JObject { ["autoplay"] = true, ["forceCrashAtSeconds"] = 3, ["endAfterSeconds"] = 12 }));
            yield return WaitFor(() => Sent("ad.request").Length > 0, 15);
            var req = Sent("ad.request").Single();
            Assert.AreEqual("revive", (string)req["payload"]["placement"]);
            Assert.AreEqual(SessionPhase.AwaitingAd, _root.Session.Phase);

            var distance = _root.Session.Sim.Distance;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(distance, _root.Session.Sim.Distance, "run must stay frozen while the ad shows");

            Send("ad.result", "rev-1", new JObject { ["placement"] = "revive", ["granted"] = true });
            Assert.AreEqual(SessionPhase.Running, _root.Session.Phase);
            yield return new WaitForSeconds(1f);
            Assert.Greater(_root.Session.Sim.Distance, distance + 5, "run did not resume");

            yield return WaitFor(() => Sent("session.ended").Length > 0, 20);
            var p = Sent("session.ended").Single()["payload"];
            Assert.AreEqual("test", (string)p["cause"]);
            Assert.AreEqual(1, (int)p["revivesUsed"]);
        }

        [UnityTest]
        public IEnumerator PauseFromFlutter_FreezesAndResumes()
        {
            Send("session.start", "p-1", Start(new JObject { ["autoplay"] = true }));
            yield return new WaitForSeconds(1f);
            Send("game.pause");
            var d = _root.Session.Sim.Distance;
            yield return new WaitForSeconds(0.5f);
            Assert.AreEqual(d, _root.Session.Sim.Distance);
            Send("game.resume");
            yield return new WaitForSeconds(0.5f);
            Assert.Greater(_root.Session.Sim.Distance, d);
            _root.Session.Quit();
            Assert.AreEqual("quit", (string)Sent("session.ended").Single()["payload"]["cause"]);
        }
    }
}
