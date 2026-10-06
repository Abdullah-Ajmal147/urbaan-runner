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
        /// <summary>Side bump: bounced back to the previous lane; the Warden closes in.</summary>
        public bool Stumbled;
        public bool Landed;
        /// <summary>Speed passed the next of <see cref="TrackRules.SpeedSteps"/>.</summary>
        public bool SpeedUp;
        /// <summary>Dodged a blocking obstacle by changing lanes in the last moment (D32: "Close!").</summary>
        public bool NearMiss;
        /// <summary>Picked up today's Lantern Hunt lantern.</summary>
        public bool HuntFound;
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
        public float VelocityY { get; private set; }
        public bool Airborne { get; private set; }
        public bool Sliding => _slideT >= 0f;
        public float SlideProgress => Sliding ? _slideT / TrackRules.SlideDuration : 0f;
        /// <summary>0 at take-off, 0.5 at the apex, 1 on landing (for the jump animation).</summary>
        public float JumpProgress => Airborne ? Math.Max(0f, Math.Min(1f, (TrackRules.JumpVelocity - VelocityY) / (2f * TrackRules.JumpVelocity))) : 0f;
        /// <summary>Height of the surface under the player (0 = street, TramHeight = a roof).</summary>
        public float GroundY { get; private set; }
        public bool OnRoof => !Airborne && GroundY >= TrackRules.TramHeight - 0.01f;

        /// <summary>Seconds left in which a second side bump means the Warden catches the courier.</summary>
        public float ChaseLeft { get; private set; }
        /// <summary>The run ended because the Warden caught the courier after two bumps.</summary>
        public bool Caught { get; private set; }
        /// <summary>How many of <see cref="TrackRules.SpeedSteps"/> the run has passed.</summary>
        public int SpeedLevel { get; private set; }

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
        float _slideT = -1f;
        bool _slideOnLanding;
        int _laneFrom = 1;
        double _laneChangedAt = -10;

        /// <summary>A lane change this soon before reaching a blocking obstacle in the old lane counts as a near miss.</summary>
        public const float NearMissWindowS = 0.3f;

        public RunSimulation(IReadOnlyList<ChunkTemplate> templates, RunConfig config, Upgrades upgrades, ulong seed, RunMode mode = RunMode.Normal)
        {
            Config = config.Clamped();
            Upgrades = upgrades.Clamped();
            Mode = mode;
            Seed = seed;
            Track = new TrackGenerator(templates, Config, seed);
            Speed = DifficultyCurve.Speed(Config, 0);
            // Steps below the starting speed are never announced.
            while (SpeedLevel < TrackRules.SpeedSteps.Length && Speed >= TrackRules.SpeedSteps[SpeedLevel]) SpeedLevel++;
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
            var before = Distance;
            Distance += Speed * h;
            DetectNearMiss(before);
            if (SpeedLevel < TrackRules.SpeedSteps.Length && Speed >= TrackRules.SpeedSteps[SpeedLevel])
            {
                SpeedLevel++;
                Events.SpeedUp = true;
            }

            HandleInput();
            MovePlayer(h);
            TickTimers(h);
            CheckObstacles();
            CollectSparks();
            CollectPowerUps();
            CollectHunt();
        }

        void CollectHunt()
        {
            ref var h = ref Track.Hunt;
            if (!h.Placed || h.Collected) return;
            if (Math.Abs(h.Z - Distance) > 1.0 || Math.Abs(X - TrackRules.LaneX(h.Lane)) >= 1.2f) return;
            h.Collected = true;
            Events.HuntFound = true;
        }

        /// <summary>Whether this run's hunt lantern was picked up (reported in session.ended).</summary>
        public bool HuntCollected => Track.Hunt.Collected;

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
                    _laneFrom = Lane;
                    _laneChangedAt = Time;
                    Lane = target;
                    Stats.LaneChanges++;
                    Events.LaneChanged = true;
                    break;
                case SwipeDirection.Up:
                    if (Airborne) break; // stays buffered: a jump pressed just before landing still counts
                    _buffer.Consume((float)Time);
                    _slideT = -1f;
                    Airborne = true;
                    VelocityY = TrackRules.JumpVelocity;
                    Stats.Jumps++;
                    Events.Jumped = true;
                    break;
                case SwipeDirection.Down:
                    _buffer.Consume((float)Time);
                    Stats.Slides++;
                    Events.Slid = true;
                    if (Airborne)
                    {
                        // Slam down, then slide the moment the feet touch.
                        VelocityY = -TrackRules.FastFallVelocity;
                        _slideOnLanding = true;
                    }
                    else _slideT = 0f;
                    break;
            }
        }

        void MovePlayer(float h)
        {
            var tx = TrackRules.LaneX(Lane);
            var step = TrackRules.LaneWidth / TrackRules.LaneChangeDuration * h;
            X = Math.Abs(tx - X) <= step ? tx : X + Math.Sign(tx - X) * step;

            if (Airborne)
            {
                VelocityY -= TrackRules.Gravity * h;
                Y += VelocityY * h;
            }
            GroundY = SurfaceUnder();
            if (Y <= GroundY)
            {
                // Feet on the street, a ramp or a roof (walking up a ramp lifts the player smoothly).
                if (Airborne && VelocityY <= 0f)
                {
                    Airborne = false;
                    Events.Landed = true;
                    if (_slideOnLanding) { _slideT = 0f; _slideOnLanding = false; }
                }
                if (!Airborne) { Y = GroundY; VelocityY = 0f; }
            }
            else if (!Airborne)
            {
                // Ran off the end of a roof: fall.
                Airborne = true;
                VelocityY = 0f;
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
            if (ChaseLeft > 0f) ChaseLeft = Math.Max(0f, ChaseLeft - h);
        }

        public float PlayerTop => Y + (Sliding ? TrackRules.SlidingHeight : TrackRules.StandingHeight);

        /// <summary>
        /// Highest walkable surface under the player's lane: a ramp always carries the player up; a roof only
        /// counts when the feet are already near or above it (otherwise it's the tram's side: a hit).
        /// </summary>
        float SurfaceUnder()
        {
            var lane = (int)Math.Round(X / TrackRules.LaneWidth) + 1;
            if (lane < 0 || lane >= TrackRules.LaneCount) return 0f;
            var best = 0f;
            var rows = Track.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                if (row.Z > Distance + TrackRules.PlayerHalfDepth) break;
                if (row.End < Distance - TrackRules.PlayerHalfDepth) continue;
                var o = row.Effective(lane);
                if (o != Obstacle.Tram && o != Obstacle.TramRamp) continue;
                // Under the feet, the surface reaches a bit beyond the roof's end (half a body), like a ledge.
                var s = Math.Min(Distance + TrackRules.PlayerHalfDepth - row.Z, TrackRules.Length(o));
                var surface = TrackRules.SurfaceAt(o, s);
                var onRamp = o == Obstacle.TramRamp && s < TrackRules.RampLength;
                if (!onRamp && Y < surface - TrackRules.RoofSnap) continue;
                if (surface > best) best = surface;
            }
            return best;
        }

        /// <summary>The front of a row passed the player this step: was it a last-moment dodge?</summary>
        void DetectNearMiss(double before)
        {
            if (_laneFrom == Lane || Time - _laneChangedAt > NearMissWindowS) return;
            var rows = Track.Rows;
            for (var i = 0; i < rows.Count; i++)
            {
                ref var row = ref rows[i];
                var front = row.Z - TrackRules.PlayerHalfDepth;
                if (front > Distance) break;
                if (front <= before) continue;
                if (TrackRules.IsBlocking(row.Effective(_laneFrom)) && !TrackRules.IsBlocking(row.Effective(Lane)))
                {
                    Events.NearMiss = true;
                    _laneChangedAt = -10; // one per dodge
                }
            }
        }

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
                    if (!Hits(o, Distance - row.Z)) continue;

                    if (InvulnerableLeft > 0f || GodMode) continue;
                    // Side bump: moving into a lane beside an obstacle we're already alongside. Bounce back and stumble.
                    var alongside = Distance - TrackRules.PlayerHalfDepth > row.Z + 0.25;
                    if (lane == Lane && _laneFrom != Lane && alongside && Math.Abs(X - TrackRules.LaneX(Lane)) > 0.2f)
                    {
                        if (Stumble(o)) return;
                        continue;
                    }
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

        bool Hits(Obstacle o, double s)
        {
            switch (o)
            {
                case Obstacle.Crate: return Y < TrackRules.CrateHeight;
                case Obstacle.Banner: return PlayerTop > TrackRules.BannerBottom;
                case Obstacle.TramRamp:
                    if (s + TrackRules.PlayerHalfDepth < TrackRules.RampLength) return false; // on the ramp
                    return Y < TrackRules.TramHeight - TrackRules.RoofSnap;
                case Obstacle.Tram: return Y < TrackRules.TramHeight - TrackRules.RoofSnap;
                default: return true; // stall, van, cart: only a lane change avoids them
            }
        }

        /// <summary>Bounce back to the lane we came from. Returns true when this second bump means caught.</summary>
        bool Stumble(Obstacle o)
        {
            Lane = _laneFrom;
            Events.Stumbled = true;
            if (ChaseLeft > 0f)
            {
                Caught = true;
                Crash(o);
                return true;
            }
            ChaseLeft = TrackRules.ChaseWindowS;
            return false;
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
            _slideT = -1f;
            _slideOnLanding = false;
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
            ChaseLeft = 0f;
            Caught = false;
            // Back on the street (or the roof) in the current lane.
            _laneFrom = Lane;
            X = TrackRules.LaneX(Lane);
            Airborne = Y > SurfaceUnder() + 0.01f;
            VelocityY = 0f;
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
