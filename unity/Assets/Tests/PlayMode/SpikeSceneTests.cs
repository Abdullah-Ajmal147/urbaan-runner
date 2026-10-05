using System.Collections;
using System.Linq;
using GameFactory.Core.Bridge;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Tests.PlayMode
{
    public class SpikeSceneTests
    {
        StandaloneHostTransport _host;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _host = new StandaloneHostTransport();
            FlutterBridge.TransportOverride = _host;
            yield return SceneManager.LoadSceneAsync("Spike", LoadSceneMode.Single);
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            FlutterBridge.TransportOverride = null;
            Time.timeScale = 1f;
            if (FlutterBridge.Instance != null) Object.Destroy(FlutterBridge.Instance.gameObject);
            yield return null;
        }

        JObject[] Sent(string type) =>
            _host.Sent.Select(JObject.Parse).Where(m => (string)m["type"] == type).ToArray();

        [Test]
        public void SendsUnityReadyOnLoad()
        {
            var ready = Sent("unity.ready");
            Assert.AreEqual(1, ready.Length);
            Assert.AreEqual(1, (int)ready[0]["payload"]["bridgeVersion"]);
        }

        [UnityTest]
        public IEnumerator PingIsAnsweredWithPong()
        {
            _host.HostSend("{\"v\":1,\"type\":\"spike.ping\",\"payload\":{\"t\":123}}");
            yield return null;
            var pong = Sent("spike.pong");
            Assert.AreEqual(1, pong.Length);
            Assert.AreEqual(123, (int)pong[0]["payload"]["echo"]["t"]);
        }

        [UnityTest]
        public IEnumerator PauseAndResumeControlTimeScale()
        {
            _host.HostSend("{\"v\":1,\"type\":\"game.pause\"}");
            yield return null;
            Assert.AreEqual(0f, Time.timeScale);
            _host.HostSend("{\"v\":1,\"type\":\"game.resume\"}");
            yield return null;
            Assert.AreEqual(1f, Time.timeScale);
        }
    }
}
