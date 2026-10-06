using GameFactory.Core;
using GameFactory.Core.Audio;
using GameFactory.Core.Bridge;
using GameFactory.Core.Haptics;
using GameFactory.Core.Input;
using GameFactory.Core.Perf;
using GameFactory.Core.World;
using UnityEngine;
using UnityEngine.Rendering;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    /// <summary>
    /// Scene entry point: wires the bridge, the run session, the views, audio, haptics and input.
    /// Everything visual is built (and pooled) here at load, never during a run.
    /// </summary>
    public sealed class GameRoot : MonoBehaviour
    {
        [SerializeField] GameAssets assets;
        [SerializeField] ChunkLibrary chunks;
        [SerializeField] Camera mainCamera;
        [SerializeField] Volume postFx;

        public RunSession Session { get; private set; }
        public TrackView Track { get; private set; }
        public EnvironmentView Environment { get; private set; }
        public PlayerView Player { get; private set; }
        public Hud Hud { get; private set; }
        public PerfReporter Perf { get; private set; }
        public AudioPlayer Audio { get; private set; }

        readonly FloatingOrigin _origin = new FloatingOrigin(500.0);
        Transform _world;
        int _lastCountdown = -1;
        SessionPhase _lastPhase;

        /// <summary>Bloom and colour grading cost fill-rate; phones with little memory skip them.</summary>
        public static bool HighQuality => SystemInfo.systemMemorySize >= 3000;

        void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
            if (postFx != null) postFx.enabled = HighQuality;
            GameLog.Write(GameLog.Perf, $"quality={(HighQuality ? "high" : "low")} mem_mb={SystemInfo.systemMemorySize} gpu={SystemInfo.graphicsDeviceName}");

            var bridge = FlutterBridge.Instance;
            Session = new RunSession(bridge.Router, chunks.chunks, Application.version);
            Session.Started += OnStarted;
            Session.Ended += _ => { Perf.EndSession(); Audio.PauseMusic(true); };
            Session.SettingsChanged += ApplySettings;

            _world = new GameObject("World").transform;
            Environment = new GameObject("Environment").AddComponent<EnvironmentView>();
            Environment.transform.SetParent(_world, false);
            Environment.Init(assets);
            Track = new GameObject("Track").AddComponent<TrackView>();
            Track.transform.SetParent(_world, false);
            Track.Init(assets);
            Player = new GameObject("Player").AddComponent<PlayerView>();
            Player.transform.SetParent(_world, false);
            Player.Init(assets, mainCamera);
            Player.Landed += () => Audio.Play(assets.sfxLand, 0.6f);

            Audio = gameObject.AddComponent<AudioPlayer>();

            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(assets);
            Hud.PausePressed += () => { Tap(); Session.SetPaused(true); };
            Hud.ResumePressed += () => { Tap(); Session.SetPaused(false); };
            Hud.QuitPressed += () => { Tap(); Session.Quit(); };
            Hud.ReviveYes += () => { Tap(); Session.AcceptRevive(); };
            Hud.ReviveNo += () => { Tap(); Session.DeclineRevive(); };

            var swipes = gameObject.AddComponent<PointerSwipeSource>();
            swipes.Swiped += Session.Input;

            Perf = gameObject.AddComponent<PerfReporter>();

            // Place the street and camera before the first session so Unity shows Lantern City straight away.
            Environment.Sync(0, _origin);
            Player.SnapIdle();
        }

        void Tap()
        {
            Audio.Play(assets.sfxUiTap, 0.8f);
            Haptics.Play(HapticKind.Light);
        }

        void ApplySettings(RunSettings s)
        {
            Player.ReduceMotion = s.ReduceMotion;
            Haptics.Enabled = s.Haptics;
            Audio.Apply(s.Music, s.Sfx);
        }

        void OnStarted(SessionStart s)
        {
            _origin.Reset();
            Track.ResetSlots();
            Player.SetCharacter(s.Character);
            Player.SnapCamera(_origin, Session.Sim);
            Perf.BeginSession();
            Audio.PlayMusic(assets.music);
            Audio.PauseMusic(false);
            _lastCountdown = -1;
        }

        /// <summary>Managed bytes allocated by the last Update (perf budget: 0 after warm-up; PlayMode-tested).</summary>
        public long LastUpdateAllocBytes { get; private set; }

        void Update()
        {
            var allocStart = System.GC.GetAllocatedBytesForCurrentThread();
            RunFrame();
            LastUpdateAllocBytes = System.GC.GetAllocatedBytesForCurrentThread() - allocStart;
        }

        void RunFrame()
        {
            // Clamp long frames (e.g. after resume) so the sim never jumps far ahead.
            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            var wasPaused = Session.Paused;
            Session.Tick(dt, Time.realtimeSinceStartupAsDouble);
            if (_lastPhase == SessionPhase.AwaitingAd && Session.Phase == SessionPhase.Running)
            {
                Audio.Play(assets.sfxRevive, 1f);
                Haptics.Play(HapticKind.Success);
            }
            _lastPhase = Session.Phase;

            var sim = Session.Sim;
            if (sim != null)
            {
                if (_origin.Update(sim.Distance)) GameLog.Write(GameLog.Game, "floating origin -> " + _origin.Offset);
                React(sim.Events);
                Environment.Sync(sim.Distance, _origin);
                Track.Sync(sim, _origin, Time.time);
                var frozen = Session.Paused || Session.Phase == SessionPhase.AwaitingAd ||
                             (Session.Tutorial != null && Session.Tutorial.Phase == TutorialPhase.WaitingForInput);
                Player.Sync(sim, _origin, dt, frozen);
                if (Session.Paused != wasPaused) Audio.PauseMusic(Session.Paused);
                if (Session.Phase == SessionPhase.RevivePrompt)
                {
                    var c = Mathf.CeilToInt(Session.PromptLeft);
                    if (c != _lastCountdown) { _lastCountdown = c; Audio.Play(assets.sfxCountdown, 0.7f); }
                }
                else _lastCountdown = -1;
            }
            Hud.Sync(Session);
        }

        void React(in SimEvents e)
        {
            Player.OnEvents(e);
            if (e.SparksCollected > 0) { Audio.Play(assets.sfxSpark, 0.55f, 0.08f); Haptics.Play(HapticKind.Light); }
            if (e.LaneChanged) Audio.Play(assets.sfxLane, 0.5f);
            if (e.Jumped) Audio.Play(assets.sfxJump, 0.7f);
            if (e.Slid) Audio.Play(assets.sfxSlide, 0.7f);
            if (e.PowerUp) { Audio.Play(assets.sfxPowerUp, 0.9f); Haptics.Play(HapticKind.Medium); }
            if (e.ShieldBroke) { Audio.Play(assets.sfxShieldBreak, 0.9f); Haptics.Play(HapticKind.Medium); }
            if (e.Crashed) { Audio.Play(assets.sfxCrash, 1f); Haptics.Play(HapticKind.Heavy); }
        }

        void OnApplicationPause(bool paused)
        {
            // Backgrounded mid-run: come back paused (HANDOVER §10.3 #15).
            if (paused && Session.Phase == SessionPhase.Running) Session.SetPaused(true);
        }
    }
}
