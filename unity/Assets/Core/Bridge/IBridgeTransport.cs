using System;

namespace GameFactory.Core.Bridge
{
    /// <summary>
    /// Moves raw JSON strings between Unity and its host. The Flutter embed and the standalone fake host
    /// both implement this, so gameplay code never knows which one it is talking to.
    /// </summary>
    public interface IBridgeTransport
    {
        /// <summary>Send a message to the host (Flutter, or a fake).</summary>
        void Send(string json);

        /// <summary>Raised when the host sends a message to Unity.</summary>
        event Action<string> MessageReceived;

        /// <summary>Called by <see cref="FlutterBridge"/> when the host delivers a message.</summary>
        void Deliver(string json);
    }
}
