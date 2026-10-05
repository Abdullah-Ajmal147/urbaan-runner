using System;
using System.Collections.Generic;

namespace GameFactory.Core.Bridge
{
    /// <summary>Real transport inside the Flutter app (flutter_embed_unity).</summary>
    public sealed class FlutterEmbedTransport : IBridgeTransport
    {
        public event Action<string> MessageReceived;

        public void Send(string json) => SendToFlutter.Send(json);

        public void Deliver(string json) => MessageReceived?.Invoke(json);
    }

    /// <summary>
    /// Fake host for standalone Unity builds and tests. Records everything Unity sends and lets a
    /// test or a <c>StandaloneHost</c> script reply as if it were Flutter.
    /// </summary>
    public sealed class StandaloneHostTransport : IBridgeTransport
    {
        public event Action<string> MessageReceived;

        /// <summary>Raised for every message Unity sends to the (fake) host.</summary>
        public event Action<string> HostReceived;

        public List<string> Sent { get; } = new List<string>();

        public void Send(string json)
        {
            Sent.Add(json);
            HostReceived?.Invoke(json);
        }

        public void Deliver(string json) => MessageReceived?.Invoke(json);

        /// <summary>Pretend Flutter sent this message.</summary>
        public void HostSend(string json) => Deliver(json);
    }
}
