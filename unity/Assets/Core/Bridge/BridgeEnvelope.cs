using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GameFactory.Core.Bridge
{
    /// <summary>
    /// One bridge message: { "v": 1, "type": "...", "sessionId": "...", "payload": { } }.
    /// See docs/bridge-contract.md. All numbers use the invariant culture.
    /// </summary>
    public sealed class BridgeEnvelope
    {
        public const int CurrentVersion = 1;

        public int V { get; }
        public string Type { get; }
        public string SessionId { get; }
        public JObject Payload { get; }

        public BridgeEnvelope(string type, string sessionId = null, JObject payload = null, int v = CurrentVersion)
        {
            if (string.IsNullOrEmpty(type)) throw new ArgumentException("type is required", nameof(type));
            V = v;
            Type = type;
            SessionId = sessionId;
            Payload = payload ?? new JObject();
        }

        static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            Culture = CultureInfo.InvariantCulture,
            FloatFormatHandling = FloatFormatHandling.DefaultValue,
            Formatting = Formatting.None,
        };

        public string ToJson()
        {
            var o = new JObject
            {
                ["v"] = V,
                ["type"] = Type,
            };
            if (SessionId != null) o["sessionId"] = SessionId;
            o["payload"] = Payload;
            return JsonConvert.SerializeObject(o, Settings);
        }

        /// <summary>Parses a message. Throws <see cref="BridgeParseException"/> on malformed input.</summary>
        public static BridgeEnvelope Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new BridgeParseException("empty message");
            JObject o;
            try
            {
                using (var reader = new JsonTextReader(new System.IO.StringReader(json)) { Culture = CultureInfo.InvariantCulture, DateParseHandling = DateParseHandling.None })
                    o = JObject.Load(reader);
            }
            catch (JsonException e)
            {
                throw new BridgeParseException("invalid json: " + e.Message);
            }

            var vToken = o["v"];
            if (vToken == null || vToken.Type != JTokenType.Integer) throw new BridgeParseException("missing or non-integer 'v'");
            var typeToken = o["type"];
            if (typeToken == null || typeToken.Type != JTokenType.String || string.IsNullOrEmpty((string)typeToken))
                throw new BridgeParseException("missing 'type'");
            var sid = o["sessionId"];
            var payload = o["payload"];
            if (payload != null && payload.Type != JTokenType.Object && payload.Type != JTokenType.Null)
                throw new BridgeParseException("'payload' must be an object");

            return new BridgeEnvelope(
                (string)typeToken,
                sid == null || sid.Type == JTokenType.Null ? null : (string)sid,
                payload as JObject,
                (int)vToken);
        }
    }

    public sealed class BridgeParseException : Exception
    {
        public BridgeParseException(string message) : base(message) { }
    }
}
