using System;
using GameFactory.Core.Autoplay;
using GameFactory.Core.Input;
using GameFactory.Core.Random;

namespace UrbanRunner.Sim
{
    /// <summary>
    /// Looks ahead and dodges (HANDOVER §9 M1). Skill 1 plays perfectly; lower skill adds reaction delays and missed rows.
    /// It plays through the same input path as a human (buffered swipes), so it tests the real controls.
    /// </summary>
    public sealed class AutoplayBot : IAutoplayAgent<SwipeDirection>
    {
        readonly RunSimulation _sim;
        readonly float _skill;
        SeededRandom _rng;

        int _decidedRow = -1;
        bool _ignoreRow;
        double _reactAt;

        public AutoplayBot(RunSimulation sim, float skill, ulong seed)
        {
            _sim = sim;
            _skill = Math.Max(0f, Math.Min(1f, skill));
            _rng = new SeededRandom(seed, 7);
        }

        public SwipeDirection Decide()
        {
            if (_sim.State != RunState.Running) return SwipeDirection.None;

            var hd = TrackRules.PlayerHalfDepth;
            var rows = _sim.Track.Rows;
            var idx = -1;
            for (var i = 0; i < rows.Count; i++)
            {
                if (rows[i].End > _sim.Distance - hd && !IsClear(rows[i])) { idx = i; break; }
            }
            if (idx < 0) return SwipeDirection.None;
            ref var row = ref rows[idx];

            if (row.Id != _decidedRow)
            {
                _decidedRow = row.Id;
                var miss = (1f - _skill) * 0.6f;
                _ignoreRow = _rng.Chance(miss * 0.3f);
                _reactAt = _sim.Time + (_rng.Chance(miss) ? _rng.Range(0.15f, 0.5f) : 0f);
            }
            if (_ignoreRow || _sim.Time < _reactAt) return SwipeDirection.None;

            var inside = _sim.Distance + hd >= row.Z;
            var target = inside ? _sim.Lane : ChooseLane(row);
            if (target != _sim.Lane) return target < _sim.Lane ? SwipeDirection.Left : SwipeDirection.Right;
            if (Math.Abs(_sim.X - TrackRules.LaneX(target)) > 0.3f) return SwipeDirection.None;

            var o = row.Effective(target);
            var v = _sim.Speed;
            var mid = row.Z + TrackRules.Length(o) * 0.5;
            var toMid = mid - _sim.Distance;
            switch (o)
            {
                case Obstacle.Crate:
                    // Centre the obstacle in the jump's air window (u = 0.5 ≈ 0.31 s after take-off).
                    if (!_sim.Airborne && toMid <= v * TrackRules.JumpDuration * 0.5f && toMid > 0) return SwipeDirection.Up;
                    break;
                case Obstacle.Banner:
                    var needSlide = toMid <= v * 0.3f + hd && toMid > -hd;
                    var slideEndsTooSoon = !_sim.Sliding || _sim.SlideProgress > 0.6f;
                    if (needSlide && (slideEndsTooSoon || _sim.Airborne)) return SwipeDirection.Down;
                    break;
            }
            return SwipeDirection.None;
        }

        static bool IsClear(in TrackRow r) =>
            r.Effective(0) == Obstacle.None && r.Effective(1) == Obstacle.None && r.Effective(2) == Obstacle.None;

        /// <summary>Nearest passable lane; prefers tram roofs (spark lines up there), then no action over jump/slide.</summary>
        int ChooseLane(in TrackRow row)
        {
            var best = _sim.Lane;
            var bestCost = float.MaxValue;
            for (var l = 0; l < TrackRules.LaneCount; l++)
            {
                var o = row.Effective(l);
                if (!TrackRules.IsPassable(o)) continue;
                var cost = Math.Abs(l - _sim.Lane) + (o == Obstacle.TramRamp ? -0.4f : o == Obstacle.None ? 0f : 0.6f);
                if (cost < bestCost) { bestCost = cost; best = l; }
            }
            return best;
        }
    }
}
