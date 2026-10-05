using GameFactory.Core;
using GameFactory.Core.Bridge;
using GameFactory.Core.Input;
using GameFactory.Core.Perf;
using GameFactory.Core.World;
using UnityEngine;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Scene entry point: wires the bridge, the run session, the views and input.
    /// Everything visual is built (and pooled) here at load, never during a run.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] GameAssets assets;
        [SerializeField] ChunkLibrary chunks;
        [SerializeField] Camera mainCamera;

        public RunSession Session { get; private set; }
        public TrackView Track { get; private set; }
        public PlayerView Player { get; private set; }
        public Hud Hud { get; private set; }
        public PerfReporter Perf { get; private set; }

        readonly FloatingOrigin _origin = new FloatingOrigin(500.0);
        Transform _world;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;

            var bridge = FlutterBridge.Instance;
            Session = new RunSession(bridge.Router, chunks.chunks, Application.version);
            Session.Started += OnStarted;
            Session.Ended += _ => Perf.EndSession();
            Session.SettingsChanged += s => Player.ReduceMotion = s.ReduceMotion;

            _world = new GameObject("World").transform;
            Track = new GameObject("Track").AddComponent<TrackView>();
            Track.transform.SetParent(_world, false);
            Track.Init(assets);
            Player = new GameObject("Player").AddComponent<PlayerView>();
            Player.transform.SetParent(_world, false);
            Player.Init(assets, mainCamera);

            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(assets.font);
            Hud.PausePressed += () => Session.SetPaused(true);
            Hud.ResumePressed += () => Session.SetPaused(false);
            Hud.QuitPressed += () => Session.Quit();
            Hud.ReviveYes += () => Session.AcceptRevive();
            Hud.ReviveNo += () => Session.DeclineRevive();

            var swipes = gameObject.AddComponent<PointerSwipeSource>();
            swipes.Swiped += Session.Input;

            Perf = gameObject.AddComponent<PerfReporter>();
        }

        void OnStarted(SessionStart s)
        {
            _origin.Reset();
            Track.ResetSlots();
            Player.SnapCamera(_origin, Session.Sim);
            Perf.BeginSession();
        }

        void Update()
        {
            // Clamp long frames (e.g. after resume) so the sim never jumps far ahead.
            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            Session.Tick(dt, Time.realtimeSinceStartupAsDouble);

            var sim = Session.Sim;
            if (sim != null)
            {
                if (_origin.Update(sim.Distance)) GameLog.Write(GameLog.Game, "floating origin -> " + _origin.Offset);
                if (sim.Events.Crashed) Player.Shake();
                Track.Sync(sim, _origin, Time.time);
                Player.Sync(sim, _origin, dt);
            }
            Hud.Sync(Session);
        }

        void OnApplicationPause(bool paused)
        {
            // Backgrounded mid-run: come back paused (HANDOVER §10.3 #15).
            if (paused && Session.Phase == SessionPhase.Running) Session.SetPaused(true);
        }
    }
}
