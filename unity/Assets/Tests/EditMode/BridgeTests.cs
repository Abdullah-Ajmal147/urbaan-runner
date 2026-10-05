using System.Globalization;
using System.Threading;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Tests.EditMode
{
    public class BridgeTests
    {
        StandaloneHostTransport _host;
        BridgeRouter _router;

        [SetUp]
        public void SetUp()
        {
            _host = new StandaloneHostTransport();
            _router = new BridgeRouter(_host) { Log = (_, __) => { } };
        }

        static JObject Last(StandaloneHostTransport t) => JObject.Parse(t.Sent[t.Sent.Count - 1]);

        [Test]
        public void Envelope_RoundTrips()
        {
            var env = new BridgeEnvelope("session.start", "abc", new JObject { ["seed"] = 42, ["mode"] = "daily" });
            var back = BridgeEnvelope.Parse(env.ToJson());
            Assert.AreEqual(1, back.V);
            Assert.AreEqual("session.start", back.Type);
            Assert.AreEqual("abc", back.SessionId);
            Assert.AreEqual(42, (int)back.Payload["seed"]);
            Assert.AreEqual("daily", (string)back.Payload["mode"]);
        }

        [Test]
        public void Envelope_MissingPayload_IsEmptyObject()
        {
            var env = BridgeEnvelope.Parse("{\"v\":1,\"type\":\"game.pause\"}");
            Assert.IsNotNull(env.Payload);
            Assert.AreEqual(0, env.Payload.Count);
            Assert.IsNull(env.SessionId);
        }

        [TestCase("")]
        [TestCase("not json")]
        [TestCase("{\"type\":\"x\"}")]
        [TestCase("{\"v\":\"1\",\"type\":\"x\"}")]
        [TestCase("{\"v\":1}")]
        [TestCase("{\"v\":1,\"type\":\"x\",\"payload\":[1]}")]
        public void Envelope_Malformed_Throws(string json)
        {
            Assert.Throws<BridgeParseException>(() => BridgeEnvelope.Parse(json));
        }

        [Test]
        public void Numbers_UseInvariantCulture_EvenUnderCommaLocale()
        {
            var old = Thread.CurrentThread.CurrentCulture;
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
                var json = new BridgeEnvelope("x", null, new JObject { ["d"] = 1.5 }).ToJson();
                StringAssert.Contains("1.5", json);
                Assert.AreEqual(1.5, (double)BridgeEnvelope.Parse(json).Payload["d"], 1e-9);
            }
            finally
            {
                Thread.CurrentThread.CurrentCulture = old;
            }
        }

        [Test]
        public void Router_DispatchesByType()
        {
            BridgeEnvelope got = null;
            _router.On("spike.ping", e => got = e);
            _host.HostSend("{\"v\":1,\"type\":\"spike.ping\",\"sessionId\":\"s1\",\"payload\":{\"t\":7}}");
            Assert.IsNotNull(got);
            Assert.AreEqual("s1", got.SessionId);
            Assert.AreEqual(7, (int)got.Payload["t"]);
        }

        [Test]
        public void Router_UnknownType_IsIgnoredWithoutReply()
        {
            _host.HostSend("{\"v\":1,\"type\":\"no.such.type\"}");
            Assert.AreEqual(0, _host.Sent.Count);
        }

        [Test]
        public void Router_VersionMismatch_SendsError()
        {
            var called = false;
            _router.On("game.pause", _ => called = true);
            _host.HostSend("{\"v\":2,\"type\":\"game.pause\"}");
            Assert.IsFalse(called);
            var err = Last(_host);
            Assert.AreEqual("error", (string)err["type"]);
            Assert.AreEqual("version_mismatch", (string)err["payload"]["code"]);
        }

        [Test]
        public void Router_BadJson_SendsError()
        {
            _host.HostSend("{oops");
            Assert.AreEqual("bad_message", (string)Last(_host)["payload"]["code"]);
        }

        [Test]
        public void Router_HandlerException_BecomesErrorMessage()
        {
            _router.On("boom", _ => throw new System.InvalidOperationException("kaput"));
            _host.HostSend("{\"v\":1,\"type\":\"boom\",\"sessionId\":\"s9\"}");
            var err = Last(_host);
            Assert.AreEqual("handler_failed", (string)err["payload"]["code"]);
            Assert.AreEqual("s9", (string)err["sessionId"]);
        }

        [Test]
        public void Router_Send_WritesEnvelope()
        {
            _router.Send("unity.ready", null, new JObject { ["bridgeVersion"] = 1, ["buildVersion"] = "0.1.0" });
            var msg = Last(_host);
            Assert.AreEqual(1, (int)msg["v"]);
            Assert.AreEqual("unity.ready", (string)msg["type"]);
            Assert.IsNull(msg["sessionId"]);
            Assert.AreEqual("0.1.0", (string)msg["payload"]["buildVersion"]);
        }
    }
}
