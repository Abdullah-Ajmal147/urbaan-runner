using System;
using System.Collections.Generic;
using GameFactory.Core.Input;

namespace UrbanRunner.Sim
{
    public enum RunState { Running, Crashed, Ended }

    public enum EndCause { Obstacle, Quit, Test }

    /// <summary>Counters reported in session.ended → stats.</summary>
    public struct RunStats
    {
        public int Jumps, Slides, LaneChanges;
        public int Magnet, Shield, DoubleSparks; // power-ups picked up
    }

    /// <summary>What happened during the last <see cref="RunSimulation.Step"/> (for audio, haptics and particles).</summary>
    public struct SimEvents
    {
        public int SparksCollected;
        public bool Jumped, Slid, LaneChanged, Bumped, ShieldBroke, Crashed;
        public bool PowerUp;
        public PowerUpKind PowerUpKind;

        public void Clear() => this = default;
    }

    /// <summary>
    /// The whole run as pure C#: no MonoBehaviours and no allocations per step. Views render it and feed it input.
    /// Deterministic for a given seed + input sequence + dt sequence.
    /// </summary>
    public sealed class RunSimulation
    {
        const float MaxSubStep = 1f / 120f;
        const double LookAheadM = 160.0;
        const double RecycleBehindM = 12.0;

        public TrackGenerator Track { get; }
        public RunConfig Config { get; }
        public Upgrades Upgrades { get; }
        public RunMode Mode { get; }
        public ulong Seed { get; }

        public RunState State { get; private set; } = RunState.Running;
        public EndCause Cause { get; private set; }
        public double Time { get; private set; }
        public double Distance { get; private set; }
        public float Speed { get; private set; }

        public int Lane { get; private set; } = 1;
        public float X { get; private set; }
        public float Y { get; private set; }
        public bool Airborne => _jumpT >= 0f;
        public bool Sliding => _slideT >= 0f;
        public float SlideProgress => Sliding ? _slideT / TrackRules.SlideDuration : 0f;
        public float JumpProgress => Airborne ? _jumpT / TrackRules.JumpDuration : 0f;

        /// <summary>Tutorial: obstacles never hurt (HANDOVER §3.9 "the player can't lose").</summary>
        public bool GodMode { get; set; }

        public int Sparks { get; private set; }
        public RunStats Stats;
        public SimEvents Events;

        public float MagnetLeft { get; private set; }
        public float ShieldLeft { get; private set; }
        public float DoubleLeft { get; private set; }
        public float InvulnerableLeft { get; private set; }
        public int RevivesUsed { get; private set; }
        public Obstacle CrashedInto { get; private set; }

        readonly InputBuffer _buffer = new InputBuffer(TrackRules.InputBufferS);
        float _jumpT = -1f;
        float _slideT = -1f;

        public RunSimulation(IReadOnlyList<ChunkTemplate> templates, RunConfig config, Upgrades upgrades, ulong seed, RunMode mode = RunMode.Normal)
        {
            Config = config.Clamped();
            Upgrades = upgrades.Clamped();
            Mode = mode;
            Seed = seed;
            Track = new TrackGenerator(templates, Config, seed);
            Speed = DifficultyCurve.Speed(Config, 0);
            Track.GenerateUntil(LookAheadM);
        }

        public void Input(SwipeDirection d) => _buffer.Push(d, (float)Time);

        public void Step(float dt)
        {
            Events.Clear();
            if (State != RunState.Running || dt <= 0f) return;
            while (dt > 0f && State == RunState.Running)
            {
                var h = dt > MaxSubStep ? MaxSubStep : dt;
                SubStep(h);
                dt -= h;
            }
            Track.GenerateUntil(Distance + LookAheadM);
            Track.Recycle(Distance - RecycleBehindM);
        }

        void SubStep(float h)
        {
            Time += h;
            Speed = DifficultyCurve.Speed(Config, Time);
            Distance += Speed * h;

            HandleInput();
            MovePlayer(h);
            TickTimers(h);
            CheckObstacles();
            CollectSparks();
            CollectPowerUps();
        }

        void HandleInput()
        {
            var d = _buffer.Peek((float)Time);
            switch (d)
            {
                case SwipeDirection.Left:
                case SwipeDirection.Right:
                    _buffer.Consume((float)Time);
                    var target = Lane + (d == SwipeDirection.Left ? -1 : 1);
                    if (target < 0 || target >= TrackRules.LaneCount) { Events.Bumped = true; break; }
                    Lane = target;
                    Stats.LaneChanges++;
                    Events.LaneChanged = true;
                    break;
                case SwipeDirection.Up:
                    if (Airborne) break; // stays buffered: a jump pressed just before landing still counts
                    _buffer.Consume((float)Time);
                    _slideT = -1f;
                    _jumpT = 0f;
                    Stats.Jumps++;
                    Events.Jumped = true;
                    break;
                case SwipeDirection.Down:
                    _buffer.Consume((float)Time);
                    _jumpT = -1f; // fast-fall into a slide
                    Y = 0f;
                    _slideT = 0f;
                    Stats.Slides++;
                    Events.Slid = true;
                    break;
            }
        }

