using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GameFactory.Core.Bridge
{
    /// <summary>
    /// The scene object Flutter talks to: sendToUnity("FlutterBridge", "OnFlutterMessage", json).
    /// Owns the transport and the router and announces <c>unity.ready</c> once the scene has loaded.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class FlutterBridge : MonoBehaviour
    {
        public const string ObjectName = "FlutterBridge";
        public const string FlutterClass = "com.learntoflutter.flutter_embed_unity_android.messaging.SendToFlutter";

        public static FlutterBridge Instance { get; private set; }

        /// <summary>Set before the scene loads to force a transport (tests, standalone host).</summary>
        public static IBridgeTransport TransportOverride { get; set; }

        public IBridgeTransport Transport { get; private set; }
        public BridgeRouter Router { get; private set; }
        public bool IsEmbedded { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            gameObject.name = ObjectName; // Flutter addresses this object by name
            DontDestroyOnLoad(gameObject);

            Transport = TransportOverride ?? DetectTransport();
            IsEmbedded = Transport is FlutterEmbedTransport;
            Router = new BridgeRouter(Transport);
            GameLog.Write(GameLog.Bridge, "transport=" + Transport.GetType().Name);
        }

        void Start()
        {
            Router.Send("unity.ready", null, new JObject
            {
                ["bridgeVersion"] = BridgeEnvelope.CurrentVersion,
                ["buildVersion"] = Application.version,
            });
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Entry point called by the Flutter plugin.</summary>
        public void OnFlutterMessage(string json) => Transport.Deliver(json);

        static IBridgeTransport DetectTransport()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // The plugin's Java class only exists when Unity runs inside the Flutter app.
            try
            {
                using (new AndroidJavaClass(FlutterClass)) { }
                return new FlutterEmbedTransport();
            }
            catch (AndroidJavaException)
            {
                return new StandaloneHostTransport();
            }
#else
            return new StandaloneHostTransport();
#endif
        }
    }
}
