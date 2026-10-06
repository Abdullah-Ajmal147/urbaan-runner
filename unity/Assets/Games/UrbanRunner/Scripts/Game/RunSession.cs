using System;
using System.Collections.Generic;
using GameFactory.Core;
using GameFactory.Core.Bridge;
using GameFactory.Core.Input;
using Newtonsoft.Json.Linq;
using UrbanRunner.Sim;

namespace UrbanRunner.Game
{
    public enum SessionPhase { Idle, Running, RevivePrompt, AwaitingAd, Ended }

    /// <summary>
    /// The Unity side of one session (bridge contract + HANDOVER §3.8 revive flow), as pure C# so it is EditMode-testable.
    /// Unity never touches balances: it only reports facts in session.ended.
    /// </summary>
    public sealed class RunSession
    {
        public const float RevivePromptSeconds = 5f;
        public const float AdTimeoutSeconds = 60f;
        const float AutoplayAcceptDelay = 0.5f;

        readonly BridgeRouter _router;
        readonly IReadOnlyList<ChunkTemplate> _templates;
        readonly string _buildVersion;

        public SessionPhase Phase { get; private set; } = SessionPhase.Idle;
        public SessionStart Start { get; private set; }
        public RunSimulation Sim { get; private set; }
        public AutoplayBot Bot { get; private set; }
        public TutorialDirector Tutorial { get; private set; }
        public bool TutorialCompleted { get; private set; }
        float _tutorialTime;
        float _tutorialBotWait;
        public bool Paused { get; private set; }
        public RunSettings Settings { get; private set; } = RunSettings.Defaults;

        /// <summary>Seconds left on the "Continue?" prompt.</summary>
        public float PromptLeft { get; private set; }
        double _adRequestedAt;
        double _now;
        bool _forcedCrashDone;

        public event Action<SessionStart> Started;
        public event Action<JObject> Ended;
        public event Action<RunSettings> SettingsChanged;

        public RunSession(BridgeRouter router, IReadOnlyList<ChunkTemplate> templates, string buildVersion)
        {
            _router = router;
            _templates = templates;
            _buildVersion = buildVersion;
            router.On("session.start", OnSessionStart);
            router.On("ad.result", OnAdResult);
            router.On("game.pause", _ => SetPaused(true));
            router.On("game.resume", _ => SetPaused(false));
            router.On("settings.update", e =>
            {
                Settings = RunSettings.Parse(e.Payload, Settings);
                SettingsChanged?.Invoke(Settings);
            });
            // Diagnostics kept from the M0 spike: lets the combined smoke test measure bridge latency.
            router.On("debug.ping", e => router.Send("debug.pong", e.SessionId, new JObject { ["echo"] = e.Payload }));
        }

        void OnSessionStart(BridgeEnvelope e)
        {
            if (Phase != SessionPhase.Idle && Phase != SessionPhase.Ended)
            {
                _router.SendError("session_active", "session.start while " + Phase, e.SessionId);
                return;
            }
            if (string.IsNullOrEmpty(e.SessionId))
            {
                _router.SendError("bad_session", "session.start without sessionId");
                return;
            }
            var s = SessionStart.Parse(e.SessionId, e.Payload);
            Start = s;
            Settings = s.Settings;
            Sim = new RunSimulation(_templates, s.Config, s.Upgrades, s.Seed, s.Mode);
            Bot = s.Test.Autoplay ? new AutoplayBot(Sim, s.Test.Skill, s.Seed) : null;
            Tutorial = s.Mode == RunMode.Tutorial ? new TutorialDirector(Sim) : null;
            // Lantern Hunt (v3): one lantern somewhere between 250 and 600 m; never during the tutorial.
            if (s.HasHunt && Tutorial == null)
                Sim.Track.HuntAtM = new GameFactory.Core.Random.SeededRandom(s.Seed, 12).Range(250f, 600f);
            TutorialCompleted = false;
            _tutorialTime = 0f;
            Paused = false;
            _forcedCrashDone = false;
            Phase = SessionPhase.Running;
            GameLog.Write(GameLog.Game, $"session {s.SessionId} start seed={s.Seed} mode={SessionStart.ModeName(s.Mode)} autoplay={s.Test.Autoplay}");
            _router.Send("session.started", s.SessionId);
            if (Tutorial != null) Analytics("tutorial_begin");
            Started?.Invoke(s);
            SettingsChanged?.Invoke(Settings);
        }