        void MovePlayer(float h)
        {
            var tx = TrackRules.LaneX(Lane);
            var step = TrackRules.LaneWidth / TrackRules.LaneChangeDuration * h;
            X = Math.Abs(tx - X) <= step ? tx : X + Math.Sign(tx - X) * step;

            if (_jumpT >= 0f)
            {
                _jumpT += h;
                if (_jumpT >= TrackRules.JumpDuration) { _jumpT = -1f; Y = 0f; }
                else
                {
                    var u = _jumpT / TrackRules.JumpDuration;
                    Y = 4f * TrackRules.JumpHeight * u * (1f - u);
                }
            }
            if (_slideT >= 0f)
            {
                _slideT += h;
                if (_slideT >= TrackRules.SlideDuration) _slideT = -1f;
            }
        }

        void TickTimers(float h)
        {
            if (MagnetLeft > 0f) MagnetLeft = Math.Max(0f, MagnetLeft - h);
            if (ShieldLeft > 0f) ShieldLeft = Math.Max(0f, ShieldLeft - h);
            if (DoubleLeft > 0f) DoubleLeft = Math.Max(0f, DoubleLeft - h);
            if (InvulnerableLeft > 0f) InvulnerableLeft = Math.Max(0f, InvulnerableLeft - h);
        }

        public float PlayerTop => Y + (Sliding ? TrackRules.SlidingHeight : TrackRules.StandingHeight);

        void CheckObstacles()
        {
            var front = Distance + TrackRules.PlayerHalfDepth;
            var back = Distance - TrackRules.PlayerHalfDepth;
            var rows = Track.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                if (row.Z > front) break; // rows are sorted by Z
                if (row.End < back) continue;
                for (var lane = 0; lane < TrackRules.LaneCount; lane++)
                {
                    var o = row.Effective(lane);
                    if (o == Obstacle.None) continue;
                    if (row.Z + TrackRules.Length(o) < back) continue;
                    if (Math.Abs(X - TrackRules.LaneX(lane)) >= TrackRules.PlayerHalfWidth + TrackRules.ObstacleHalfWidth) continue;
                    if (!Hits(o)) continue;

                    if (InvulnerableLeft > 0f || GodMode) continue;
                    if (ShieldLeft > 0f)
                    {
                        ShieldLeft = 0f;
                        InvulnerableLeft = 1f;
                        row.Cleared |= (byte)(1 << lane);
                        Events.ShieldBroke = true;
                        continue;
                    }
                    Crash(o);
                    return;
                }
            }
        }

        bool Hits(Obstacle o)
        {
            switch (o)
            {
                case Obstacle.Crate: return Y < TrackRules.CrateHeight;
                case Obstacle.Banner: return PlayerTop > TrackRules.BannerBottom;
                default: return true; // stall, van, cart: only a lane change avoids them
            }
        }

        void CollectSparks()
        {
            var sparks = Track.Sparks;
            var magnet = MagnetLeft > 0f;
            var reach = magnet ? 3.0 : 0.9;
            for (var i = 0; i < sparks.Count; i++)
            {
                ref var s = ref sparks[i];
                if (s.Z > Distance + reach) break;
                if (s.Collected || s.Z < Distance - reach) continue;
                bool inReach;
                if (magnet) inReach = Math.Abs(s.Lane - Lane) <= 1;
                else inReach = Math.Abs(X - TrackRules.LaneX(s.Lane)) < 1.0f && s.Y >= Y - 0.3f && s.Y <= PlayerTop + 0.3f;
                if (!inReach) continue;
                s.Collected = true;
                var gained = DoubleLeft > 0f ? 2 : 1;
                Sparks += gained;
                Events.SparksCollected += gained;
            }
        }

        void CollectPowerUps()
        {
            var list = Track.PowerUps;
            for (var i = 0; i < list.Count; i++)
            {
                ref var p = ref list[i];
                if (p.Z > Distance + 1.0) break;
                if (p.Collected || p.Z < Distance - 1.0) continue;
                if (Math.Abs(X - TrackRules.LaneX(p.Lane)) >= 1.2f) continue;
                p.Collected = true;
                Activate(p.Kind);
            }
        }

        public void Activate(PowerUpKind kind)
        {
            var d = PowerUpRules.Duration(kind, Upgrades);
            switch (kind)
            {
                case PowerUpKind.Magnet: MagnetLeft = d; Stats.Magnet++; break;
                case PowerUpKind.Shield: ShieldLeft = d; Stats.Shield++; break;
                default: DoubleLeft = d; Stats.DoubleSparks++; break;
            }
            Events.PowerUp = true;
            Events.PowerUpKind = kind;
        }

        void Crash(Obstacle o)
        {
            CrashedInto = o;
            State = RunState.Crashed;
            Events.Crashed = true;
            _jumpT = -1f;
            _slideT = -1f;
            Y = 0f;
        }

        /// <summary>Test hook (testMode.forceCrashAtSeconds): crash as if an obstacle was hit.</summary>
        public void ForceCrash()
        {
            if (State == RunState.Running) Crash(Obstacle.Stall);
        }

        public bool CanRevive => State == RunState.Crashed && Config.ReviveEnabled && RevivesUsed < Config.RevivesPerRun;

        /// <summary>HANDOVER §3.8: clear nearby obstacles, 2 s of invulnerability, resume.</summary>
        public void Revive()
        {
            if (!CanRevive) throw new InvalidOperationException("no revive available");
            RevivesUsed++;
            var clearTo = Distance + Speed * 2.5 + 10.0;
            var rows = Track.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var r = ref rows[i];
                if (r.Z > clearTo) break;
                r.Cleared = 0b111;
            }
            InvulnerableLeft = 2f;
            State = RunState.Running;
        }

        public void End(EndCause cause)
        {
            if (State == RunState.Ended) return;
            Cause = cause;
            State = RunState.Ended;
        }
    }
}
