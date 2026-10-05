using System.Linq;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UrbanRunner.Game;

namespace Tests.EditMode
{
    /// <summary>The Unity side of the bridge contract for a whole session (HANDOVER §6, §3.8).</summary>
    public class SessionTests
    {
        const float Dt = 1f / 60f;
        StandaloneHostTransport _host;
        RunSession _session;
        double _now;

        [SetUp]
        public void SetUp()
        {
            var templates = GeneratorTests.Templates ?? (GeneratorTests.Templates = UrbanRunner.Editor.ChunkCatalog.EnsureAssets().chunks);
            _host = new StandaloneHostTransport();
            var router = new BridgeRouter(_host) { Log = (_, __) => { } };
            _session = new RunSession(router, templates, "0.1.0-test");
            _now = 0;
        }

        void Send(string type, string sid = null, JObject payload = null) =>
            _host.HostSend(new BridgeEnvelope(type, sid, payload).ToJson());

        void Tick(float seconds)
        {
            for (var t = 0f; t < seconds; t += Dt)
            {
                _now += Dt;
                _session.Tick(Dt, _now);
            }
        }

        JObject[] Sent(string type) => _host.Sent.Select(JObject.Parse).Where(m => (string)m["type"] == type).ToArray();

        static JObject StartPayload(JObject testMode = null, int revives = 1) => new JObject
        {
            ["seed"] = 1234,
            ["mode"] = "normal",
            ["character"] = "mira",
            ["trail"] = "none",
            ["upgrades"] = new JObject { ["magnet"] = 1, ["shield"] = 0, ["double"] = 2 },
            ["config"] = new JObject
            {
                ["start_speed"] = 8, ["max_speed"] = 22, ["speed_ramp_per_min"] = 3,
                ["revives_per_run"] = revives, ["revive_enabled"] = true,
            },
            ["settings"] = new JObject { ["music"] = true, ["sfx"] = true, ["haptics"] = false, ["reduceMotion"] = true },
            ["testMode"] = testMode,
        };

        [Test]
        public void Start_SendsSessionStarted_AndAppliesPayload()
        {
            Send("session.start", "s1", StartPayload());
            Assert.AreEqual("s1", (string)Sent("session.started").Single()["sessionId"]);
            Assert.AreEqual(SessionPhase.Running, _session.Phase);
            Assert.AreEqual(1234UL, _session.Start.Seed);
            Assert.AreEqual(2, _session.Start.Upgrades.Double);
            Assert.IsFalse(_session.Settings.Haptics);
            Assert.IsTrue(_session.Settings.ReduceMotion);
        }

        [Test]
        public void StartWithoutSessionId_IsAnError()
        {
            Send("session.start", null, StartPayload());
            Assert.AreEqual("bad_session", (string)Sent("error").Single()["payload"]["code"]);
            Assert.AreEqual(SessionPhase.Idle, _session.Phase);
        }

        [Test]
        public void SecondStartWhileRunning_IsRejected()
        {
            Send("session.start", "s1", StartPayload());
            Send("session.start", "s2", StartPayload());
            Assert.AreEqual("session_active", (string)Sent("error").Single()["payload"]["code"]);
            Assert.AreEqual("s1", _session.Start.SessionId);
        }