        /// <summary>
        /// The Android back button, if it reaches Unity (Flutter normally forwards it as game.pause): running →
        /// pause, "Keep running?" → no thanks. Never resumes: Back can arrive by both paths, and two deliveries must
        /// not cancel out (D30). Resuming is the pause menu's "Keep running" button. Returns true if it did something.
        /// </summary>
        public bool Back()
        {
            switch (Phase)
            {
                case SessionPhase.Running:
                    if (Paused) return false;
                    SetPaused(true);
                    return true;
                case SessionPhase.RevivePrompt:
                    DeclineRevive();
                    return true;
                default:
                    return false; // waiting for an ad, idle or ended: nothing to do
            }
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            GameLog.Write(GameLog.Game, paused ? "paused" : "resumed");
        }

        /// <summary>Player input (swipes) during a run. Ignored while autoplay drives.</summary>
        public void Input(SwipeDirection d)
        {
            if (Phase != SessionPhase.Running || Paused) return;
            if (Tutorial != null && !Tutorial.Completed) Tutorial.Input(d);
            else if (Bot == null) Sim.Input(d);
        }

        /// <summary>Tutorial phase handling. Returns the dt the simulation should advance by (0 = frozen).</summary>
        float TutorialStep(float dt)
        {
            _tutorialTime += dt;
            if (Bot != null && Tutorial.Phase == TutorialPhase.WaitingForInput)
            {
                _tutorialBotWait += dt;
                if (_tutorialBotWait > 0.4f) { _tutorialBotWait = 0f; Tutorial.Input(Tutorial.Required); }
            }
            var advance = Tutorial.Tick(dt);
            if (Tutorial.Completed)
            {
                TutorialCompleted = true;
                _router.Send("analytics.event", Start.SessionId, new JObject
                {
                    ["name"] = "tutorial_complete",
                    ["params"] = new JObject { ["session_id"] = Start.SessionId, ["duration_s"] = System.Math.Round(_tutorialTime, 1) },
                });
                GameLog.Write(GameLog.Game, "tutorial complete");
            }
            return advance ? dt * TutorialDirector.SpeedFactor : 0f;
        }

        /// <summary>Advance by <paramref name="dt"/> game seconds; <paramref name="now"/> is a real-time clock for timeouts.</summary>
        public void Tick(float dt, double now)
        {
            _now = now;
            switch (Phase)
            {
                case SessionPhase.Running:
                    if (Paused) return;
                    if (Tutorial != null && !Tutorial.Completed)
                    {
                        Sim.Step(TutorialStep(dt));
                        break;
                    }
                    if (Bot != null) Sim.Input(Bot.Decide());
                    Sim.Step(dt);
                    var t = Start.Test;
                    if (t.ForceCrashAtSeconds > 0 && !_forcedCrashDone && Sim.Time >= t.ForceCrashAtSeconds)
                    {
                        _forcedCrashDone = true;
                        Sim.ForceCrash();
                    }
                    if (Sim.State == RunState.Crashed) OnCrash();
                    else if (t.EndAfterSeconds > 0 && Sim.Time >= t.EndAfterSeconds) Finish(EndCause.Test);
                    break;

                case SessionPhase.RevivePrompt:
                    if (Paused) return;
                    PromptLeft -= dt;
                    if (Bot != null && RevivePromptSeconds - PromptLeft >= AutoplayAcceptDelay) AcceptRevive();
                    else if (PromptLeft <= 0) DeclineRevive();
                    break;

                case SessionPhase.AwaitingAd:
                    // Real time, so a frozen player (Flutter pauses Unity during the ad) still respects the limit.
                    if (now - _adRequestedAt >= AdTimeoutSeconds)
                    {
                        GameLog.Write(GameLog.Game, "ad.result timeout: treating as not granted");
                        Finish(EndCause.Obstacle);
                    }
                    break;
            }
        }

