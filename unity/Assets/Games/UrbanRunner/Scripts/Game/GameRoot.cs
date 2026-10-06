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
        public ChaserView Chaser { get; private set; }
        SpeedLinesView _speedLines;
        FestivalSkyView _sky;
        TrailView _trail;
        HuntLanternView _hunt;
        int _huntFound;
        static readonly string[] HuntToasts = { "Lantern 1 of 5!", "Lantern 2 of 5!", "Lantern 3 of 5!", "Lantern 4 of 5!", "Hunt complete!" };
        int _combo;
        float _comboTimer;
        float _slowMo;      // real seconds of near-miss slow motion left
        float _baseTimeScale = 1f;

        /// <summary>Gems collected within this time of each other keep the combo going.</summary>
        const float ComboWindowS = 0.75f;
        bool _bestCelebrated;
        public Hud Hud { get; private set; }
        public PerfReporter Perf { get; private set; }
        public AdaptiveQuality Quality { get; private set; }
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
            GameLog.Write(GameLog.Perf, $"device mem_mb={SystemInfo.systemMemorySize} gpu={SystemInfo.graphicsDeviceName} cpu_cores={SystemInfo.processorCount} refresh_hz={Screen.currentResolution.refreshRateRatio.value:0} screen={Screen.width}x{Screen.height}");
            // Adaptive quality (D31): starts high on capable phones, lower on low-memory ones, then follows real fps.
            Quality = gameObject.AddComponent<AdaptiveQuality>();
            Quality.Init(postFx, HighQuality ? 0 : 2);

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
            Chaser = new GameObject("Chaser").AddComponent<ChaserView>();
            Chaser.transform.SetParent(_world, false);
            Chaser.Init(assets);
            _speedLines = new GameObject("SpeedLines").AddComponent<SpeedLinesView>();
            _speedLines.Init(assets, mainCamera);
            _sky = new GameObject("FestivalSky").AddComponent<FestivalSkyView>();
            _sky.transform.SetParent(_world, false);
            _sky.Init(assets);
            _trail = new GameObject("Trail").AddComponent<TrailView>();
            _trail.Init(assets, Player.Body);
            _hunt = new GameObject("Hunt").AddComponent<HuntLanternView>();
            _hunt.transform.SetParent(_world, false);
            _hunt.Init(assets);

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
            WarmUpShaders();
        }

        /// <summary>Renders one frame with every visual on screen into a tiny texture, so shader compilation happens
        /// during loading (D31). Costs a few hundred ms once, inside the loading time.</summary>
        void WarmUpShaders()
        {
            var started = Time.realtimeSinceStartup;
            Track.ShowAllForWarmup(true, new Vector3(0, 0, 10f));
            var rt = RenderTexture.GetTemporary(256, 512, 24);
            var previous = mainCamera.targetTexture;
            mainCamera.targetTexture = rt;
            Track.DrawSparks();
            mainCamera.Render();
            mainCamera.targetTexture = previous;
            RenderTexture.ReleaseTemporary(rt);
            Track.ShowAllForWarmup(false, Vector3.zero);
            GameLog.Write(GameLog.Perf, $"shader_warmup_ms={(Time.realtimeSinceStartup - started) * 1000f:0}");
        }

        void Tap()
        {
            Audio.Play(assets.sfxUiTap, 0.8f);
            Haptics.Play(HapticKind.Light);
        }

        void ApplySettings(RunSettings s)
        {
            Player.ReduceMotion = s.ReduceMotion;
            _speedLines.ReduceMotion = s.ReduceMotion;
            Haptics.Enabled = s.Haptics;
            Audio.Apply(s.Music, s.Sfx);
        }

        void OnStarted(SessionStart s)
        {
            _origin.Reset();
            Track.ResetSlots();
            Player.SetCharacter(s.Character);
            Player.SnapCamera(_origin, Session.Sim);
            Chaser.SnapAway();
            _trail.Set(s.Trail);
            if (s.HasHunt) _hunt.SetColour(s.HuntColour);
            _huntFound = s.HuntFound;
            _combo = 0;
            Hud.SetCombo(0);
            _bestCelebrated = s.BestDistance <= 0; // nothing to beat on a first run
            Perf.BeginSession();
            Quality.Restart();
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
            // Android back button = Escape in Unity's input system.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame && Session.Back()) Tap();
            // Clamp long frames (e.g. after resume) so the sim never jumps far ahead.
            var dt = Mathf.Min(Time.deltaTime, 0.1f);
            var wasPaused = Session.Paused;
            Session.Tick(dt, Time.realtimeSinceStartupAsDouble);
            // Judge quality only on real running frames (not menus, pauses, ads or tutorial freezes).
            var measuring = Session.Phase == SessionPhase.Running && !Session.Paused &&
                            !(Session.Tutorial != null && Session.Tutorial.Phase == TutorialPhase.WaitingForInput);
            if (measuring && !Quality.Active) Quality.Restart();
            Quality.Active = measuring;
            if (_lastPhase == SessionPhase.AwaitingAd && Session.Phase == SessionPhase.Running)
            {
                Audio.Play(assets.sfxRevive, 1f);
                Haptics.Play(HapticKind.Success);
            }
            _lastPhase = Session.Phase;

            var sim = Session.Sim;
            if (sim != null)
            {
                if (_origin.Update(sim.Distance))
                {
                    GameLog.Write(GameLog.Game, "floating origin -> " + _origin.Offset);
                    _trail.OnOriginShift();
                }
                React(sim.Events);
                Environment.Sync(sim.Distance, _origin);
                Track.Sync(sim, _origin, Time.time);
                var frozen = Session.Paused || Session.Phase == SessionPhase.AwaitingAd ||
                             (Session.Tutorial != null && Session.Tutorial.Phase == TutorialPhase.WaitingForInput);
                Player.Sync(sim, _origin, dt, frozen);
                Chaser.Sync(sim, _origin, dt, frozen);
                _sky.Sync(_origin, sim.Distance, dt, frozen);
                _trail.Emit(sim.State == RunState.Running && !frozen);
                _hunt.Sync(sim, _origin, Time.time);
                if (!frozen && _combo > 0 && (_comboTimer -= dt) <= 0f) { _combo = 0; Hud.SetCombo(0); }
                if (!_bestCelebrated && sim.State == RunState.Running && sim.Distance > Session.Start.BestDistance)
                {
                    _bestCelebrated = true;
                    _sky.Celebrate(_origin.ToWorld(sim.Distance), 4);
                    Hud.Toast("New best!", Icons.Sparkle, new Color32(0x3D, 0xFF, 0xA2, 0xFF));
                    Audio.Play(assets.sfxPowerUp, 0.8f);
                    Haptics.Play(HapticKind.Success);
                }
                var speedT = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(11f, sim.Config.MaxSpeed, sim.Speed)) * (sim.Speed >= 11f ? 1f : 0f);
                var running = sim.State == RunState.Running && !frozen;
                _speedLines.Sync(running ? speedT : 0f, sim.Speed, dt);
                Audio.SetMusicPitch(1f + 0.08f * Mathf.InverseLerp(sim.Config.StartSpeed, sim.Config.MaxSpeed, sim.Speed));
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
            if (e.SparksCollected > 0)
            {
                // Combo: each gem in a quick chain rings a little higher (capped), and the counter bounces.
                _combo += 1;
                _comboTimer = ComboWindowS;
                Hud.SetCombo(_combo);
                Hud.PulseSparks();
                Audio.PlayPitched(assets.sfxSpark, 0.55f, 1f + Mathf.Min(_combo, 20) * 0.025f);
                Haptics.Play(HapticKind.Light);
            }
            if (e.NearMiss)
            {
                Hud.Toast("Close!", Icons.Lightning, new Color32(0x2E, 0xE6, 0xFF, 0xFF));
                Audio.Play(assets.sfxLane, 0.9f);
                Haptics.Play(HapticKind.Light);
                StartSlowMo();
            }
            if (e.Crashed) { _combo = 0; Hud.SetCombo(0); }
            if (e.HuntFound)
            {
                _hunt.Found();
                Hud.Toast(HuntToasts[Mathf.Clamp(_huntFound, 0, 4)], Icons.Sparkle, new Color32(0xFF, 0xB5, 0x47, 0xFF));
                Audio.Play(assets.sfxPowerUp, 1f);
                Haptics.Play(HapticKind.Success);
                if (Session.Sim != null) _sky.Celebrate(_origin.ToWorld(Session.Sim.Distance), _huntFound >= 4 ? 4 : 2);
            }
            if (e.LaneChanged) Audio.Play(assets.sfxLane, 0.5f);
            if (e.Jumped) Audio.Play(assets.sfxJump, 0.7f);
            if (e.Slid) Audio.Play(assets.sfxSlide, 0.7f);
            if (e.PowerUp) { Audio.Play(assets.sfxPowerUp, 0.9f); Haptics.Play(HapticKind.Medium); }
            if (e.ShieldBroke) { Audio.Play(assets.sfxShieldBreak, 0.9f); Haptics.Play(HapticKind.Medium); }
            if (e.Crashed) { Audio.Play(assets.sfxCrash, 1f); Haptics.Play(HapticKind.Heavy); }
            if (e.Stumbled && !e.Crashed)
            {
                Audio.Play(assets.sfxShieldBreak, 0.8f, 0.1f);
                Haptics.Play(HapticKind.Medium);
                Player.Shake();
                Hud.Toast("Warden on you!", Icons.Warning, new Color32(0xFF, 0x5A, 0x5A, 0xFF));
            }
            if (e.SpeedUp)
            {
                Audio.Play(assets.sfxPowerUp, 0.45f);
                Hud.Toast("Faster!", Icons.Lightning, new Color32(0xFF, 0xB5, 0x47, 0xFF));
                if (Session.Sim != null) _sky.Celebrate(_origin.ToWorld(Session.Sim.Distance), 2);
            }
        }

        /// <summary>A heartbeat of slow motion on a near miss (D32 juice); never with Reduce motion.</summary>
        void StartSlowMo()
        {
            if (Player.ReduceMotion || _slowMo > 0f) return;
            _baseTimeScale = Time.timeScale;
            Time.timeScale = _baseTimeScale * 0.35f;
            _slowMo = 0.16f;
        }

        void LateUpdate()
        {
            if (_slowMo <= 0f) return;
            _slowMo -= Time.unscaledDeltaTime;
            if (_slowMo <= 0f) Time.timeScale = _baseTimeScale;
        }

        void OnApplicationPause(bool paused)
        {
            // Backgrounded mid-run: come back paused (HANDOVER §10.3 #15).
            if (paused && Session.Phase == SessionPhase.Running) Session.SetPaused(true);
        }
    }
}
