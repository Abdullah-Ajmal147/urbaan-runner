using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace GameFactory.Core.Bridge
{
    /// <summary>
    /// Pure-C# message router (no MonoBehaviour, so it is EditMode-testable).
    /// Parses envelopes, checks the version, dispatches by type, and logs-and-ignores unknown types.
    /// </summary>
    public sealed class BridgeRouter
    {
        readonly IBridgeTransport _transport;
        readonly Dictionary<string, Action<BridgeEnvelope>> _handlers = new Dictionary<string, Action<BridgeEnvelope>>();

        public int BridgeVersion { get; }

        /// <summary>Log sink; defaults to <see cref="GameLog"/>.</summary>
        public Action<string, string> Log { get; set; } = GameLog.Write;

        public BridgeRouter(IBridgeTransport transport, int bridgeVersion = BridgeEnvelope.CurrentVersion)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            BridgeVersion = bridgeVersion;
            _transport.MessageReceived += OnMessage;
        }

        public void On(string type, Action<BridgeEnvelope> handler) => _handlers[type] = handler;

        public void Off(string type) => _handlers.Remove(type);

        public void Send(string type, string sessionId = null, JObject payload = null)
        {
            var json = new BridgeEnvelope(type, sessionId, payload, BridgeVersion).ToJson();
            Log(GameLog.Bridge, "-> " + json);
            _transport.Send(json);
        }

        public void SendError(string code, string message, string sessionId = null)
        {
            Log(GameLog.Error, code + ": " + message);
            Send("error", sessionId, new JObject { ["code"] = code, ["message"] = message });
        }

        void OnMessage(string json)
        {
            Log(GameLog.Bridge, "<- " + json);
            BridgeEnvelope env;
            try
            {
                env = BridgeEnvelope.Parse(json);
            }
            catch (BridgeParseException e)
            {
                SendError("bad_message", e.Message);
                return;
            }

            if (env.V != BridgeVersion)
            {
                SendError("version_mismatch", $"expected v{BridgeVersion}, got v{env.V}", env.SessionId);
                return;
            }

            if (!_handlers.TryGetValue(env.Type, out var handler))
            {
                Log(GameLog.Bridge, "ignoring unknown message type '" + env.Type + "'");
                return;
            }

            try
            {
                handler(env);
            }
            catch (Exception e)
            {
                SendError("handler_failed", env.Type + ": " + e.Message, env.SessionId);
            }
        }
    }
}