        void OnCrash()
        {
            if (Sim.CanRevive)
            {
                Phase = SessionPhase.RevivePrompt;
                PromptLeft = RevivePromptSeconds;
                Analytics("revive_offer");
            }
            else Finish(EndCause.Obstacle);
        }

        /// <summary>"Continue?" → yes. Asks Flutter for a rewarded ad.</summary>
        public void AcceptRevive()
        {
            if (Phase != SessionPhase.RevivePrompt) return;
            Analytics("revive_accept");
            Phase = SessionPhase.AwaitingAd;
            _adRequestedAt = _now;
            _router.Send("ad.request", Start.SessionId, new JObject { ["placement"] = "revive" });
        }

        /// <summary>"Continue?" → no, or the countdown ran out.</summary>
        public void DeclineRevive()
        {
            if (Phase != SessionPhase.RevivePrompt) return;
            Analytics("revive_decline");
            Finish(EndCause.Obstacle);
        }

        void OnAdResult(BridgeEnvelope e)
        {
            if (Phase != SessionPhase.AwaitingAd || (string)e.Payload["placement"] != "revive")
            {
                GameLog.Write(GameLog.Bridge, "ignoring ad.result (phase " + Phase + ")");
                return; // duplicates and late callbacks are harmless
            }
            if (e.SessionId != null && e.SessionId != Start.SessionId)
            {
                GameLog.Write(GameLog.Bridge, "ignoring ad.result for another session");
                return;
            }
            if (e.Payload.Value<bool?>("granted") == true)
            {
                Sim.Revive();
                Phase = SessionPhase.Running;
                GameLog.Write(GameLog.Game, "revived");
            }
            else Finish(EndCause.Obstacle);
        }

        /// <summary>Pause menu → Quit.</summary>
        public void Quit()
        {
            if (Phase == SessionPhase.Running || Phase == SessionPhase.RevivePrompt || Phase == SessionPhase.AwaitingAd)
                Finish(EndCause.Quit);
        }

        void Finish(EndCause cause)
        {
            Sim.End(cause);
            Phase = SessionPhase.Ended;
            Paused = false;
            var s = Sim.Stats;
            var payload = new JObject
            {
                ["mode"] = SessionStart.ModeName(Start.Mode),
                ["seed"] = Start.Seed,
                ["distanceM"] = Math.Round(Sim.Distance, 1),
                ["sparks"] = Sim.Sparks,
                ["durationS"] = Math.Round(Sim.Time, 2),
                ["cause"] = cause == EndCause.Quit ? "quit" : cause == EndCause.Test ? "test" : "obstacle",
                ["revivesUsed"] = Sim.RevivesUsed,
                ["stats"] = new JObject
                {
                    ["jumps"] = s.Jumps,
                    ["slides"] = s.Slides,
                    ["laneChanges"] = s.LaneChanges,
                    ["magnet"] = s.Magnet,
                    ["shield"] = s.Shield,
                    ["doubleSparks"] = s.DoubleSparks,
                },
                ["buildVersion"] = _buildVersion,
                ["tutorialCompleted"] = TutorialCompleted,
                ["huntFound"] = Sim.HuntCollected,
            };
            GameLog.Write(GameLog.Game, $"session {Start.SessionId} ended cause={payload["cause"]} distance={Sim.Distance:F0} sparks={Sim.Sparks}");
            _router.Send("session.ended", Start.SessionId, payload);
            Ended?.Invoke(payload);
        }

        void Analytics(string name) =>
            _router.Send("analytics.event", Start.SessionId, new JObject
            {
                ["name"] = name,
                ["params"] = new JObject { ["session_id"] = Start.SessionId },
            });
    }
}