        [Test]
        public void EndAfterSeconds_EndsWithCauseTest_AndFullPayload()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = true, ["skill"] = 1, ["endAfterSeconds"] = 20 }));
            Tick(21);
            var ended = Sent("session.ended").Single();
            var p = (JObject)ended["payload"];
            Assert.AreEqual("s1", (string)ended["sessionId"]);
            Assert.AreEqual("test", (string)p["cause"]);
            Assert.AreEqual("normal", (string)p["mode"]);
            Assert.AreEqual(1234, (long)p["seed"]);
            Assert.Greater((double)p["distanceM"], 150);
            Assert.AreEqual(20.0, (double)p["durationS"], 0.05);
            Assert.AreEqual(0, (int)p["revivesUsed"]);
            Assert.AreEqual("0.1.0-test", (string)p["buildVersion"]);
            foreach (var k in new[] { "jumps", "slides", "laneChanges", "magnet", "shield", "doubleSparks" })
                Assert.IsNotNull(p["stats"][k], k);
            Assert.AreEqual(SessionPhase.Ended, _session.Phase);
        }

        [Test]
        public void ForcedCrash_OffersRevive_DeclineEndsWithObstacle()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = false, ["forceCrashAtSeconds"] = 3 }));
            Tick(3.1f);
            Assert.AreEqual(SessionPhase.RevivePrompt, _session.Phase);
            Assert.AreEqual("revive_offer", (string)Sent("analytics.event").Single()["payload"]["name"]);
            Tick(RunSession.RevivePromptSeconds + 0.1f); // countdown runs out
            var events = Sent("analytics.event").Select(m => (string)m["payload"]["name"]).ToArray();
            CollectionAssert.AreEqual(new[] { "revive_offer", "revive_decline" }, events);
            var p = Sent("session.ended").Single()["payload"];
            Assert.AreEqual("obstacle", (string)p["cause"]);
            Assert.AreEqual(3.0, (double)p["durationS"], 0.05);
        }

        [Test]
        public void Revive_Granted_ResumesTheRun()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = true, ["forceCrashAtSeconds"] = 2, ["endAfterSeconds"] = 10 }));
            Tick(2.1f);
            Assert.AreEqual(SessionPhase.RevivePrompt, _session.Phase);
            Tick(0.6f); // autoplay accepts after 0.5 s
            Assert.AreEqual(SessionPhase.AwaitingAd, _session.Phase);
            var req = Sent("ad.request").Single();
            Assert.AreEqual("revive", (string)req["payload"]["placement"]);
            Assert.AreEqual("s1", (string)req["sessionId"]);

            Send("ad.result", "s1", new JObject { ["placement"] = "revive", ["granted"] = true });
            Assert.AreEqual(SessionPhase.Running, _session.Phase);
            Tick(9);
            var p = Sent("session.ended").Single()["payload"];
            Assert.AreEqual("test", (string)p["cause"]);
            Assert.AreEqual(1, (int)p["revivesUsed"]);
            var names = Sent("analytics.event").Select(m => (string)m["payload"]["name"]);
            CollectionAssert.AreEqual(new[] { "revive_offer", "revive_accept" }, names);
        }

        [Test]
        public void Revive_NotGranted_Ends()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["forceCrashAtSeconds"] = 1 }));
            Tick(1.1f);
            _session.AcceptRevive();
            Send("ad.result", "s1", new JObject { ["placement"] = "revive", ["granted"] = false });
            Assert.AreEqual("obstacle", (string)Sent("session.ended").Single()["payload"]["cause"]);
        }

        [Test]
        public void AdResult_TimesOutAfter60s()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["forceCrashAtSeconds"] = 1 }));
            Tick(1.1f);
            _session.AcceptRevive();
            Tick(59);
            Assert.AreEqual(SessionPhase.AwaitingAd, _session.Phase);
            Tick(1.1f);
            Assert.AreEqual(SessionPhase.Ended, _session.Phase);
            Assert.AreEqual("obstacle", (string)Sent("session.ended").Single()["payload"]["cause"]);
        }

        [Test]
        public void DuplicateAdResult_IsIgnored()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["forceCrashAtSeconds"] = 1 }, revives: 2));
            Tick(1.1f);
            _session.AcceptRevive();
            var granted = new JObject { ["placement"] = "revive", ["granted"] = true };
            Send("ad.result", "s1", granted);
            Send("ad.result", "s1", granted);
            Assert.AreEqual(1, _session.Sim.RevivesUsed);
            Assert.AreEqual(SessionPhase.Running, _session.Phase);
        }

        [Test]
        public void SecondCrash_AfterLastRevive_EndsDirectly()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["forceCrashAtSeconds"] = 1 }));
            Tick(1.1f);
            _session.AcceptRevive();
            Send("ad.result", "s1", new JObject { ["placement"] = "revive", ["granted"] = true });
            Tick(120); // no input: will crash again once invulnerability ends
            Assert.AreEqual(SessionPhase.Ended, _session.Phase);
            Assert.AreEqual(1, Sent("analytics.event").Count(m => (string)m["payload"]["name"] == "revive_offer"));
        }

        [Test]
        public void Quit_EndsWithCauseQuit()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = true }));
            Tick(2);
            _session.Quit();
            Assert.AreEqual("quit", (string)Sent("session.ended").Single()["payload"]["cause"]);
        }

        [Test]
        public void PauseFreezesTheRun()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = true }));
            Tick(1);
            var d = _session.Sim.Distance;
            Send("game.pause");
            Tick(2);
            Assert.AreEqual(d, _session.Sim.Distance);
            Send("game.resume");
            Tick(1);
            Assert.Greater(_session.Sim.Distance, d);
        }

        [Test]
        public void SettingsUpdate_IsApplied()
        {
            RunSettings? seen = null;
            _session.SettingsChanged += s => seen = s;
            Send("settings.update", null, new JObject { ["music"] = false, ["sfx"] = true, ["haptics"] = true, ["reduceMotion"] = false });
            Assert.IsFalse(_session.Settings.Music);
            Assert.IsTrue(seen.HasValue);
        }

        [Test]
        public void NewSessionAfterEnd_Works()
        {
            Send("session.start", "s1", StartPayload(new JObject { ["autoplay"] = true, ["endAfterSeconds"] = 1 }));
            Tick(1.1f);
            Send("session.start", "s2", StartPayload(new JObject { ["autoplay"] = true, ["endAfterSeconds"] = 1 }));
            Tick(1.1f);
            CollectionAssert.AreEqual(new[] { "s1", "s2" }, Sent("session.ended").Select(m => (string)m["sessionId"]));
        }

        [Test]
        public void DebugPing_IsAnswered()
        {
            Send("debug.ping", null, new JObject { ["id"] = 5 });
            Assert.AreEqual(5, (int)Sent("debug.pong").Single()["payload"]["echo"]["id"]);
        }
    }
}
